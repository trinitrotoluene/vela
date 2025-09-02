using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using SpacetimeDB.Types;
using Vela.Services.Contracts;

/// <summary>
/// This service provides a fault-tolerant, cancellable connection to the Bitcraft backend
/// </summary>
public class BitcraftService : BackgroundService
{
  private readonly Counter<long> _connectionAttemptsMetric;
  private readonly Counter<long> _disconnectionsMetric;
  private readonly Counter<long> _connectionsMetric;

  private readonly ILogger<BitcraftService> _logger;
  private readonly IOptions<BitcraftServiceOptions> _options;
  private readonly IDbConnectionAccessor _accessor;
  private readonly IMetricHelpers _metricHelpers;
  private readonly AsyncRetryPolicy _retryPolicy;
  private readonly SemaphoreSlim _reconnectionLock;
  private Task? _connectionLoopTask;
  private CancellationTokenSource? _connLoopCts;

  public BitcraftService(
    ILogger<BitcraftService> logger,
    IOptions<BitcraftServiceOptions> options,
    IDbConnectionAccessor accessor,
    IMeterFactory metricsFactory,
    IMetricHelpers metricHelpers
  )
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
    _accessor = accessor;
    _metricHelpers = metricHelpers;

    var metrics = metricsFactory.Create("Vela");

    _connectionAttemptsMetric = metrics.CreateCounter<long>("bitcraft_connection_attempted");
    _connectionsMetric = metrics.CreateCounter<long>("bitcraft_connection_connected");
    _disconnectionsMetric = metrics.CreateCounter<long>("bitcraft_connection_disconnected");
  }

  private static IEnumerable<TimeSpan> GetRetrySchedule()
  {
    // First we try to reconnect immediately in case it was a transient error
    yield return TimeSpan.FromSeconds(2);
    yield return TimeSpan.FromSeconds(4);
    yield return TimeSpan.FromSeconds(30);
    yield return TimeSpan.FromMinutes(5);

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

  protected override async Task ExecuteAsync(CancellationToken cancellationToken)
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

  private async Task ConnectionLoop(DbConnection conn, CancellationToken cancellationToken)
  {
    _connLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    await Task.Yield();

    const int targetHz = 60;
    const double targetLoopMs = 1000.0 / targetHz;

    try
    {
      _logger.LogInformation("Loop started");

      var stopwatch = Stopwatch.StartNew()!;

      while (!_connLoopCts.Token.IsCancellationRequested)
      {
        var startMs = stopwatch.ElapsedMilliseconds;
        conn.FrameTick();
        var endMs = stopwatch.ElapsedMilliseconds;
        var elapsedMs = endMs - startMs;

        var delay = Math.Max(0, targetLoopMs - elapsedMs);

        await Task.Delay((int)delay, _connLoopCts.Token);
      }

      _logger.LogInformation("Loop ended");
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
      _accessor.SetConnection(null);
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
      var conn = await _retryPolicy.ExecuteAsync(ConnectAsync, cancellationToken);
      if (conn == null)
      {
        _logger.LogCritical("ConnectAsync unexpectedly returned null");
        return;
      }
      _logger.LogInformation("Connection succeeded");
      _accessor.SetConnection(conn);
    }
    finally
    {
      _logger.LogInformation("Releasing reconnection lock");
      _reconnectionLock.Release();
    }
  }

  private async Task<DbConnection> ConnectAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Cancelling stale connection loop");
    var oldCts = _connLoopCts;
    _connLoopCts = null;
    oldCts?.Cancel();
    await (_connectionLoopTask ?? Task.CompletedTask);

    oldCts?.Dispose();
    _logger.LogInformation("Stale connection loop successfully cancelled");

    _logger.LogInformation("Attempting connection");
    _connectionAttemptsMetric.Add(1, new TagList() { { "service", _metricHelpers.ServiceName } });
    var tcs = new TaskCompletionSource<DbConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());

    var conn = DbConnection.Builder()
      .WithUri(_options.Value.Uri)
      .WithModuleName(_options.Value.Module)
      .WithToken(_options.Value.AuthToken)
      .OnConnect((conn, identity, token) =>
      {
        _connectionsMetric.Add(1, new TagList() { { "service", _metricHelpers.ServiceName } });

        _logger.LogInformation("Connected with identity {identity}", identity.ToString());
        tcs.TrySetResult(conn);
      })
      .OnConnectError((err) =>
      {
        _logger.LogError(err, "Connection error");
        tcs.TrySetException(err);
      })
      .OnDisconnect((ctx, err) =>
      {
        _disconnectionsMetric.Add(1, new TagList() { { "service", _metricHelpers.ServiceName } });

        // If this callback is invoked after the tcs is completed, this means we signalled a successful connection to the caller
        // so we need to schedule the reconnection ourselves.
        if (tcs.Task.IsCompleted)
        {
          _ = Task.Run(() => ConnectWithRetryAsync(cancellationToken));
          return;
        }

        // Otherwise, the callback was invoked before we signalled a successful connection to the caller
        // so we can safely try and set the exception - the caller may then attempt to retry.
        if (err != null)
        {
          _logger.LogWarning(err, "Disconnected due to an error");
          tcs.TrySetException(err);
        }
        else
        {
          _logger.LogWarning("OnDisconnect invoked with a null exception");
          tcs.TrySetException(new Exception("Unknown error"));
        }
      })
      .Build();

    _logger.LogInformation("Spawning connection loop task");
    _connectionLoopTask = Task.Run(() => ConnectionLoop(conn, cancellationToken), cancellationToken);
    return await tcs.Task;
  }

  public override async Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Beginning cleanup");

    _accessor.SetConnection(null);
    _connLoopCts?.Cancel();
    _connLoopCts?.Dispose();

    await (_connectionLoopTask ?? Task.CompletedTask);

    _logger.LogInformation("Cleanup complete");
  }

  public override void Dispose()
  {
    _reconnectionLock?.Dispose();
    _connLoopCts?.Dispose();
  }
}
