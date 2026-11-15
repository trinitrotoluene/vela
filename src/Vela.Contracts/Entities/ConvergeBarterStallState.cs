using Convergence.Client;

namespace Vela.Contracts.Entities;

// One per barter stall building, keyed by the building's entity id. The game row only carries a
// discontinued flag, so this marks which BuildingState entities are stalls.
[ConvergenceEntity("BarterStallState")]
public partial struct ConvergeBarterStallState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
