using Convergence.Client;
using Vela.Contracts.Entities;
using Vela.Services.Contracts;

namespace Vela.Dump.Services;

/// <summary>
/// BitcraftService requires a ConvergeDB writer, but the dump tool never writes to ConvergeDB.
/// Only FlushPendingAsync is called on the connection loop; everything else is unreachable here.
/// </summary>
public sealed class NullConvergeDbWriter : IConvergeDbWriter
{
  public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
  public Task FlushPendingAsync(CancellationToken ct) => Task.CompletedTask;

  public Task AssertAsync<T>(T entity, EntityMetadata? metadata = null) where T : struct, IConvergenceEntity<T> => throw Unused();
  public Task EmitAsync<T>(T evt) where T : struct, IConvergenceEntity<T>, IConvergeEvent<T> => throw Unused();
  public Task RetractAsync<T>(ReadOnlyMemory<byte> entityId, EntityMetadata? metadata = null) where T : struct, IConvergenceEntity<T> => throw Unused();
  public Task EpochAsync(Func<Task> body) => throw Unused();
  public Task BeginEpochAsync(CancellationToken ct = default) => throw Unused();
  public Task EndEpochAsync(CancellationToken ct = default) => throw Unused();
  public ConvergenceBatch Batch() => throw Unused();
  public KindHandle<T> GetKind<T>() where T : struct, IConvergenceEntity<T> => throw Unused();

  private static NotSupportedException Unused() => new("Vela.Dump does not write to ConvergeDB.");
}
