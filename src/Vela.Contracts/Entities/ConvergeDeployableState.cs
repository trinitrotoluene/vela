using Convergence.Client;

namespace Vela.Contracts.Entities;

// A player's deployable (a boat, a cart and the like), keyed by the deployable's entity id, which
// is the OwnerEntityId of its inventory in InventoryState. From the game's deployable_state_v2.
[ConvergenceEntity("DeployableState")]
public partial struct ConvergeDeployableState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    // The owning player's entity id.
    [Field(1)] public ulong OwnerId { get; set; }
    [Field(2)] public ulong ClaimEntityId { get; set; }
    // Taken out of the world a while after its owner signs out, and put back when they sign in.
    // It is still theirs, with its inventory.
    [Field(3)] public bool Hidden { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
