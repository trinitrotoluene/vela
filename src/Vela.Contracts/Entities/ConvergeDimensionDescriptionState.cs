using Convergence.Client;

namespace Vela.Contracts.Entities;

// One per interior dimension. Maps a LocationState.Dimension to the interior network it belongs to.
[ConvergenceEntity("DimensionDescriptionState")]
public partial struct ConvergeDimensionDescriptionState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong DimensionNetworkId { get; set; }
    [Field(2)] public uint DimensionId { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
