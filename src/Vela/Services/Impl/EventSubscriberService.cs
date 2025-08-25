using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpacetimeDB;
using SpacetimeDB.Types;
using StackExchange.Redis;
using Vela.Events;
using Vela.Mappers;

public class EventSubscriberService : IEventSubscriber
{
  private readonly ILogger<EventGatewayService> _logger;
  private readonly IDatabase _cache;
  private readonly List<object> _eventMappings;
  private readonly IOptions<BitcraftServiceOptions> _options;

  public EventSubscriberService(ILogger<EventGatewayService> logger, IConnectionMultiplexer multiplexer, IOptions<BitcraftServiceOptions> options)
  {
    _logger = logger;
    _options = options;

    _cache = multiplexer.GetDatabase();
    _eventMappings = LoadMappings();
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
      var tableProperty = fields
          .FirstOrDefault(x => (x.FieldType.BaseType?.IsGenericType ?? false)
              && x.FieldType.BaseType?.GetGenericTypeDefinition() == typeof(RemoteTableHandle<,>)
              && x.FieldType.BaseType?.GenericTypeArguments[1] == entityType);

      if (tableProperty == null)
      {
        _logger.LogWarning("No table found for entity type {type}", entityType.Name);
        continue;
      }

      var table = tableProperty.GetValue(conn.Db) ?? throw new Exception("Unable to retrieve Db instance");

      var registerHandlersMethod = typeof(EventSubscriberService)
            .GetMethod(nameof(RegisterEventHandlers), BindingFlags.NonPublic | BindingFlags.Instance)
            !.MakeGenericMethod(entityType, mappedType);

      registerHandlersMethod?.Invoke(this, [table, mapping]);
    }

    _logger.LogInformation("Done setting up subscriptions");
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
      _logger.LogInformation("Subscribing to inserts on {table}", tableType.Name);

      var handlerDelegate = CreateCompatibleDelegate(insertEvent, (EventContext ctx, TEntity entity) =>
      {
        try
        {
          PublishEvent($"{mapper.TopicName}.insert", mapper.Map(entity));
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
      _logger.LogInformation("Subscribing to updates on {table}", tableType.Name);

      var handlerDelegate = CreateCompatibleDelegate(updateEvent, (EventContext ctx, TEntity oldEntity, TEntity newEntity) =>
      {
        try
        {
          PublishUpdateEvent($"{mapper.TopicName}.update", mapper.Map(oldEntity), mapper.Map(newEntity));
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
      _logger.LogInformation("Subscribing to deletes on {table}", tableType.Name);

      var handlerDelegate = CreateCompatibleDelegate(deleteEvent, (EventContext ctx, TEntity entity) =>
      {
        try
        {
          PublishEvent($"{mapper.TopicName}.delete", mapper.Map(entity));
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

  private void PublishEvent<T>(string topic, T payload, bool delete = false) where T : BitcraftEventBase
  {
    try
    {
      var json = JsonSerializer.Serialize(new Envelope<T>
      (
        Version: EnvelopeVersion.V1,
        Module: _options.Value.Module,
        Entity: payload
      ));
      var cacheJson = JsonSerializer.Serialize(payload);
      var cacheKey = $"cache:{typeof(T).Name}";

      if (delete)
      {
        _logger.LogInformation("Deleting {cacheJson} from {cacheKey}", cacheJson, cacheKey);
        _cache.HashDelete(cacheKey, payload.Id, CommandFlags.FireAndForget);
      }
      else
      {
        _logger.LogInformation("Caching {cacheJson} in {cacheKey}", cacheJson, cacheKey);
        _cache.HashSet(
          cacheKey,
          [new HashEntry(payload.Id, cacheJson)],
          CommandFlags.FireAndForget
        );
      }

      _logger.LogInformation("Publishing {json} to {topic}", json, topic);
      _ = _cache.Publish(RedisChannel.Literal(topic), json, CommandFlags.FireAndForget);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error publishing to {topic}", topic);
    }
  }

  public void PublishUpdateEvent<T>(string topic, T oldEntity, T newEntity) where T : BitcraftEventBase
  {
    try
    {
      var json = JsonSerializer.Serialize(new UpdateEnvelope<T>(
        Version: EnvelopeVersion.V1,
        Module: _options.Value.Module,
        OldEntity: oldEntity,
        NewEntity: newEntity
      ));
      var cacheJson = JsonSerializer.Serialize(newEntity);
      var cacheKey = $"cache:{typeof(T).Name}";
      _logger.LogInformation("Caching {cacheJson} in {cacheKey}", cacheJson, cacheKey);
      _cache.HashSet(
        cacheKey,
        [new HashEntry(newEntity.Id, cacheJson)],
        CommandFlags.FireAndForget
      );

      _logger.LogInformation("Publishing {json} to {topic}", json, topic);
      _ = _cache.Publish(RedisChannel.Literal(topic), json, CommandFlags.FireAndForget);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error publishing to {topic}", topic);
    }
  }
}