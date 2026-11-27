using System.Diagnostics;
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
  private readonly IFatalRestart _fatalRestart;

  public EventGatewayService(
    ILogger<EventGatewayService> logger,
    IDbConnectionAccessor accessor,
    IEventSubscriber subscriber,
    IConvergeDbWriter convergeWriter,
    IOptions<BitcraftServiceOptions> options,
    IMetricHelpers metrics,
    IHttpClientFactory httpClientFactory,
    IFatalRestart fatalRestart
  )
  {
    _logger = logger;
    _accessor = accessor;
    _subscriber = subscriber;
    _convergeWriter = convergeWriter;
    _options = options;
    _metrics = metrics;
    _httpClientFactory = httpClientFactory;
    _fatalRestart = fatalRestart;
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

        await RunConnectionLifecycle(conn, cancellationToken);

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
        // Unexpected lifecycle failure (subscription setup, subscriber wiring, etc.). Treat it as
        // fatal and restart through the one restart primitive rather than re-throwing - this keeps the
        // restart path and exit code consistent with ConvergeDB failures instead of depending on the
        // host's unhandled-exception (StopHost) behaviour, whose exit code is harder to reason about.
        _logger.LogError(ex, "Error in event gateway loop");
        _fatalRestart.Trigger($"event gateway loop failed: {ex.Message}");
        break;
      }
    }
  }

  private async Task RunConnectionLifecycle(DbConnection conn, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Start connection lifecycle");

    // Register handlers BEFORE subscribing so OnInsert/OnUpdate fire as the cacheless SDK
    // delivers initial-subscription rows via PostApply (no .Iter() cache).
    _subscriber.RegisterAllEventHandlers(conn);

    // Open the epoch + streaming snapshot BEFORE the first Subscribe so OnInsert handlers
    // can stream-write directly into the open ConvergenceBatch. The epoch closes on the last
    // query's OnApplied; ConvergeDB retracts anything in the prior baseline that wasn't
    // re-asserted during streaming.
    await _convergeWriter.BeginEpochAsync(cancellationToken);
    _subscriber.BeginStreamingSnapshot();

    var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var queries = BuildSubscriptionQueries(nowUnixSeconds);

    _logger.LogInformation("Subscribing to {Total} queries, one at a time", queries.Length);
    SubscribeNext(conn, queries, 0, cancellationToken);
  }

  // One query per subscribe call, each sent when the one before it has applied. The server checks
  // its row limit against the summed estimate of every query in a call, so queries sent together
  // can be refused where each alone is accepted. Sending them in turn also means the last
  // "Subscribing" line names the query at fault when the server refuses one or never answers.
  private void SubscribeNext(DbConnection conn, string[] queries, int index, CancellationToken cancellationToken)
  {
    var query = SingleLine(queries[index]);
    var started = Stopwatch.GetTimestamp();
    _logger.LogInformation("Subscribing {Index}/{Total}: {Query}", index + 1, queries.Length, query);

    conn.SubscriptionBuilder()
      .OnApplied((ctx) =>
      {
        _logger.LogInformation("Subscription {Index}/{Total} applied in {ElapsedMs}ms: {Query}",
          index + 1, queries.Length, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, query);

        if (index + 1 < queries.Length)
        {
          SubscribeNext(conn, queries, index + 1, cancellationToken);
          return;
        }

        // OnApplied is a synchronous SDK callback on the FrameTick thread. Hand the async
        // finalize work off to a Task so we don't block FrameTick - and so any awaitable
        // exceptions surface cleanly.
        _ = Task.Run(() => FinalizeSnapshotAsync(conn, cancellationToken));
      })
      .OnError((err, ex) =>
      {
        _logger.LogError(ex, "Subscription {Index}/{Total} refused after {ElapsedMs}ms: {Query}",
          index + 1, queries.Length, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, query);
        // Drop the connection - process restart will abandon the open epoch on the server
        // and re-seed from a fresh epoch on next startup.
        conn.Disconnect();
      })
      .Subscribe([queries[index]]);
  }

  private static string SingleLine(string query) => string.Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

  private async Task FinalizeSnapshotAsync(DbConnection conn, CancellationToken cancellationToken)
  {
    try
    {
      _logger.LogInformation("All subscriptions applied - finalizing streaming snapshot");
      await _subscriber.EndStreamingSnapshotAsync();
      await _convergeWriter.EndEpochAsync(cancellationToken);
      _logger.LogInformation("Epoch complete - ConvergeDB and PostgreSQL populated");
      _ = Task.Run(() => HeartbeatAsync(conn, cancellationToken), cancellationToken);
    }
    catch (Exception ex)
    {
      // Safety net: ConvergeDB I/O failures here already restart via the writer (EndEpochAsync). This
      // catches anything else in the finalize path (e.g. the subscriber's snapshot teardown) and
      // restarts through the same primitive so the snapshot can re-seed cleanly.
      _logger.LogError(ex, "Fatal error finalizing snapshot epoch");
      _fatalRestart.Trigger($"snapshot epoch finalize failed: {ex.Message}");
    }
  }

  internal const string OwnerLocationsQuery =
    "SELECT l.* FROM inventory_state i JOIN location_state l ON l.entity_id = i.owner_entity_id";

  // Location joins name the small table first. A join with no WHERE reads every row of its FROM
  // table and probes the other side by index, so written the other way round these would read
  // all of location_state (millions of rows per region) each time the subscription is applied.
  internal const string PublicCraftLocationsQuery =
    "SELECT ls.* FROM public_progressive_action_state ppas JOIN location_state ls ON ls.entity_id = ppas.building_entity_id";

  internal const string TownHallLocationsQuery =
    "SELECT ls.* FROM claim_state cs JOIN location_state ls ON ls.entity_id = cs.owner_building_entity_id";

  internal static string[] BuildSubscriptionQueries(long nowUnixSeconds)
  {
    return [
      "SELECT * FROM item_desc",
      "SELECT * FROM cargo_desc",
      "SELECT * FROM crafting_recipe_desc",
      "SELECT * FROM item_list_desc",
      "SELECT * FROM building_desc",
      "SELECT * FROM claim_tech_desc",
      "SELECT * FROM paving_tile_desc",
      "SELECT * FROM skill_desc",
      "SELECT * FROM tool_type_desc",
      "SELECT * FROM tool_desc",
      "SELECT * FROM building_type_desc",
      "SELECT * FROM extraction_recipe_desc",
      "SELECT * FROM secondary_knowledge_desc",
      "SELECT * FROM resource_desc",
      "SELECT * FROM deployable_desc",
      "SELECT * FROM user_state",
      "SELECT * FROM claim_local_state",
      "SELECT * FROM player_username_state",
      "SELECT * FROM buy_order_state",
      "SELECT * FROM sell_order_state",
      "SELECT * FROM closed_listing_state",
      "SELECT * FROM building_state",
      "SELECT * FROM building_nickname_state",
      "SELECT * FROM claim_tech_state",
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
      // Every inventory: players' (main, toolbelt, wallet), banks, chests and buildings.
      "SELECT * FROM inventory_state",
      // Where each inventory's owner stands, to place inventories that carry no player id
      // (housing chests: owner -> location -> dimension -> network -> player_housing_state).
      // Reads inventory_state and looks location_state up by primary key, never scanning it.
      // Subscribed within the same epoch, which retracts the locations of owners that are gone. A row also
      // returned by another location_state join is asserted once: the cacheless SDK counts
      // subscriptions per key and fires the delete only when the last one drops it.
      OwnerLocationsQuery,
      // Barter stalls: one row per stall. Their orders and locations are joined from the stall
      // side, so the server looks each up by index (trade_order_state.shop_entity_id,
      // location_state's primary key) instead of reading those tables whole.
      "SELECT * FROM barter_stall_state",
      "SELECT t.* FROM barter_stall_state b JOIN trade_order_state t ON t.shop_entity_id = b.entity_id",
      // Interiors, to tell which building (or whose housing) a stall stands in.
      "SELECT * FROM dimension_description_state",
      "SELECT * FROM dimension_network_state",
      "SELECT * FROM player_housing_state",
      "SELECT ls.* FROM barter_stall_state b JOIN location_state ls ON ls.entity_id = b.entity_id",
      PublicCraftLocationsQuery,
      TownHallLocationsQuery,
      // What players hold outside inventories: worn equipment (one row per player), equipment
      // presets (a few per player) and deployables, whose inventories are owned by the
      // deployable. Small tables that change rarely, so they are read whole.
      "SELECT * FROM equipment_state",
      "SELECT * FROM equipment_preset_state",
      "SELECT * FROM deployable_state_v2",
      // Who belongs to which claim: a row per membership. Small next to the large per-region tables, so read whole.
      "SELECT * FROM claim_member_state",
      // Event tables
      "SELECT * FROM market_trade_event",
      "SELECT * FROM barter_stall_inventory_event",
    ];
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
