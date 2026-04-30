using Convergence.Client;

namespace Vela.Services.Contracts;

public interface IConvergeDbWriter
{
    Task InitializeAsync(CancellationToken ct);
    Task AssertAsync<T>(T entity, EntityMetadata? metadata = null) where T : struct, IConvergenceEntity<T>;
    Task RetractAsync<T>(ReadOnlyMemory<byte> entityId, EntityMetadata? metadata = null) where T : struct, IConvergenceEntity<T>;
    Task EpochAsync(Func<Task> body);
    ConvergenceBatch Batch();
    KindHandle<T> GetKind<T>() where T : struct, IConvergenceEntity<T>;
}
