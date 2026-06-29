using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Convergence.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vela.Contracts.Entities;
using Vela.Services.Contracts;

namespace Vela.Services.Impl;

public class ConvergeDbWriter : IConvergeDbWriter
{
    private readonly ILogger<ConvergeDbWriter> _logger;
    private readonly IOptions<BitcraftServiceOptions> _bitcraftOptions;
    private readonly IOptions<ConvergeDbOptions> _options;
    private readonly IFatalRestart _fatalRestart;
    private ConvergenceClient? _client;
    private readonly ConcurrentDictionary<Type, object> _kindHandles = new();
    private int _activeEpochs;

    // Seam over the steady-state ConvergeDB write path (buffer + flush + epoch boundary) so the
    // tick-flush gating, dirty-skip and transport-failure handling below can be unit-tested
    // without a live ConvergeDB server. Production uses ConvergenceClientSink; set in
    // InitializeAsync (or injected directly via the internal test constructor).
    private IConvergenceWriteSink? _sink;

    // Set on every buffered steady-state write, cleared on flush, so idle ticks can skip
    // flushing entirely (ConvergenceClient.FlushAsync still awaits a TCP pipe flush even with an
    // empty buffer). Effectively single-threaded - buffering happens inside conn.FrameTick() and
    // the flush immediately after, both on the connection-loop thread - but accessed via
    // Volatile/Interlocked so the test-and-clear is well defined regardless.
    private int _dirty;

    public ConvergeDbWriter(
        ILogger<ConvergeDbWriter> logger,
        IOptions<BitcraftServiceOptions> bitcraftOptions,
        IOptions<ConvergeDbOptions> options,
        IFatalRestart fatalRestart
    )
    {
        _logger = logger;
        _bitcraftOptions = bitcraftOptions;
        _options = options;
        _fatalRestart = fatalRestart;
    }

    // Test-only: inject a fake write sink instead of connecting to a real ConvergeDB server.
    // Skips InitializeAsync (so the option fields stay unused); all steady-state-flush logic runs
    // against the supplied sink.
    internal ConvergeDbWriter(
        ILogger<ConvergeDbWriter> logger,
        IFatalRestart fatalRestart,
        IConvergenceWriteSink sink
    )
    {
        _logger = logger;
        // Option fields are only read by InitializeAsync, which tests skip.
        _bitcraftOptions = null!;
        _options = null!;
        _fatalRestart = fatalRestart;
        _sink = sink;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var opts = _options.Value;
        var module = _bitcraftOptions.Value.Module;
        var sourceId = DeriveSourceIdFromModule(module);

        _client = await ConvergenceClient.ConnectAsync(new ConvergenceOptions
        {
            Host = opts.Host,
            Port = opts.Port,
            Name = module,
            SourceId = sourceId,
        }, ct);

        _logger.LogInformation("Connected to ConvergeDB at {Host}:{Port} as source {SourceId} (derived from module {Module})",
            opts.Host, opts.Port, sourceId, module);

        // Register all entity kinds
        await RegisterAllKindsAsync(ct);

        // Wire the production write sink now that the client + kind handles exist.
        _sink = new ConvergenceClientSink(this);
    }

    private async Task RegisterAllKindsAsync(CancellationToken ct)
    {
        await RegisterKindAsync<ConvergeBuildingState>(ct);
        await RegisterKindAsync<ConvergeClaimState>(ct);
        await RegisterKindAsync<ConvergeClaimLocalState>(ct);
        await RegisterKindAsync<ConvergeClaimTechState>(ct);
        await RegisterKindAsync<ConvergeEmpireState>(ct);
        await RegisterKindAsync<ConvergeEmpireNodeState>(ct);
        await RegisterKindAsync<ConvergeEmpireNodeSiegeState>(ct);
        await RegisterKindAsync<ConvergeUserState>(ct);
        await RegisterKindAsync<ConvergeUsernameState>(ct);
        await RegisterKindAsync<ConvergeLocationState>(ct);
        await RegisterKindAsync<ConvergeProgressiveAction>(ct);
        await RegisterKindAsync<ConvergePublicProgressiveAction>(ct);
        await RegisterKindAsync<ConvergeChatMessage>(ct);
        await RegisterKindAsync<ConvergeActionLogState>(ct);
        await RegisterKindAsync<ConvergeAuctionListingState>(ct);
        await RegisterKindAsync<ConvergeClosedListingState>(ct);
        await RegisterKindAsync<ConvergeInventoryState>(ct);

        _logger.LogInformation("Registered {Count} entity kinds with ConvergeDB", _kindHandles.Count);
    }

