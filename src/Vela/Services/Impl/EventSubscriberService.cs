using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;
using System.Reflection;
using Convergence.Client;
using Convergence.Client.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpacetimeDB;
using SpacetimeDB.Types;
using Vela.Contracts.Entities;
using Vela.Entities;
using Vela.Events;
using Vela.Mappers;
using Vela.Services.Contracts;
using Vela.Services.Impl;

public class EventSubscriberService : IEventSubscriber
{
  private readonly Counter<long> _eventCounter;
  private readonly Counter<long> _snapshotEntityCounter;
  private readonly Histogram<double> _snapshotDuration;
  private readonly ILogger<EventGatewayService> _logger;
  private readonly IConvergeDbWriter _convergeWriter;
  private readonly IDescriptorDbWriter _descriptorWriter;
  private readonly IHostApplicationLifetime _hostLifetime;
  private readonly List<object> _eventMappings;
  private readonly IOptions<BitcraftServiceOptions> _options;
  private readonly Dictionary<Type, Func<BitcraftEventBase, string, EntityMetadata?, Task>> _assertDispatchers;
  private readonly Dictionary<Type, Func<BitcraftEventBase, string, EntityMetadata?, Task>> _retractDispatchers;
  private readonly Dictionary<Type, Func<ConvergenceBatch, BitcraftEventBase, string, Task>> _batchAssertDispatchers;

  private readonly object _snapshotLock = new();
  private bool _snapshotComplete = false;
  private Dictionary<Type, Dictionary<string, BitcraftEventBase>> _snapshotBuffer = new();
  private Stopwatch? _snapshotStopwatch;

  public EventSubscriberService(
    ILogger<EventGatewayService> logger,
    IOptions<BitcraftServiceOptions> options,
    IMeterFactory metricsFactory,
    IMetricHelpers metricHelpers,
    IConvergeDbWriter convergeWriter,
    IDescriptorDbWriter descriptorWriter,
    IHostApplicationLifetime hostLifetime)
  {
    _logger = logger;
    _options = options;
    _convergeWriter = convergeWriter;
    _descriptorWriter = descriptorWriter;
    _hostLifetime = hostLifetime;

    _eventMappings = LoadMappings();
    var metrics = metricsFactory.Create("Vela", null, [
      new("service", metricHelpers.ServiceName)
    ]);

    _eventCounter = metrics.CreateCounter<long>("vela_event_published");
    _snapshotEntityCounter = metrics.CreateCounter<long>("vela_snapshot_entities",
      description: "Total entities loaded during snapshot operations");
    _snapshotDuration = metrics.CreateHistogram<double>("vela_snapshot_duration_seconds",
      unit: "s", description: "Time taken to snapshot and populate base caches");

    _assertDispatchers = BuildAssertDispatchers();
    _retractDispatchers = BuildRetractDispatchers();
    _batchAssertDispatchers = BuildBatchAssertDispatchers();

    ValidateConvergeDbDispatcherCoverage();
  }

  private void ValidateConvergeDbDispatcherCoverage()
  {
    var missing = new List<string>();
    foreach (var type in BitcraftEventBase.ConvergeDbTypes)
    {
      var gaps = new List<string>();
      if (!_assertDispatchers.ContainsKey(type)) gaps.Add("assert");
      if (!_retractDispatchers.ContainsKey(type)) gaps.Add("retract");
      if (!_batchAssertDispatchers.ContainsKey(type)) gaps.Add("batch-assert");
      if (gaps.Count > 0)
        missing.Add($"{type.Name} missing: {string.Join(", ", gaps)}");
    }

    if (missing.Count > 0)
      throw new InvalidOperationException(
        "EventSubscriberService dispatcher coverage gap — every [ConvergeDb] type must be wired into all three dispatcher maps:\n  "
        + string.Join("\n  ", missing));
  }

