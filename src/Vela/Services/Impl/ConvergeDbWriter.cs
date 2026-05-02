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
    private ConvergenceClient? _client;
    private readonly ConcurrentDictionary<Type, object> _kindHandles = new();
    private int _activeEpochs;

    public ConvergeDbWriter(
        ILogger<ConvergeDbWriter> logger,
        IOptions<BitcraftServiceOptions> bitcraftOptions,
        IOptions<ConvergeDbOptions> options
    )
    {
        _logger = logger;
        _bitcraftOptions = bitcraftOptions;
        _options = options;
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

    public async Task AssertAsync<T>(T entity, EntityMetadata? metadata = null) where T : struct, IConvergenceEntity<T>
    {
        var handle = GetKind<T>();
        await handle.AssertAsync(entity, metadata);
    }

    public async Task RetractAsync<T>(ReadOnlyMemory<byte> entityId, EntityMetadata? metadata = null) where T : struct, IConvergenceEntity<T>
    {
        var handle = GetKind<T>();
        await handle.RetractAsync(entityId, metadata);
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
        await _client!.EpochBeginAsync(ct);
    }

    public async Task EndEpochAsync(CancellationToken ct = default)
    {
        try
        {
            await _client!.EpochEndAsync(ct);
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
}

public class ConvergeDbOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3727;
}
