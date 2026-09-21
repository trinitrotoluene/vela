using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("BuildingState")]
public partial struct ConvergeBuildingState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong ClaimEntityId { get; set; }
    // bitcraft_building_desc id.
    [Field(2)] public int BuildingDescriptionId { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
