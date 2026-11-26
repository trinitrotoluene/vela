using Convergence.Client;

namespace Vela.Contracts.Entities;

// One of a player's equipment presets, keyed by the preset's own entity id. Every preset holds its
// items whether it is active or not; switching presets only moves the Active flag.
[ConvergenceEntity("EquipmentPresetState")]
public partial struct ConvergeEquipmentPresetState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong PlayerEntityId { get; set; }
    [Field(2)] public int Index { get; set; }
    [Field(3)] public bool Active { get; set; }
    // One per equipment slot, in the game's order; an empty slot has ItemType 0, as in inventories.
    [Field(4, MaxCount = 64)] public ConvergeInventoryPocket[] Slots { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
