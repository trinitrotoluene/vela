using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("InventoryState", DiskBacked = true, EventStream = true)]
public partial struct ConvergeInventoryState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1, MaxCount = 256)] public ConvergeInventoryPocket[] Pockets { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