    private async Task RegisterKindAsync<T>(CancellationToken ct) where T : struct, IConvergenceEntity<T>
    {
        _logger.LogInformation("Registering kind {KindName}...", T.KindName);
        var handle = await _client!.RegisterKindAsync<T>(ct);
        _kindHandles[typeof(T)] = handle;
        _logger.LogInformation("Registered kind {KindName} (id={KindId})", T.KindName, handle.KindId);
    }

    public KindHandle<T> GetKind<T>() where T : struct, IConvergenceEntity<T>
    {
        return (KindHandle<T>)_kindHandles[typeof(T)];
    }

    // Buffer-only: the write is appended to the client's pending-op buffer and sent later by
    // FlushPendingAsync (once per connection-loop tick). Batching only changes how ops are framed
    // on the wire, not their content/order/identity - arrival order is preserved end to end.
    //
    // NOTE (converged state only): ConvergeDB's server-side accumulator coalesces writes to the
    // same entity within its ~20ms time-based window by last-writer-wins - field data, source bits
    // AND metadata. So in the converged STATE view (subscriptions/point-queries), if the same
    // entity is asserted twice in one window with different metadata only the last op's metadata
    // survives. This is true with or without client-side batching (the window is time-based, not
    // frame-based) and is not a batching regression. Event-stream kinds are NOT affected: the
    // server records one event per op (with its own metadata) BEFORE coalescing, and a BATCH frame
    // is processed op-by-op, so no event or metadata is lost there.
    public async Task AssertAsync<T>(T entity, EntityMetadata? metadata = null) where T : struct, IConvergenceEntity<T>
    {
        await _sink!.AssertAsync(entity, metadata);
        Volatile.Write(ref _dirty, 1);
    }

    public async Task RetractAsync<T>(ReadOnlyMemory<byte> entityId, EntityMetadata? metadata = null) where T : struct, IConvergenceEntity<T>
    {
        await _sink!.RetractAsync<T>(entityId, metadata);
        Volatile.Write(ref _dirty, 1);
    }

    public async Task FlushPendingAsync(CancellationToken ct)
    {
        // Gate 1: while a snapshot epoch is open the streaming snapshot owns flushing of the
        // shared client buffer; a per-tick steady-state flush must not fire. Read across threads -
        // _activeEpochs is mutated on the lifecycle/finalize threads (Begin/EndEpochAsync).
        if (Volatile.Read(ref _activeEpochs) > 0)
            return;

        // Gate 2: nothing buffered since the last flush - skip the idle tick entirely.
        if (Interlocked.Exchange(ref _dirty, 0) == 0)
            return;

        try
        {
            await _sink!.FlushAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            FatalConvergeDbFailure("flush", ex);
        }
    }

    public async Task EpochAsync(Func<Task> body)
    {
        await _client!.EpochAsync(body);
    }

    public async Task BeginEpochAsync(CancellationToken ct = default)
    {
        // Double-begin is a Vela logic error, not a ConvergeDB I/O failure - fail fast rather than
        // restart, so the bug surfaces instead of hiding behind a container bounce.
        if (Interlocked.Increment(ref _activeEpochs) > 1)
        {
            Interlocked.Decrement(ref _activeEpochs);
            throw new InvalidOperationException("Cannot begin a ConvergeDB epoch while another is already open");
        }

        try
        {
            await _sink!.EpochBeginAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            FatalConvergeDbFailure("begin epoch", ex);
        }
    }

    public async Task EndEpochAsync(CancellationToken ct = default)
    {
        try
        {
            await _sink!.EpochEndAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            FatalConvergeDbFailure("end epoch", ex);
        }
        finally
        {
            Interlocked.Decrement(ref _activeEpochs);
        }
    }

