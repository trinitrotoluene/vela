using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpacetimeDB;
using SpacetimeDB.Types;
using StackExchange.Redis;
using Vela.Events;

public class EventGatewayService : BackgroundService
{
  private readonly ILogger<EventGatewayService> _logger;
  private readonly IDbConnectionAccessor _accessor;
  private readonly IEventSubscriber _subscriber;
  private readonly IOptions<BitcraftServiceOptions> _options;

  public EventGatewayService(
    ILogger<EventGatewayService> logger,
    IDbConnectionAccessor accessor,
    IEventSubscriber subscriber,
    IOptions<BitcraftServiceOptions> options
  )
  {
    _logger = logger;
    _accessor = accessor;
    _subscriber = subscriber;
    _options = options;
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

        RunConnectionLifecycle(conn);

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
        break;
      }
    }
  }

  private void RunConnectionLifecycle(DbConnection conn)
  {
    _logger.LogInformation("Start connection lifecycle");

    var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var nowTimestamp = DateTimeOffset.UtcNow.ToString("o");

    // Configure base subscription
    conn.SubscriptionBuilder()
      .OnApplied((ctx) => Task.Run(() => OnBaseSubscriptionsApplied(ctx, conn)))
      .OnError(OnBaseSubscriptionsErrored)
      .Subscribe([
        "SELECT ls.* from location_state ls INNER JOIN public_progressive_action_state ppas ON ppas.building_entity_id = ls.entity_id",
        "SELECT * from item_desc",
        "SELECT * from crafting_recipe_desc",
        "SELECT * from item_list_desc",
        @"SELECT e.* FROM empire_state e
  JOIN claim_state c
    ON e.capital_building_entity_id = c.owner_building_entity_id
  ",
        "SELECT * from building_desc",
        "SELECT t.* from claim_state t WHERE t.neutral = FALSE",
        "SELECT * FROM claim_local_state",
        "SELECT * from player_username_state",
        $"SELECT t.* from chat_message_state t WHERE t.channel_id >= 0 AND t.timestamp > {nowUnixSeconds}",
        $"SELECT * from user_moderation_state",
        "SELECT * from buy_order_state",
        "SELECT * from sell_order_state",
        "SELECT * from building_state",
        @"SELECT s.* FROM progressive_action_state s
INNER JOIN public_progressive_action_state p
  ON s.entity_id = p.entity_id
WHERE s.craft_count > 50",
        @"SELECT p.* FROM public_progressive_action_state p
JOIN progressive_action_state s
ON p.entity_id = s.entity_id
  WHERE s.craft_count > 50",
        "SELECT * FROM inventory_state"
      ]);
  }

  private void OnBaseSubscriptionsErrored(ErrorContext err, Exception ex)
  {
    _logger.LogError(ex, "Error applying base subscriptions");
  }

  private async Task OnBaseSubscriptionsApplied(SubscriptionEventContext _, DbConnection conn)
  {
    try
    {
      _logger.LogInformation("Base subscriptions applied");
      await _subscriber.PopulateBaseCachesAsync(conn);
      // todo send this regularly and inject name from options into application
      _subscriber.PublishSystemEvent(new HeartbeatEvent(
        Application: $"gateway-{_options.Value.Module}",
        DateTime.UtcNow,
        Seq: 0
      ));

      _logger.LogInformation("Registering update handlers");
      _subscriber.SubscribeToChanges(conn);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Exception thrown by subscriber init");
    }
  }

  public override Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }
}