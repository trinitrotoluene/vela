using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("InventoryState", DiskBacked = true, EventStream = true)]
public partial struct ConvergeInventoryState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1, MaxCount = 256)] public ConvergeInventoryPocket[] Pockets { get; set; }
    // EntityId is the inventory's own id; these locate it. Building storage has PlayerOwnerEntityId = 0,
    // a bank's per-player inventories carry the player there. InventoryIndex orders a building's inventories.
    [Field(2)] public ulong OwnerEntityId { get; set; }
    [Field(3)] public ulong PlayerOwnerEntityId { get; set; }
    [Field(4)] public int InventoryIndex { get; set; }
    // Pockets from CargoIndex on take cargo, those before it items. Rows written before this field
    // existed read 0 here until they next change.
    [Field(5)] public int CargoIndex { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
