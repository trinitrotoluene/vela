using Convergence.Client;
using Vela.Contracts.Entities;

namespace Vela.Services.Contracts;

public interface IConvergeDbWriter
{
    Task InitializeAsync(CancellationToken ct);
    Task AssertAsync<T>(T entity, EntityMetadata? metadata = null) where T : struct, IConvergenceEntity<T>;
    // Event-only kinds: buffer one event-stream op for T without maintaining per-entity state.
    Task EmitAsync<T>(T evt) where T : struct, IConvergenceEntity<T>, IConvergeEvent<T>;
    Task RetractAsync<T>(ReadOnlyMemory<byte> entityId, EntityMetadata? metadata = null) where T : struct, IConvergenceEntity<T>;

    // Flush steady-state writes buffered since the last call. Intended to be called once per
    // connection-loop tick (BitcraftService). No-ops while a snapshot epoch is open and when
    // nothing was buffered. Owns ConvergeDB transport-failure → host-shutdown handling.
    Task FlushPendingAsync(CancellationToken ct);
    Task EpochAsync(Func<Task> body);
    Task BeginEpochAsync(CancellationToken ct = default);
    Task EndEpochAsync(CancellationToken ct = default);
    ConvergenceBatch Batch();
    KindHandle<T> GetKind<T>() where T : struct, IConvergenceEntity<T>;
}
