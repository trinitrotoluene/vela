using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpacetimeDB;
using SpacetimeDB.Types;
using StackExchange.Redis;
using Vela.Data;
using Vela.Events;
using Vela.Mappers;
using Vela.Services.Contracts;

public class EventSubscriberService : IEventSubscriber
{
  private static readonly SemaphoreSlim _populateSemaphore = new(3, 3);
  private readonly Counter<long> _eventCounter;
  private readonly Counter<long> _snapshotEntityCounter;
  private readonly Histogram<double> _snapshotDuration;
  private readonly ILogger<EventGatewayService> _logger;
  private readonly IDatabase _cache;
  private readonly IEntityDbWriter _dbWriter;
  private readonly List<object> _eventMappings;
  private readonly IOptions<BitcraftServiceOptions> _options;
  private readonly JsonSerializerOptions _jsonOptions;

  public EventSubscriberService(
    ILogger<EventGatewayService> logger,
    IConnectionMultiplexer multiplexer,
    IOptions<BitcraftServiceOptions> options,
    IMeterFactory metricsFactory,
    IMetricHelpers metricHelpers,
    IEntityDbWriter dbWriter,
    JsonSerializerOptions jsonOptions)
  {
    _logger = logger;
    _options = options;
    _dbWriter = dbWriter;

    _cache = multiplexer.GetDatabase();
    _eventMappings = LoadMappings();
    var metrics = metricsFactory.Create("Vela", null, [
      new("service", metricHelpers.ServiceName)
    ]);

    _eventCounter = metrics.CreateCounter<long>("vela_event_published");
    _snapshotEntityCounter = metrics.CreateCounter<long>("vela_snapshot_entities",
      description: "Total entities loaded during snapshot operations");
    _snapshotDuration = metrics.CreateHistogram<double>("vela_snapshot_duration_seconds",
      unit: "s", description: "Time taken to snapshot and populate base caches");
    _jsonOptions = jsonOptions;
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

  public void SubscribeToChanges(DbConnection conn)
  {
    _logger.LogInformation("Subscribing to all supported changes");

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
      var mappedType = mapperGenericArguments[1];
      var tableProperties = fields
          .Where(x => (x.FieldType.BaseType?.IsGenericType ?? false)
              && x.FieldType.BaseType?.GetGenericTypeDefinition() == typeof(RemoteTableHandle<,>)
              && x.FieldType.BaseType?.GenericTypeArguments[1] == entityType);


      // Some tables share a type e.g. AuctionListingState for buy and sell orders.
      // Typically, you can tell what the type is by inspecting the schema, so we can just register the mapper for both.
      // If this causes problems in the future, need to explore some kind of synthetic entity split e.g.
      // [SyntheticEntity("<TablePropertyName", "<SyntheticEntityName>")]
      // would then "split" the shared entity depending on which table it came from. Would be pain to implement though...
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

    _logger.LogInformation("Done setting up subscriptions");
  }

  public Dictionary<(Type OutputType, string CacheKey), List<BitcraftEventBase>> SnapshotBaseCaches(DbConnection conn)
  {
    _logger.LogInformation("Snapshotting table data");
    var sw = Stopwatch.StartNew();

    var merged = new Dictionary<(Type OutputType, string CacheKey), List<BitcraftEventBase>>();

    var dbType = conn.Db.GetType();
    var handleFields = dbType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
      .Where(x =>
          x.FieldType.BaseType?.IsGenericType == true &&
          x.FieldType.BaseType.GetGenericTypeDefinition() == typeof(RemoteTableHandle<,>)
      );

    foreach (var handleField in handleFields)
    {
      var handleInstance = handleField.GetValue(conn.Db);
      if (handleInstance == null)
        continue;

      var handlerBaseType = handleField.FieldType.BaseType;
      if (handlerBaseType == null || !handlerBaseType.IsGenericType)
        continue;

      var rowType = handlerBaseType.GetGenericArguments()[1];
      if (rowType == null)
        continue;

      var mapper = _eventMappings.FirstOrDefault(x => x.GetType().BaseType?.GetGenericArguments()[0] == rowType);
      if (mapper == null)
        continue;

      var outputType = mapper.GetType().BaseType!.GetGenericArguments()[1];
      var cacheKey = BitcraftEventBase.CacheKey(outputType, _options.Value.Module);

      var iterMethod = handleField.FieldType.GetMethod("Iter", BindingFlags.Instance | BindingFlags.Public);
      if (iterMethod == null)
      {
        _logger.LogWarning("No Iter() method found on handle field: {fieldName}", handleField.Name);
        continue;
      }

      var iterResult = iterMethod.Invoke(handleInstance, null);
      if (iterResult is not IEnumerable fromItems)
      {
        _logger.LogWarning("Iter() did not return an IEnumerable on: {fieldName}", handleField.Name);
        continue;
      }

      var mapMethod = GetType()
            .GetMethod(nameof(MapForHandle), BindingFlags.Instance | BindingFlags.NonPublic)?
            .MakeGenericMethod(rowType, outputType);

      if (mapMethod?.Invoke(this, [fromItems, mapper]) is not List<BitcraftEventBase> mappedEntities)
        continue;

      var key = (outputType, cacheKey);
      if (!merged.TryGetValue(key, out var list))
      {
        list = [];
        merged[key] = list;
      }
      list.AddRange(mappedEntities);
    }

    var totalEntities = merged.Values.Sum(l => l.Count);
    _snapshotEntityCounter.Add(totalEntities);
    _snapshotDuration.Record(sw.Elapsed.TotalSeconds);
    _logger.LogInformation("Snapshot complete: {Count} entities in {Elapsed:F2}s", totalEntities, sw.Elapsed.TotalSeconds);

    return merged;
  }

  public async Task PopulateBaseCachesAsync(
    Dictionary<(Type OutputType, string CacheKey), List<BitcraftEventBase>> merged)
  {
    _logger.LogInformation("Populating storage");

    var populateTasks = merged.Select(kvp =>
      PopulateMergedAsync(kvp.Key.CacheKey, kvp.Key.OutputType, kvp.Value));

    await Task.WhenAll(populateTasks);
  }

  private List<BitcraftEventBase> MapForHandle<TFrom, TTo>(
    IEnumerable<TFrom> fromItems,
    MappedDbEntityBase<TFrom, TTo> mapper
  ) where TTo : BitcraftEventBase
  {
    var mapped = new List<BitcraftEventBase>();
    foreach (var item in fromItems.ToArray())
    {
      var mappedItem = mapper.Map(item);
      mappedItem.Module = _options.Value.Module;
      mapped.Add(mappedItem);
    }
    return mapped;
  }

  private async Task PopulateMergedAsync(
    string cacheKey, Type outputType, List<BitcraftEventBase> entities)
  {
    await Task.Yield();

    var storageTarget = BitcraftEventBase.GetStorageTarget(outputType);

    if (storageTarget.HasFlag(StorageTarget.Cache))
    {
      var hashEntries = entities
        .Select(m => new HashEntry(m.Id, JsonSerializer.Serialize(m, m.GetType(), _jsonOptions)))
        .ToArray();

      _logger.LogInformation("Populating cache key {cacheKey} with {count} initial values", cacheKey, hashEntries.Length);

      // Capture existing fields before writing so concurrent additions aren't swept as stale
      var existingFields = !BitcraftEventBase.IsGlobalCacheKey(outputType)
        ? await _cache.HashKeysAsync(cacheKey)
        : null;

      await _cache.HashSetAsync(cacheKey, hashEntries);

      if (existingFields != null)
      {
        // Module-scoped keys are exclusive - remove stale entries left by a previous snapshot
        var validIds = entities.Select(m => (RedisValue)m.Id).ToHashSet();
        var staleFields = existingFields.Where(f => !validIds.Contains(f)).ToArray();
        if (staleFields.Length > 0)
        {
          _logger.LogInformation("Removing {count} stale entries from {cacheKey}", staleFields.Length, cacheKey);
          await _cache.HashDeleteAsync(cacheKey, staleFields);
        }
      }
    }

    if (storageTarget.HasFlag(StorageTarget.Database))
    {
      await _populateSemaphore.WaitAsync();
      try
      {
        _logger.LogInformation("Populating database for {type} with {count} initial values", outputType.Name, entities.Count);
        await _dbWriter.PopulateAsync(outputType, _options.Value.Module, entities);
      }
      finally
      {
        _populateSemaphore.Release();
      }
    }
  }

  private void RegisterEventHandlers<TEntity, TOutput>(
    object table,
    MappedDbEntityBase<TEntity, TOutput> mapper
) where TOutput : BitcraftEventBase
  {
    var tableType = table.GetType();
    var events = tableType.GetEvents();
    var insertEvent = tableType.GetEvent("OnInsert");
    var updateEvent = tableType.GetEvent("OnUpdate");
    var deleteEvent = tableType.GetEvent("OnDelete");

    if (insertEvent != null)
    {
      _logger.LogDebug("Subscribing to inserts on {table}", tableType.Name);
      var handlerDelegate = CreateCompatibleDelegate(insertEvent, (EventContext ctx, TEntity entity) =>
      {
        try
        {
          PublishEvent(ctx, $"{mapper.TopicName}.insert", mapper.Map(entity), delete: false);
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
          PublishUpdateEvent(
            ctx,
            $"{mapper.TopicName}.update",
            mapper.Map(oldEntity),
            mapper.Map(newEntity)
          );
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
          PublishEvent(ctx, $"{mapper.TopicName}.delete", mapper.Map(entity), delete: true);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error handling delete event");
        }
      });

      deleteEvent.AddEventHandler(table, handlerDelegate);
    }
  }

  private static Delegate CreateCompatibleDelegate(EventInfo eventInfo, Delegate handler)
  {
    var handlerType = eventInfo.EventHandlerType!;
    return Delegate.CreateDelegate(handlerType, handler.Target!, handler.Method);
  }

  public void PublishSystemEvent<TEvent>(TEvent payload) where TEvent : GenericEventBase
  {
    var topic = $"system.{typeof(TEvent).Name}";

    try
    {
      var json = JsonSerializer.Serialize(new Envelope<TEvent>
      (
        Version: EnvelopeVersion.V1,
        Module: _options.Value.Module,
        CallerIdentity: "SYSTEM",
        Reducer: "SYSTEM",
        Entity: payload
      ), _jsonOptions);

      _eventCounter.Add(1, new TagList { { "topic", topic } });
      _cache.Publish(RedisChannel.Literal(topic), json, CommandFlags.FireAndForget);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error publishing to {topic}", topic);
    }
  }

  private void PublishEvent<T>(
    EventContext ctx, string topic, T payload, bool delete
  ) where T : BitcraftEventBase
  {
    try
    {
      payload.Module = _options.Value.Module;

      string callerIdentity = "UNKNOWN";
      string reducer = "UNKNOWN";

      if (ctx.Event is Event<Reducer>.Reducer reducerCtx)
      {
        callerIdentity = reducerCtx.ReducerEvent.CallerIdentity.ToString();
        reducer = reducerCtx.ReducerEvent.Reducer.GetType().Name ?? "UNKNOWN";
      }

      var json = JsonSerializer.Serialize(new Envelope<T>
      (
        Version: EnvelopeVersion.V1,
        Module: _options.Value.Module,
        Entity: payload,
        CallerIdentity: callerIdentity,
        Reducer: reducer
      ), _jsonOptions);

      var storageTarget = BitcraftEventBase.GetStorageTarget(typeof(T));

      // Redis cache operations
      if (storageTarget.HasFlag(StorageTarget.Cache))
      {
        var cacheJson = JsonSerializer.Serialize(payload, _jsonOptions);
        var cacheKey = BitcraftEventBase.CacheKey(payload, _options.Value.Module);

        if (delete)
        {
          _logger.LogDebug("Deleting from cache {cacheKey}", cacheKey);
          _cache.HashDelete(cacheKey, payload.Id, CommandFlags.FireAndForget);
        }
        else
        {
          _logger.LogDebug("Caching in {cacheKey}", cacheKey);
          _cache.HashSet(
            cacheKey,
            [new HashEntry(payload.Id, cacheJson)],
            CommandFlags.FireAndForget
          );
        }
      }

      // Buffered database operations (coalesced and flushed periodically)
      if (storageTarget.HasFlag(StorageTarget.Database))
      {
        if (delete)
          _dbWriter.EnqueueDelete(payload);
        else
          _dbWriter.EnqueueUpsert(payload);
      }

      // Always publish to pub/sub
      _logger.LogDebug("Publishing to {topic}", topic);
      _eventCounter.Add(1, new TagList { { "topic", topic } });
      _ = _cache.Publish(RedisChannel.Literal(topic), json, CommandFlags.FireAndForget);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error publishing to {topic}", topic);
    }
  }

  public void PublishUpdateEvent<T>(
    EventContext ctx, string topic, T oldEntity, T newEntity
  ) where T : BitcraftEventBase
  {
    try
    {
      oldEntity.Module = _options.Value.Module;
      newEntity.Module = _options.Value.Module;

      string callerIdentity = "UNKNOWN";
      string reducer = "UNKNOWN";

      if (ctx.Event is Event<Reducer>.Reducer reducerCtx)
      {
        callerIdentity = reducerCtx.ReducerEvent.CallerIdentity.ToString();
        reducer = reducerCtx.ReducerEvent.Reducer.GetType().Name ?? "UNKNOWN";
      }

      var json = JsonSerializer.Serialize(new UpdateEnvelope<T>(
        Version: EnvelopeVersion.V1,
        Module: _options.Value.Module,
        CallerIdentity: callerIdentity,
        Reducer: reducer,
        OldEntity: oldEntity,
        NewEntity: newEntity
      ), _jsonOptions);

      var storageTarget = BitcraftEventBase.GetStorageTarget(typeof(T));

      // Redis cache operations
      if (storageTarget.HasFlag(StorageTarget.Cache))
      {
        var cacheJson = JsonSerializer.Serialize(newEntity, _jsonOptions);
        var cacheKey = BitcraftEventBase.CacheKey(newEntity, _options.Value.Module);

        _logger.LogDebug("Caching in {cacheKey}", cacheKey);
        _cache.HashSet(
          cacheKey,
          [new HashEntry(newEntity.Id, cacheJson)],
          CommandFlags.FireAndForget
        );
      }

      // Buffered database operations (coalesced and flushed periodically)
      if (storageTarget.HasFlag(StorageTarget.Database))
      {
        _dbWriter.EnqueueUpsert(newEntity);
      }

      // Always publish to pub/sub
      _logger.LogDebug("Publishing to {topic}", topic);
      _eventCounter.Add(1, new TagList { { "topic", topic } });
      _ = _cache.Publish(RedisChannel.Literal(topic), json, CommandFlags.FireAndForget);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error publishing to {topic}", topic);
    }
  }
}
