namespace Vela.Services.Contracts;

/// <summary>
/// The single, process-wide "restart me" signal
///
/// Triggering it requests a graceful host shutdown AND sets a non-zero exit code, so the process
/// drains and exits cleanly while the orchestrator (Docker <c>restart: unless-stopped</c>, k8s, …)
/// unambiguously treats the exit as a failure and restarts the container. The restart re-runs the
/// startup path, which re-seeds ConvergeDB from a clean epoch.
///
/// This is reserved for FATAL, unrecoverable conditions - chiefly any ConvergeDB I/O failure, since
/// ConvergeDB is the system of record and a broken connection compromises the current source epoch.
/// Recoverable upstream (SpacetimeDB) blips must NOT come here: they reconnect in-process via
/// BitcraftService and never restart the host.
/// </summary>
public interface IFatalRestart
{
  /// <summary>
  /// Request a graceful shutdown with a failure exit code. Idempotent: the first call wins and the
  /// reason is logged; later calls during the shutdown window are ignored. Returns immediately -
  /// the actual stop happens as the host unwinds.
  /// </summary>
  void Trigger(string reason);
}
