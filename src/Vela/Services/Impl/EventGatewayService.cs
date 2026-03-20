using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpacetimeDB.Types;
using Vela.Data;
using Vela.Events;
using Vela.Services.Contracts;

public class EventGatewayService : BackgroundService
{
  private readonly IMetricHelpers _metrics;
  private readonly ILogger<EventGatewayService> _logger;
  private readonly IDbConnectionAccessor _accessor;
  private readonly IEventSubscriber _subscriber;
  private readonly IEntityDbWriter _dbWriter;
  private readonly IOptions<BitcraftServiceOptions> _options;
  private readonly IHttpClientFactory _httpClientFactory;

  public EventGatewayService(
    ILogger<EventGatewayService> logger,
    IDbConnectionAccessor accessor,
    IEventSubscriber subscriber,
    IEntityDbWriter dbWriter,
    IOptions<BitcraftServiceOptions> options,
    IMetricHelpers metrics,
    IHttpClientFactory httpClientFactory
  )
  {
    _logger = logger;
    _accessor = accessor;
    _subscriber = subscriber;
    _dbWriter = dbWriter;
    _options = options;
    _metrics = metrics;
    _httpClientFactory = httpClientFactory;
  }

  protected override async Task ExecuteAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Starting event gateway, waiting for Bitcraft connection");
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        var conn = await _accessor.WaitForConnectionAsync(cancellationToken);
        if (conn == null)
        {
          _logger.LogWarning("Received null connection, retrying...");
          continue;
        }
        _logger.LogInformation("Acquired connection");

        RunConnectionLifecycle(conn, cancellationToken);

        while (conn != null && _accessor.TryGet(out var currentConn) && conn == currentConn)
        {
          await Task.Delay(1000, cancellationToken);
        }

