namespace Vela.Contracts.Entities;

// Implemented by event-only kinds. The writer owns the entity id these are written under (one per
// source), so it needs a way to stamp it onto the otherwise init-only EntityId.
public interface IConvergeEvent<TSelf> where TSelf : struct, IConvergeEvent<TSelf>
{
    TSelf WithEntityId(ReadOnlyMemory<byte> entityId);
}
