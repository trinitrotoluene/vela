using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("PavedTileState", DiskBacked = true)]
public partial struct ConvergePavedTileState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public int TileTypeId { get; set; }
    [Field(2)] public ulong RelatedEntityId { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