  private Dictionary<Type, Func<BitcraftEventBase, string, EntityMetadata?, Task>> BuildAssertDispatchers()
  {
    return new Dictionary<Type, Func<BitcraftEventBase, string, EntityMetadata?, Task>>
    {
      [typeof(BitcraftBuildingState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftBuildingState)e, m), md),
      [typeof(BitcraftClaimState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftClaimState)e, m), md),
      [typeof(BitcraftClaimLocalState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftClaimLocalState)e, m), md),
      [typeof(BitcraftClaimTechState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftClaimTechState)e, m), md),
      [typeof(BitcraftEmpireState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftEmpireState)e, m), md),
      [typeof(BitcraftEmpireNodeState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftEmpireNodeState)e, m), md),
      [typeof(BitcraftEmpireNodeSiegeState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftEmpireNodeSiegeState)e, m), md),
      [typeof(BitcraftUserState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftUserState)e, m), md),
      [typeof(BitcraftUsernameState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftUsernameState)e, m), md),
      [typeof(BitcraftLocationState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftLocationState)e, m), md),
      [typeof(BitcraftProgressiveAction)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftProgressiveAction)e, m), md),
      [typeof(BitcraftPublicProgressiveAction)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftPublicProgressiveAction)e, m), md),
      [typeof(BitcraftPavedTileState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftPavedTileState)e, m), md),
      [typeof(BitcraftChatMessage)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftChatMessage)e, m), md),
      [typeof(BitcraftActionLogState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftActionLogState)e, m), md),
      [typeof(BitcraftAuctionListingState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftAuctionListingState)e, m), md),
      [typeof(BitcraftClosedListingState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftClosedListingState)e, m), md),
      [typeof(BitcraftInventoryState)] = async (e, m, md) =>
        await _convergeWriter.AssertAsync(ConvergeDbConverters.ToConverge((BitcraftInventoryState)e, m), md),
    };
  }

  private Dictionary<Type, Func<BitcraftEventBase, string, EntityMetadata?, Task>> BuildRetractDispatchers()
  {
    // Build retract dispatchers using the same ID construction logic as the converters
    return new Dictionary<Type, Func<BitcraftEventBase, string, EntityMetadata?, Task>>
    {
      [typeof(BitcraftBuildingState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeBuildingState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftClaimState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeClaimState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftClaimLocalState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeClaimLocalState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftClaimTechState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeClaimTechState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftEmpireState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeEmpireState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftEmpireNodeState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeEmpireNodeState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftEmpireNodeSiegeState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeEmpireNodeSiegeState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftUserState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeUserState>((ReadOnlyMemory<byte>)Convert.FromHexString(e.Id), md),
      [typeof(BitcraftUsernameState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeUsernameState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftLocationState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeLocationState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftProgressiveAction)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeProgressiveAction>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftPublicProgressiveAction)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergePublicProgressiveAction>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftPavedTileState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergePavedTileState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftChatMessage)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeChatMessage>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftActionLogState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeActionLogState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftAuctionListingState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeAuctionListingState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftClosedListingState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeClosedListingState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
      [typeof(BitcraftInventoryState)] = async (e, m, md) =>
        await _convergeWriter.RetractAsync<ConvergeInventoryState>(Convergence.Client.EntityId.FromULong(ulong.Parse(e.Id)), md),
    };
  }

  private Dictionary<Type, Func<ConvergenceBatch, BitcraftEventBase, string, Task>> BuildBatchAssertDispatchers()
  {
    return new Dictionary<Type, Func<ConvergenceBatch, BitcraftEventBase, string, Task>>
    {
      [typeof(BitcraftBuildingState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeBuildingState>(), ConvergeDbConverters.ToConverge((BitcraftBuildingState)e, m)),
      [typeof(BitcraftClaimState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeClaimState>(), ConvergeDbConverters.ToConverge((BitcraftClaimState)e, m)),
      [typeof(BitcraftClaimLocalState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeClaimLocalState>(), ConvergeDbConverters.ToConverge((BitcraftClaimLocalState)e, m)),
      [typeof(BitcraftClaimTechState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeClaimTechState>(), ConvergeDbConverters.ToConverge((BitcraftClaimTechState)e, m)),
      [typeof(BitcraftEmpireState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeEmpireState>(), ConvergeDbConverters.ToConverge((BitcraftEmpireState)e, m)),
      [typeof(BitcraftEmpireNodeState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeEmpireNodeState>(), ConvergeDbConverters.ToConverge((BitcraftEmpireNodeState)e, m)),
      [typeof(BitcraftEmpireNodeSiegeState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeEmpireNodeSiegeState>(), ConvergeDbConverters.ToConverge((BitcraftEmpireNodeSiegeState)e, m)),
      [typeof(BitcraftUserState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeUserState>(), ConvergeDbConverters.ToConverge((BitcraftUserState)e, m)),
      [typeof(BitcraftUsernameState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeUsernameState>(), ConvergeDbConverters.ToConverge((BitcraftUsernameState)e, m)),
      [typeof(BitcraftLocationState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeLocationState>(), ConvergeDbConverters.ToConverge((BitcraftLocationState)e, m)),
      [typeof(BitcraftProgressiveAction)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeProgressiveAction>(), ConvergeDbConverters.ToConverge((BitcraftProgressiveAction)e, m)),
      [typeof(BitcraftPublicProgressiveAction)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergePublicProgressiveAction>(), ConvergeDbConverters.ToConverge((BitcraftPublicProgressiveAction)e, m)),
      [typeof(BitcraftPavedTileState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergePavedTileState>(), ConvergeDbConverters.ToConverge((BitcraftPavedTileState)e, m)),
      [typeof(BitcraftChatMessage)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeChatMessage>(), ConvergeDbConverters.ToConverge((BitcraftChatMessage)e, m)),
      [typeof(BitcraftActionLogState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeActionLogState>(), ConvergeDbConverters.ToConverge((BitcraftActionLogState)e, m)),
      [typeof(BitcraftAuctionListingState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeAuctionListingState>(), ConvergeDbConverters.ToConverge((BitcraftAuctionListingState)e, m)),
      [typeof(BitcraftClosedListingState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeClosedListingState>(), ConvergeDbConverters.ToConverge((BitcraftClosedListingState)e, m)),
      [typeof(BitcraftInventoryState)] = async (batch, e, m) =>
        await batch.AssertAsync(_convergeWriter.GetKind<ConvergeInventoryState>(), ConvergeDbConverters.ToConverge((BitcraftInventoryState)e, m)),
    };
  }

  private List<object> LoadMappings()
  {
    var mappings = new List<object>();
    var baseType = typeof(MappedDbEntityBase<,>);

    var assembly = Assembly.GetExecutingAssembly();
    var implementations = assembly.GetTypes()
        .Where(t => !t.IsAbstract
          && t.BaseType != null
          && t.BaseType.IsGenericType
          && t.BaseType.GetGenericTypeDefinition() == baseType
        )
        .ToArray();

    _logger.LogInformation("Found {count} mapping implementations", implementations.Length);

    foreach (var implementation in implementations)
    {
      var instance = Activator.CreateInstance(implementation);
      if (instance != null)
      {
        mappings.Add(instance);
        _logger.LogInformation("Loaded mapping: {type}", implementation.Name);
      }
    }

    return mappings;
  }

  public void RegisterAllEventHandlers(DbConnection conn)
  {
    _logger.LogInformation("Registering OnInsert/OnUpdate/OnDelete handlers for all mapped tables");

    var fields = conn.Db.GetType().GetFields();

    foreach (var mapping in _eventMappings)
    {
      _logger.LogInformation("Configuring subscribers for mapping {mapping}", mapping.GetType().Name);

      var mappingType = mapping.GetType();
      var baseType = mappingType.BaseType;

      if (baseType == null || !baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(MappedDbEntityBase<,>))
      {
        _logger.LogWarning("Mapping {type} does not inherit from MappedDbEntityBase", mappingType.Name);
        continue;
      }

      var mapperGenericArguments = baseType.GetGenericArguments();
      var entityType = mapperGenericArguments[0];
      var tableProperties = fields
          .Where(x => (x.FieldType.BaseType?.IsGenericType ?? false)
              && x.FieldType.BaseType?.GetGenericTypeDefinition() == typeof(RemoteTableHandle<,>)
              && x.FieldType.BaseType?.GenericTypeArguments[1] == entityType);

      var mappedType = mapperGenericArguments[1];

      foreach (var tableProperty in tableProperties)
      {
        _logger.LogInformation("Mapping {tableName}", tableProperty.Name);

        var table = tableProperty.GetValue(conn.Db) ?? throw new Exception("Unable to retrieve Db instance");

        var registerHandlersMethod = typeof(EventSubscriberService)
              .GetMethod(nameof(RegisterEventHandlers), BindingFlags.NonPublic | BindingFlags.Instance)
              !.MakeGenericMethod(entityType, mappedType);

        registerHandlersMethod?.Invoke(this, [table, mapping]);
      }
    }

    _logger.LogInformation("Done registering event handlers");
  }

  public Dictionary<Type, List<BitcraftEventBase>> DrainSnapshotBuffer()
  {
    Dictionary<Type, Dictionary<string, BitcraftEventBase>> taken;
    TimeSpan elapsed;
    lock (_snapshotLock)
    {
      taken = _snapshotBuffer;
      _snapshotBuffer = new();
      _snapshotComplete = true;
      elapsed = _snapshotStopwatch?.Elapsed ?? TimeSpan.Zero;
      _snapshotStopwatch = null;
    }

    var merged = new Dictionary<Type, List<BitcraftEventBase>>(taken.Count);
    foreach (var kvp in taken)
    {
      merged[kvp.Key] = [.. kvp.Value.Values];
    }

    var totalEntities = merged.Values.Sum(l => l.Count);
    _snapshotEntityCounter.Add(totalEntities);
    _snapshotDuration.Record(elapsed.TotalSeconds);
    _logger.LogInformation("Snapshot complete: {Count} entities in {Elapsed:F2}s", totalEntities, elapsed.TotalSeconds);

    return merged;
  }

  public async Task PopulateBaseCachesAsync(
    Dictionary<Type, List<BitcraftEventBase>> merged)
  {
    _logger.LogInformation("Populating storage");

    var module = _options.Value.Module;

    await using var batch = _convergeWriter.Batch();
    foreach (var kvp in merged)
    {
      var outputType = kvp.Key;
      var entities = kvp.Value;

      if (BitcraftEventBase.PostgresTypes.Contains(outputType))
      {
        // Descriptor entities → PostgreSQL
        _logger.LogInformation("Populating PostgreSQL for {Type} with {Count} descriptors", outputType.Name, entities.Count);
        await _descriptorWriter.PopulateAsync(outputType, entities);
      }
      else if (_batchAssertDispatchers.ContainsKey(outputType))
      {
        // Dynamic entities → ConvergeDB (batched within Epoch by EventGatewayService)
        foreach (var entity in entities)
        {
          entity.Module = module;
          await _batchAssertDispatchers[outputType](batch, entity, module);
        }
        _logger.LogInformation("Asserted {Count} {Type} entities to ConvergeDB (batched)", entities.Count, outputType.Name);
      }
    }
    // batch auto-flushes on DisposeAsync
  }

  private void RegisterEventHandlers<TEntity, TOutput>(
    object table,
    MappedDbEntityBase<TEntity, TOutput> mapper
  ) where TOutput : BitcraftEventBase
  {
    var tableType = table.GetType();
    var insertEvent = tableType.GetEvent("OnInsert");
    var updateEvent = tableType.GetEvent("OnUpdate");
    var deleteEvent = tableType.GetEvent("OnDelete");

    var bufferKey = typeof(TOutput);

    if (insertEvent != null)
    {
      _logger.LogDebug("Subscribing to inserts on {table}", tableType.Name);
      var handlerDelegate = CreateCompatibleDelegate(insertEvent, (EventContext ctx, TEntity entity) =>
      {
        try
        {
          var mapped = mapper.Map(entity);
          mapped.Module = _options.Value.Module;
          if (TryBufferSnapshotRow(ctx, bufferKey, mapped)) return;
          PublishEvent(ctx, mapped, delete: false);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error handling insert event");
        }
      });

      insertEvent.AddEventHandler(table, handlerDelegate);
    }

    if (updateEvent != null)
    {
      _logger.LogDebug("Subscribing to updates on {table}", tableType.Name);

      var handlerDelegate = CreateCompatibleDelegate(updateEvent, (EventContext ctx, TEntity oldEntity, TEntity newEntity) =>
      {
        try
        {
          var mapped = mapper.Map(newEntity);
          mapped.Module = _options.Value.Module;
          if (TryBufferSnapshotRow(ctx, bufferKey, mapped)) return;
          PublishEvent(ctx, mapped, delete: false);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error handling update event");
        }
      });

      updateEvent.AddEventHandler(table, handlerDelegate);
    }

    if (deleteEvent != null)
    {
      _logger.LogDebug("Subscribing to deletes on {table}", tableType.Name);

      var handlerDelegate = CreateCompatibleDelegate(deleteEvent, (EventContext ctx, TEntity entity) =>
      {
        try
        {
          // Deletes during the SubscribeApplied window only happen via overlapping-subscription
          // multiplicity transitions; the post-batch snapshot already reflects the desired state.
          if (ctx.Event is Event<Reducer>.SubscribeApplied) return;
          PublishEvent(ctx, mapper.Map(entity), delete: true);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error handling delete event");
        }
      });

      deleteEvent.AddEventHandler(table, handlerDelegate);
    }
  }

  private bool TryBufferSnapshotRow(
    EventContext ctx,
    Type bufferKey,
    BitcraftEventBase mapped)
  {
    if (ctx.Event is not Event<Reducer>.SubscribeApplied) return false;

    lock (_snapshotLock)
    {
      if (_snapshotComplete) return false;

      _snapshotStopwatch ??= Stopwatch.StartNew();

      if (!_snapshotBuffer.TryGetValue(bufferKey, out var byId))
      {
        byId = new Dictionary<string, BitcraftEventBase>();
        _snapshotBuffer[bufferKey] = byId;
      }
      byId[mapped.Id] = mapped;
    }
    return true;
  }

  private static Delegate CreateCompatibleDelegate(EventInfo eventInfo, Delegate handler)
  {
    var handlerType = eventInfo.EventHandlerType!;
    return Delegate.CreateDelegate(handlerType, handler.Target!, handler.Method);
  }

  public void PublishSystemEvent<TEvent>(TEvent payload) where TEvent : GenericEventBase
  {
    // System events (heartbeats) are no longer published to Redis.
    // They can be handled via ConvergeDB or a separate mechanism if needed.
    _logger.LogDebug("System event: {Type}", typeof(TEvent).Name);
  }

  private void PublishEvent<T>(
    EventContext ctx, T payload, bool delete
  ) where T : BitcraftEventBase
  {
    try
    {
      var module = _options.Value.Module;
      payload.Module = module;
      var entityType = typeof(T);

      _eventCounter.Add(1, new TagList { { "type", entityType.Name }, { "delete", delete } });

      EntityMetadata? metadata = null;
      if (ctx.Event is Event<Reducer>.Reducer reducerCtx)
      {
        metadata = new EntityMetadata(
          ("ci", reducerCtx.ReducerEvent.CallerIdentity.ToString()),
          ("rd", reducerCtx.ReducerEvent.Reducer.GetType().Name ?? "UNKNOWN")
        );
      }

      if (BitcraftEventBase.PostgresTypes.Contains(entityType))
      {
        // Descriptor → PostgreSQL
        if (delete)
          _descriptorWriter.EnqueueDelete(payload);
        else
          _descriptorWriter.EnqueueUpsert(payload);
      }
      else if (delete && _retractDispatchers.TryGetValue(entityType, out var retract))
      {
        // Dynamic entity delete → ConvergeDB RETRACT
        retract(payload, module, metadata).GetAwaiter().GetResult();
      }
      else if (!delete && _assertDispatchers.TryGetValue(entityType, out var assert))
      {
        // Dynamic entity insert/update → ConvergeDB ASSERT
        assert(payload, module, metadata).GetAwaiter().GetResult();
      }
      else
      {
        _logger.LogWarning("No dispatch handler for entity type {Type}", entityType.Name);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error publishing event for {Type}", typeof(T).Name);
      if (IsConvergeDbTransportFailure(ex))
      {
        _logger.LogError("ConvergeDB transport failure detected - shutting down host for clean restart");
        _hostLifetime.StopApplication();
      }
    }
  }

  // A ConvergeDB transport failure invalidates the source epoch - in-process recovery
  // would leak stale entities. Signal the host to shut down so Docker restarts the
  // container and re-runs the startup path (including the initial EpochAsync re-seed).
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
}