    // Single, uniform reaction to a ConvergeDB I/O failure (begin/end epoch or flush): it is always
    // fatal. ConvergeDB is the system of record; a dropped or absent connection means the current
    // source epoch is compromised, and in-process recovery would leak stale entities. Hand off to
    // IFatalRestart, which exits the process so the orchestrator restarts it and the startup path
    // re-seeds from a clean epoch. Buffered-but-unsent ops are lost - fine, the re-seed restores
    // everything.
    //
    // Callers swallow once this returns: the exception's only job was to surface a broken ConvergeDB,
    // and that is now done. Every background loop unwinds on the cancellation the restart triggers,
    // so re-throwing would only risk the failure being mistaken for a recoverable SpacetimeDB blip
    // (the original hang).
    //
    // The catch filter is `when (!ct.IsCancellationRequested)` - NOT a type check. Rationale: the
    // Convergence client signals a dropped connection as a bare InvalidOperationException("Not
    // connected."), and a mid-request drop can even surface as an OperationCanceledException from the
    // client's own internal token - so no exception type reliably means "transport failure". The one
    // thing that reliably means "this is benign" is OUR token being cancelled: that is the host
    // stopping (graceful shutdown or a restart already in flight), where we must let the exception
    // propagate and unwind rather than escalate a clean stop into a failure exit.
    private void FatalConvergeDbFailure(string operation, Exception ex)
    {
        _logger.LogError(ex, "ConvergeDB {Operation} failed", operation);
        _fatalRestart.Trigger($"ConvergeDB {operation} failed: {ex.Message}");
    }

    public ConvergenceBatch Batch() => _client!.Batch();

    // Derives ConvergeDB SourceId from the trailing "-N" segment of the module name
    // (e.g. "bitcraft-live-12" → 12). SourceId is a byte, so N must be 0-255.
    private static byte DeriveSourceIdFromModule(string module)
    {
        var match = Regex.Match(module, @"-(\d+)$");
        if (!match.Success || !byte.TryParse(match.Groups[1].Value, out var sourceId))
        {
            throw new InvalidOperationException(
                $"Bitcraft:Module '{module}' must end with '-<number>' (0-255) to derive ConvergeDB SourceId");
        }
        return sourceId;
    }

    // Production write sink backed by the real ConvergenceClient. Assert/Retract are routed
    // through a single long-lived ConvergenceBatch: although ConvergenceBatch is documented as a
    // scoped/await-using type, it holds no state of its own - it's a thin typed-encode shim over
    // the client's shared pending-op buffer (its Assert/Retract buffer; FlushAsync drains and is
    // reusable). Holding one for the process lifetime avoids re-allocating it per write; it is
    // intentionally never disposed (disposal would merely flush).
    private sealed class ConvergenceClientSink : IConvergenceWriteSink
    {
        private readonly ConvergeDbWriter _owner;
        private readonly ConvergenceBatch _batch;

        public ConvergenceClientSink(ConvergeDbWriter owner)
        {
            _owner = owner;
            _batch = owner._client!.Batch();
        }

        public ValueTask AssertAsync<T>(T entity, EntityMetadata? metadata) where T : struct, IConvergenceEntity<T>
            => _batch.AssertAsync(_owner.GetKind<T>(), entity, metadata);

        public ValueTask RetractAsync<T>(ReadOnlyMemory<byte> entityId, EntityMetadata? metadata) where T : struct, IConvergenceEntity<T>
            => _batch.RetractAsync(_owner.GetKind<T>(), entityId, metadata);

        public ValueTask FlushAsync(CancellationToken ct) => _owner._client!.FlushAsync(ct);

        public Task EpochBeginAsync(CancellationToken ct) => _owner._client!.EpochBeginAsync(ct);

        public Task EpochEndAsync(CancellationToken ct) => _owner._client!.EpochEndAsync(ct);
    }
}

// Seam over the steady-state ConvergeDB write path so ConvergeDbWriter's tick-flush gating,
// dirty-skip and transport-failure handling can be unit-tested against a fake. Production is
// ConvergeDbWriter.ConvergenceClientSink; the snapshot path keeps using ConvergeDbWriter.Batch()
// directly and is unaffected.
internal interface IConvergenceWriteSink
{
    ValueTask AssertAsync<T>(T entity, EntityMetadata? metadata) where T : struct, IConvergenceEntity<T>;
    ValueTask RetractAsync<T>(ReadOnlyMemory<byte> entityId, EntityMetadata? metadata) where T : struct, IConvergenceEntity<T>;
    ValueTask FlushAsync(CancellationToken ct);
    Task EpochBeginAsync(CancellationToken ct);
    Task EpochEndAsync(CancellationToken ct);
}

public class ConvergeDbOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3727;
}
