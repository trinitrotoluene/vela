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
using Vela.Events;
using Vela.Mappers;
using Vela.Services.Contracts;

public class EventSubscriberService : IEventSubscriber
{
  private readonly Counter<long> _eventCounter;
  private readonly ILogger<EventGatewayService> _logger;
  private readonly IDatabase _cache;
  private readonly List<object> _eventMappings;
  private readonly IOptions<BitcraftServiceOptions> _options;

  public EventSubscriberService(
    ILogger<EventGatewayService> logger,
    IConnectionMultiplexer multiplexer,
    IOptions<BitcraftServiceOptions> options,
    IMeterFactory metricsFactory,
    IMetricHelpers metricHelpers
  )
  {
    _logger = logger;
    _options = options;

    _cache = multiplexer.GetDatabase();
    _eventMappings = LoadMappings();
    var metrics = metricsFactory.Create("Vela", null, [
      new("service", metricHelpers.ServiceName)
    ]);

    _eventCounter = metrics.CreateCounter<long>("vela_event_published");
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

  public async Task PopulateBaseCachesAsync(DbConnection conn)
  {
    _logger.LogInformation("Clearing region-specific caches");

    var keys = _cache.Multiplexer.GetServers().FirstOrDefault()?
      .Keys(pattern: "*")
      .Select(x => x.ToString())
      .Where(x => x.EndsWith(_options.Value.Module));

    foreach (var regionKey in keys ?? [])
    {
      _logger.LogInformation("Clearing cached key: {key}", regionKey);
      await _cache.KeyDeleteAsync(regionKey);
    }

    _logger.LogInformation("Populating caches");

    var dbType = conn.Db.GetType();
    var handleFields = dbType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
      .Where(x =>
          x.FieldType.BaseType?.IsGenericType == true &&
          x.FieldType.BaseType.GetGenericTypeDefinition() == typeof(RemoteTableHandle<,>)
      );

    List<Task> populateTasks = [];

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

      var populateMethod = GetType()
            .GetMethod(nameof(PopulateBaseCacheForHandleAsync), BindingFlags.Instance | BindingFlags.NonPublic)?
            .MakeGenericMethod(rowType, outputType);

      var task = (Task?)populateMethod?.Invoke(this, [cacheKey, iterResult, mapper]);
      if (task != null)
        populateTasks.Add(task);
    }

    await Task.WhenAll(populateTasks);
  }

  private async Task PopulateBaseCacheForHandleAsync<TFrom, TTo>(
    string cacheKey,
    IEnumerable<TFrom> fromItems,
    MappedDbEntityBase<TFrom, TTo> mapper
  ) where TTo : BitcraftEventBase
  {
    await Task.Yield();

    List<HashEntry> hashEntries = [];

    foreach (var item in fromItems.ToArray())
    {
      var mappedItem = mapper.Map(item);
      mappedItem.Module = _options.Value.Module;

      var serializedItem = JsonSerializer.Serialize(mappedItem);

      hashEntries.Add(new HashEntry(mappedItem.Id, serializedItem));
    }

    _logger.LogInformation("Populating cache key {cacheKey} with {count} initial values", cacheKey, hashEntries.Count);
    await _cache.HashSetAsync(cacheKey, [.. hashEntries]);
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
        Entity: payload
      ));

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
      var callerIdentity = ctx.Event is Event<Reducer>.Reducer reducerCtx
        ? reducerCtx.ReducerEvent.CallerIdentity.ToString()
        : null!;

      var json = JsonSerializer.Serialize(new Envelope<T>
      (
        Version: EnvelopeVersion.V1,
        Module: _options.Value.Module,
        Entity: payload,
        CallerIdentity: callerIdentity
      ));
      var cacheJson = JsonSerializer.Serialize(payload);
      var cacheKey = BitcraftEventBase.CacheKey(payload, _options.Value.Module);

      if (delete)
      {
        _logger.LogDebug("Deleting {cacheJson} from {cacheKey}", cacheJson, cacheKey);
        _cache.HashDelete(cacheKey, payload.Id, CommandFlags.FireAndForget);
      }
      else
      {
        _logger.LogDebug("Caching {cacheJson} in {cacheKey}", cacheJson, cacheKey);
        _cache.HashSet(
          cacheKey,
          [new HashEntry(payload.Id, cacheJson)],
          CommandFlags.FireAndForget
        );
      }

      _logger.LogDebug("Publishing {json} to {topic}", json, topic);
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

      var callerIdentity = ctx.Event is Event<Reducer>.Reducer reducerCtx
        ? reducerCtx.ReducerEvent.CallerIdentity.ToString()
        : null!;

      var json = JsonSerializer.Serialize(new UpdateEnvelope<T>(
        Version: EnvelopeVersion.V1,
        Module: _options.Value.Module,
        CallerIdentity: callerIdentity,
        OldEntity: oldEntity,
        NewEntity: newEntity
      ));
      var cacheJson = JsonSerializer.Serialize(newEntity);
      var cacheKey = BitcraftEventBase.CacheKey(newEntity, _options.Value.Module);

      _logger.LogDebug("Caching {cacheJson} in {cacheKey}", cacheJson, cacheKey);
      _cache.HashSet(
        cacheKey,
        [new HashEntry(newEntity.Id, cacheJson)],
        CommandFlags.FireAndForget
      );

      _logger.LogDebug("Publishing {json} to {topic}", json, topic);
      _eventCounter.Add(1, new TagList { { "topic", topic } });
      _ = _cache.Publish(RedisChannel.Literal(topic), json, CommandFlags.FireAndForget);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error publishing to {topic}", topic);
    }
  }
}