using Convergence.Client;

namespace Vela.Contracts.Entities;

// One per interior network (a building's interior, a dungeon or a player's housing).
[ConvergenceEntity("DimensionNetworkState")]
public partial struct ConvergeDimensionNetworkState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    // The building the interior belongs to. Not meaningful for player housing: use
    // PlayerHousingState.EntranceBuildingId instead.
    [Field(1)] public ulong BuildingId { get; set; }
    // 0 for player housing and unclaimed buildings.
    [Field(2)] public ulong ClaimEntityId { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
