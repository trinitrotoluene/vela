using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Convergence.Client;
using Convergence.Client.Protocol;
using Microsoft.Extensions.Hosting;
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
    private readonly IHostApplicationLifetime _hostLifetime;
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
    // empty buffer). Effectively single-threaded — buffering happens inside conn.FrameTick() and
    // the flush immediately after, both on the connection-loop thread — but accessed via
    // Volatile/Interlocked so the test-and-clear is well defined regardless.
    private int _dirty;

    public ConvergeDbWriter(
        ILogger<ConvergeDbWriter> logger,
        IOptions<BitcraftServiceOptions> bitcraftOptions,
        IOptions<ConvergeDbOptions> options,
        IHostApplicationLifetime hostLifetime
    )
    {
        _logger = logger;
        _bitcraftOptions = bitcraftOptions;
        _options = options;
        _hostLifetime = hostLifetime;
    }

    // Test-only: inject a fake write sink instead of connecting to a real ConvergeDB server.
    // Skips InitializeAsync (so the option fields stay unused); all steady-state-flush logic runs
    // against the supplied sink.
    internal ConvergeDbWriter(
        ILogger<ConvergeDbWriter> logger,
        IHostApplicationLifetime hostLifetime,
        IConvergenceWriteSink sink
    )
    {
        _logger = logger;
        // Option fields are only read by InitializeAsync, which tests skip.
        _bitcraftOptions = null!;
        _options = null!;
        _hostLifetime = hostLifetime;
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
    // on the wire, not their content/order/identity — arrival order is preserved end to end.
    //
    // NOTE (converged state only): ConvergeDB's server-side accumulator coalesces writes to the
    // same entity within its ~20ms time-based window by last-writer-wins — field data, source bits
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
        // shared client buffer; a per-tick steady-state flush must not fire. Read across threads —
        // _activeEpochs is mutated on the lifecycle/finalize threads (Begin/EndEpochAsync).
        if (Volatile.Read(ref _activeEpochs) > 0)
            return;

        // Gate 2: nothing buffered since the last flush — skip the idle tick entirely.
        if (Interlocked.Exchange(ref _dirty, 0) == 0)
            return;

        try
        {
            await _sink!.FlushAsync(ct);
        }
        catch (Exception ex) when (IsConvergeDbTransportFailure(ex))
        {
            // A transport failure invalidates the source epoch; in-process recovery would leak
            // stale entities. Signal the host to shut down so Docker restarts the container and
            // re-runs the startup path (fresh-epoch re-seed). Buffered-but-unsent ops are lost,
            // which is fine — recovery re-seeds everything.
            _logger.LogError(ex, "ConvergeDB transport failure during flush - shutting down host for clean restart");
            _hostLifetime.StopApplication();
        }
    }

    public async Task EpochAsync(Func<Task> body)
    {
        await _client!.EpochAsync(body);
    }

    public async Task BeginEpochAsync(CancellationToken ct = default)
    {
        if (Interlocked.Increment(ref _activeEpochs) > 1)
        {
            Interlocked.Decrement(ref _activeEpochs);
            throw new InvalidOperationException("Cannot begin a ConvergeDB epoch while another is already open");
        }
        await _sink!.EpochBeginAsync(ct);
    }

    public async Task EndEpochAsync(CancellationToken ct = default)
    {
        try
        {
            await _sink!.EpochEndAsync(ct);
        }
        finally
        {
            Interlocked.Decrement(ref _activeEpochs);
        }
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

    // A ConvergeDB transport failure invalidates the source epoch - in-process recovery would
    // leak stale entities. Buffered writes never reach the wire (they only fill a List), so this
    // can now only surface from FlushPendingAsync; the detection lives here, with the writer that
    // owns ConvergeDB semantics, rather than in the event dispatch path.
    private static bool IsConvergeDbTransportFailure(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is ProtocolException or SocketException or IOException or ObjectDisposedException)
                return true;
            if (e.GetType().FullName?.StartsWith("Convergence.Client.", StringComparison.Ordinal) == true)
                return true;
        }
        return false;
    }

    // Production write sink backed by the real ConvergenceClient. Assert/Retract are routed
    // through a single long-lived ConvergenceBatch: although ConvergenceBatch is documented as a
    // scoped/await-using type, it holds no state of its own — it's a thin typed-encode shim over
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
