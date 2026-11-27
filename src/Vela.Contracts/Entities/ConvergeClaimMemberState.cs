using Convergence.Client;

namespace Vela.Contracts.Entities;

// A player's membership of a claim, keyed by the membership's entity id. From the game's
// claim_member_state. Any member counts, whatever their permissions there.
[ConvergenceEntity("ClaimMemberState")]
public partial struct ConvergeClaimMemberState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong ClaimEntityId { get; set; }
    // The member's player entity id.
    [Field(2)] public ulong PlayerEntityId { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