        await _dbWriter.StopFlushLoopAsync();
        _logger.LogInformation("Connection lost, waiting for reconnection");
      }
      catch (OperationCanceledException)
      {
        _logger.LogInformation("Service shutdown requested");
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in event gateway loop {message}", ex.Message);
        break;
      }
    }
  }

  private void RunConnectionLifecycle(DbConnection conn, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Start connection lifecycle");

    var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var nowTimestamp = DateTimeOffset.UtcNow.ToString("o");

    // Configure base subscription
    conn.SubscriptionBuilder()
      .OnApplied((ctx) =>
      {
        try
        {
          // Runs inline on the SpacetimeDB callback thread (which blocks further
          // message processing) so tables are stable during iteration and change
          // handlers are registered before any updates can arrive.
          LogIdentity(ctx, conn);
          var snapshot = _subscriber.SnapshotBaseCaches(conn);
          _subscriber.SubscribeToChanges(conn);
          _dbWriter.StartFlushLoop();
          _ = Task.Run(() => HeartbeatAsync(conn, cancellationToken), cancellationToken);
          _ = Task.Run(() => OnBaseSubscriptionsApplied(snapshot));
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Fatal error during subscription init");
        }
      })
      .OnError(OnBaseSubscriptionsErrored)
      .Subscribe([
        "SELECT ls.* FROM location_state ls INNER JOIN public_progressive_action_state ppas ON ppas.building_entity_id = ls.entity_id",
        "SELECT * FROM item_desc",
        "SELECT * FROM cargo_desc",
        "SELECT * FROM crafting_recipe_desc",
        "SELECT * FROM item_list_desc",
        @"SELECT e.* FROM empire_state e
          JOIN claim_state c
            ON e.capital_building_entity_id = c.owner_building_entity_id",
        @"SELECT * FROM empire_node_state", // todo can this be restricted at all?
        @"SELECT e.* FROM empire_node_siege_state e
          JOIN building_state b
            ON e.building_entity_id = b.entity_id",
        "SELECT * FROM user_state",
        "SELECT * FROM building_desc",
        @"SELECT t.* FROM claim_state t INNER JOIN claim_local_state l ON t.entity_id = l.entity_id 
WHERE l.building_description_id = 405 OR l.building_description_id = 292245080",
        "SELECT * FROM claim_local_state",
        "SELECT * FROM player_username_state",
        $"SELECT t.* FROM chat_message_state t WHERE t.channel_id >= 0 AND t.timestamp > {nowUnixSeconds}",
        "SELECT * FROM buy_order_state",
        "SELECT * FROM sell_order_state",
        "SELECT * FROM closed_listing_state",
        "SELECT * FROM building_state",
        @"SELECT s.* FROM progressive_action_state s
INNER JOIN public_progressive_action_state p
  ON s.entity_id = p.entity_id
WHERE s.craft_count > 50",
        @"SELECT p.* FROM public_progressive_action_state p
JOIN progressive_action_state s
ON p.entity_id = s.entity_id
  WHERE s.craft_count > 50",
        "SELECT i.* FROM inventory_state i INNER JOIN building_state b ON i.owner_entity_id = b.entity_id",
        "SELECT ls.* FROM location_state ls INNER JOIN claim_state cs ON cs.owner_building_entity_id = ls.entity_id",
        "SELECT * FROM claim_tech_state",
        "SELECT * FROM claim_tech_desc"
      ]);
  }

  private void OnBaseSubscriptionsErrored(ErrorContext err, Exception ex)
  {
    _logger.LogError(ex, "Error applying base subscriptions");
  }

  private void LogIdentity(SubscriptionEventContext ctx, DbConnection conn)
  {
    var currentIdentity = ctx.Identity ?? new SpacetimeDB.Identity();
    var userState = conn.Db.UserState.Identity.Find(currentIdentity);
    if (userState != null)
    {
      var username = conn.Db.PlayerUsernameState.EntityId.Find(userState.EntityId);
      _logger.LogInformation(
        "Signed in with identity {identity} as {username} ({entityId})",
        currentIdentity,
        username?.Username,
        userState.EntityId
      );
    }
    else
    {
      _logger.LogInformation("Signed in with unknown credentials");
    }
  }

  private async Task OnBaseSubscriptionsApplied(
    Dictionary<(Type OutputType, string CacheKey), List<BitcraftEventBase>> snapshot)
  {
    try
    {
      _logger.LogInformation("Base subscriptions applied");
      await _subscriber.PopulateBaseCachesAsync(snapshot);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Exception thrown by subscriber init");
    }
  }

  public override async Task StopAsync(CancellationToken cancellationToken)
  {
    await _dbWriter.StopFlushLoopAsync();
  }

  private async Task HeartbeatAsync(DbConnection currentConn, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Starting heartbeat");

    var seq = 0;
    var interval = TimeSpan.FromSeconds(10);

    while (!cancellationToken.IsCancellationRequested && currentConn.IsActive)
    {
      try
      {
        _logger.LogInformation("Heartbeat {seq}", seq);
        _subscriber.PublishSystemEvent(new HeartbeatEvent(
          Application: _metrics.ServiceName,
          DateTime.UtcNow,
          Seq: seq++
        ));

        if (!string.IsNullOrEmpty(_options.Value.HeartbeatUrl))
          _ = PingHeartbeatUrlAsync();
      }
      catch (TaskCanceledException)
      {
        _logger.LogInformation("Heartbeat task cancelled");
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Exception thrown while publishing heartbeat");
      }

      try
      {
        await Task.Delay(interval, cancellationToken);
      }
      catch (TaskCanceledException)
      {
        _logger.LogInformation("Heartbeat delay cancelled");
      }
    }

    _logger.LogInformation("Heartbeat cancelled");
  }

  private async Task PingHeartbeatUrlAsync()
  {
    try
    {
      using var client = _httpClientFactory.CreateClient("Heartbeat");
      await client.GetAsync(_options.Value.HeartbeatUrl);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to ping heartbeat URL");
    }
  }
}