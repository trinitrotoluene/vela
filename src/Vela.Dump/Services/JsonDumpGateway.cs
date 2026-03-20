using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpacetimeDB.Types;

namespace Vela.Dump.Services;

public class JsonDumpGateway : BackgroundService
{
  private readonly ILogger<JsonDumpGateway> _logger;
  private readonly IDbConnectionAccessor _accessor;
  private readonly IOptions<JsonDumpOptions> _dumpOptions;
  private readonly JsonDumpSubscriber _subscriber;

  public JsonDumpGateway(
    ILogger<JsonDumpGateway> logger,
    IDbConnectionAccessor accessor,
    IOptions<JsonDumpOptions> dumpOptions,
    JsonDumpSubscriber subscriber)
  {
    _logger = logger;
    _accessor = accessor;
    _dumpOptions = dumpOptions;
    _subscriber = subscriber;
  }

  protected override async Task ExecuteAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Starting JSON dump gateway, waiting for connection");

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

        while (_accessor.TryGet(out var currentConn) && conn == currentConn)
        {
          await Task.Delay(1000, cancellationToken);
        }

        _logger.LogInformation("Connection lost, waiting for reconnection");
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error in dump gateway loop");
        break;
      }
    }
  }

  private void RunConnectionLifecycle(DbConnection conn)
  {
    conn.SubscriptionBuilder()
      .OnApplied((ctx) =>
      {
        _logger.LogInformation("Subscriptions applied, starting initial dump");
        Task.Run(async () =>
        {
          await _subscriber.DumpTablesAsync(conn);
          _logger.LogInformation("Initial dump complete");
        });
      })
      .OnError((err, ex) =>
      {
        _logger.LogError(ex, "Error applying subscriptions");
      })
      .Subscribe(_dumpOptions.Value.Subscriptions);
  }
}
