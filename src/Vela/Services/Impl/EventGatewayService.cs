using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpacetimeDB.Types;
using Vela.Events;
using Vela.Services.Contracts;

public class EventGatewayService : BackgroundService
{
  private readonly IMetricHelpers _metrics;
  private readonly ILogger<EventGatewayService> _logger;
  private readonly IDbConnectionAccessor _accessor;
  private readonly IEventSubscriber _subscriber;
  private readonly IConvergeDbWriter _convergeWriter;
  private readonly IOptions<BitcraftServiceOptions> _options;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IHostApplicationLifetime _hostLifetime;

  public EventGatewayService(
    ILogger<EventGatewayService> logger,
    IDbConnectionAccessor accessor,
    IEventSubscriber subscriber,
    IConvergeDbWriter convergeWriter,
    IOptions<BitcraftServiceOptions> options,
    IMetricHelpers metrics,
    IHttpClientFactory httpClientFactory,
    IHostApplicationLifetime hostLifetime
  )
  {
    _logger = logger;
    _accessor = accessor;
    _subscriber = subscriber;
    _convergeWriter = convergeWriter;
    _options = options;
    _metrics = metrics;
    _httpClientFactory = httpClientFactory;
    _hostLifetime = hostLifetime;
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
        throw;
      }
    }
  }

  private void RunConnectionLifecycle(DbConnection conn, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Start connection lifecycle");

    // Register handlers BEFORE subscribing so OnInsert/OnUpdate fire into the snapshot buffer
    // as the cacheless SDK delivers initial-subscription rows via PostApply (no .Iter() cache).
    _subscriber.RegisterAllEventHandlers(conn);

    var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var batches = BuildSubscriptionBatches(nowUnixSeconds);
    var remaining = batches.Length;

    _logger.LogInformation("Subscribing in {BatchCount} batches ({Total} queries total)",
      batches.Length, batches.Sum(b => b.Length));

    for (var i = 0; i < batches.Length; i++)
    {
      var batchIndex = i;
      conn.SubscriptionBuilder()
        .OnApplied((ctx) =>
        {
          _logger.LogInformation("Subscription batch {Index}/{Total} applied ({QueryCount} queries)",
            batchIndex + 1, batches.Length, batches[batchIndex].Length);

          if (Interlocked.Decrement(ref remaining) == 0)
          {
            try
            {
              _logger.LogInformation("All subscription batches applied, initializing");
              var snapshot = _subscriber.DrainSnapshotBuffer();
              _ = Task.Run(() => HeartbeatAsync(conn, cancellationToken), cancellationToken);
              _ = Task.Run(() => OnBaseSubscriptionsApplied(snapshot));
            }
            catch (Exception ex)
            {
              _logger.LogError(ex, "Fatal error during subscription init - shutting down host for clean restart");
              _hostLifetime.StopApplication();
            }
          }
        })
        .OnError((err, ex) =>
        {
          _logger.LogError(ex, "Error applying subscription batch {Index}/{Total}",
            batchIndex + 1, batches.Length);
          conn.Disconnect();
        })
        .Subscribe(batches[batchIndex]);
    }
  }

  private static string[][] BuildSubscriptionBatches(long nowUnixSeconds)
  {
    return [
      [
        "SELECT * FROM item_desc",
        "SELECT * FROM cargo_desc",
        "SELECT * FROM crafting_recipe_desc",
        "SELECT * FROM item_list_desc",
        "SELECT * FROM building_desc",
        "SELECT * FROM claim_tech_desc",
        "SELECT * FROM paving_tile_desc",
        "SELECT * FROM user_state",
        "SELECT * FROM claim_local_state",
        "SELECT * FROM player_username_state",
        "SELECT * FROM buy_order_state",
        "SELECT * FROM sell_order_state",
        "SELECT * FROM closed_listing_state",
        "SELECT * FROM building_state",
        "SELECT * FROM claim_tech_state",
        "SELECT * FROM paved_tile_state",
                @"SELECT e.* FROM empire_state e
          JOIN claim_state c
            ON e.capital_building_entity_id = c.owner_building_entity_id",
        @"SELECT * FROM empire_node_state", // todo can this be restricted at all?
        @"SELECT e.* FROM empire_node_siege_state e
          JOIN building_state b
            ON e.building_entity_id = b.entity_id",
        @"SELECT t.* FROM claim_state t INNER JOIN claim_local_state l ON t.entity_id = l.entity_id
WHERE l.building_description_id = 405 OR l.building_description_id = 292245080",
        $"SELECT t.* FROM chat_message_state t WHERE t.channel_id >= 0 AND t.timestamp > {nowUnixSeconds}",
        @"SELECT s.* FROM progressive_action_state s
INNER JOIN public_progressive_action_state p
  ON s.entity_id = p.entity_id
WHERE s.craft_count > 50",
        @"SELECT p.* FROM public_progressive_action_state p
JOIN progressive_action_state s
ON p.entity_id = s.entity_id
  WHERE s.craft_count > 50",
        "SELECT i.* FROM inventory_state i INNER JOIN building_state b ON i.owner_entity_id = b.entity_id",
      ],
      [
        "SELECT ls.* FROM location_state ls INNER JOIN public_progressive_action_state ppas ON ppas.building_entity_id = ls.entity_id"
      ],
      [
        "SELECT ls.* FROM location_state ls INNER JOIN claim_state cs ON cs.owner_building_entity_id = ls.entity_id",
      ],
      [
        "SELECT ls.* FROM location_state ls INNER JOIN paved_tile_state pts ON pts.entity_id = ls.entity_id"
      ]
    ];
  }


  private async Task OnBaseSubscriptionsApplied(
    Dictionary<Type, List<BitcraftEventBase>> snapshot)
  {
    try
    {
      _logger.LogInformation("Base subscriptions applied");

      // Wrap dynamic entity population in a ConvergeDB Epoch.
      // The Epoch auto-retracts any entities from this source that are not re-asserted,
      // cleanly handling stale entity cleanup on reconnection.
      await _convergeWriter.EpochAsync(async () =>
      {
        await _subscriber.PopulateBaseCachesAsync(snapshot);
      });

      _logger.LogInformation("Epoch complete - ConvergeDB and PostgreSQL populated");
    }
    catch (Exception ex)
    {
      // Epoch failure means ConvergeDB state is in an undefined/abandoned state.
      // Stop the host so Docker restarts the container and a fresh epoch re-seeds cleanly.
      _logger.LogError(ex, "Exception thrown by subscriber init - shutting down host for clean restart");
      _hostLifetime.StopApplication();
    }
  }

  public override Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
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
