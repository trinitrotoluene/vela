using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vela.Services.Contracts;

namespace Vela.Services.Impl;

/// <summary>
/// Production <see cref="IFatalRestart"/>: turns a fatal condition into a graceful, non-zero-exit
/// host shutdown so the orchestrator restarts the container. See <see cref="IFatalRestart"/> for the
/// policy. Registered as a singleton so the idempotency guard is process-wide.
/// </summary>
public sealed class FatalRestart : IFatalRestart
{
  private readonly ILogger<FatalRestart> _logger;
  private readonly IHostApplicationLifetime _lifetime;
  private int _triggered;

  public FatalRestart(ILogger<FatalRestart> logger, IHostApplicationLifetime lifetime)
  {
    _logger = logger;
    _lifetime = lifetime;
  }

  public void Trigger(string reason)
  {
    // First fatal condition wins; later ones are just noise from loops unwinding through the same
    // broken dependency during the shutdown window.
    if (Interlocked.Exchange(ref _triggered, 1) != 0)
    {
      _logger.LogDebug("Fatal restart already in progress, ignoring: {Reason}", reason);
      return;
    }

    _logger.LogCritical("Fatal condition - restarting process: {Reason}", reason);

    // Non-zero exit so EVERY restart policy (on-failure, always, unless-stopped) treats this as a
    // failure and restarts. We still request a graceful stop (rather than Environment.Exit) so the
    // host drains: logs flush, OTLP exporters ship, IDisposables run. The exit code is read from
    // Environment.ExitCode once Main returns after RunAsync completes.
    Environment.ExitCode = 1;
    _lifetime.StopApplication();
  }
}
