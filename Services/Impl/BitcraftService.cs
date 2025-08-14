using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using SpacetimeDB;
using SpacetimeDB.Types;

public class BitcraftService : IHostedService
{
  private readonly ILogger<BitcraftService> _logger;
  private readonly IOptions<BitcraftServiceOptions> _options;
  private readonly AsyncRetryPolicy _retryPolicy;
  private readonly SemaphoreSlim _reconnectionLock;
  private Task? _connectionLoopTask;
  private CancellationTokenSource? _connLoopCts;

  public BitcraftService(ILogger<BitcraftService> logger, IOptions<BitcraftServiceOptions> options)
  {
    _logger = logger;
    _options = options;
    _retryPolicy = Policy.Handle<Exception>()
      .WaitAndRetryAsync(
        GetRetrySchedule(),
        onRetry: (err, timeSpan, retryCount, context) =>
        {
          _logger.LogWarning(err, "Retry attempt {retryCount}: waiting {timeSpan}", retryCount, timeSpan);
        }
      );
    _reconnectionLock = new SemaphoreSlim(1, 1);
    _connLoopCts = null;
  }

  private static IEnumerable<TimeSpan> GetRetrySchedule()
  {
    // First we try to reconnect immediately in case it was a transient error
    yield return TimeSpan.FromSeconds(2);
    yield return TimeSpan.FromSeconds(4);

    // Sustained errors typically mean the token is dead until we log in on the game client
    // so, try every 30 min for the next 12 hours.
    var duration = TimeSpan.FromHours(12);
    var interval = TimeSpan.FromMinutes(30);
    int additionalRetries = (int)(duration.TotalMinutes / interval.TotalMinutes);

    for (int i = 0; i < additionalRetries; i++)
    {
      yield return interval;
    }
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    try
    {
      await ConnectWithRetryAsync(cancellationToken);
      await Task.Delay(Timeout.Infinite, cancellationToken);
    }
    catch (TaskCanceledException tcEx)
    {
      _logger.LogInformation(tcEx, "Bitcraft service cancelled");
    }

    _logger.LogInformation("Bitcraft service run to completion");
  }

  public async Task ConnectionLoop(DbConnection conn, CancellationToken cancellationToken)
  {
    _connLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

    try
    {
      _logger.LogInformation("Loop started");
      while (!_connLoopCts.Token.IsCancellationRequested)
      {
        conn.FrameTick();
        await Task.Delay(50, cancellationToken);
      }
      _logger.LogInformation("Loop cancelled");
    }
    catch (TaskCanceledException)
    {
      _logger.LogInformation("Connection loop cancelled");
    }
    catch (Exception err)
    {
      _logger.LogError(err, "Error occurred in connection loop");
    }
    finally
    {
      conn.Disconnect();
      while (conn.IsActive)
      {
        conn.FrameTick();
      }
    }
  }

  private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Acquiring reconnection lock");
    await _reconnectionLock.WaitAsync(cancellationToken);

    try
    {
      _logger.LogInformation("Acquired reconnection lock");

      _connLoopCts?.Cancel();
      _connLoopCts?.Dispose();
      _connLoopCts = null;

      await (_connectionLoopTask ?? Task.CompletedTask);
      await _retryPolicy.ExecuteAsync(ConnectAsync, cancellationToken);
    }
    finally
    {
      _reconnectionLock.Release();
    }
  }

  private Task<DbConnection> ConnectAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Attempting connection");

    var tcs = new TaskCompletionSource<DbConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());

    var conn = DbConnection.Builder()
      .WithUri(_options.Value.Uri)
      .WithModuleName(_options.Value.Module)
      .WithToken(_options.Value.AuthToken)
      .OnConnect((conn, identity, token) =>
      {
        tcs.TrySetResult(conn);
        _logger.LogInformation("Connected with identity {identity}", identity.ToString());
      })
      .OnConnectError((err) =>
      {
        _logger.LogError(err, "Connection error");
        tcs.TrySetException(err);
      })
      .OnDisconnect((ctx, err) =>
      {
        if (err != null && !cancellationToken.IsCancellationRequested)
        {
          _logger.LogWarning(err, "Disconnected due to an error");
          _ = Task.Run(() => ConnectWithRetryAsync(cancellationToken), cancellationToken);
        }
        else
        {
          _logger.LogWarning("Did not disconnect due to an error - not attempting to reconnect");
        }
      })
      .Build();

    _logger.LogInformation("Spawning connection loop task");
    _connectionLoopTask = Task.Run(() => ConnectionLoop(conn, cancellationToken), cancellationToken);
    return tcs.Task;
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Beginning cleanup");

    _connLoopCts?.Cancel();
    _connLoopCts?.Dispose();

    await (_connectionLoopTask ?? Task.CompletedTask);

    _logger.LogInformation("Cleanup complete");
  }
}
