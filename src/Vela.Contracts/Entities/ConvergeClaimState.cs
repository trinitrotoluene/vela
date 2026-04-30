using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("ClaimState")]
public partial struct ConvergeClaimState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong OwnerPlayerId { get; set; }
    [Field(2)] public ulong OwnerBuildingId { get; set; }
    [Field(3, MaxLength = 256)] public string Name { get; set; }
    [Field(4)] public bool IsNeutral { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
