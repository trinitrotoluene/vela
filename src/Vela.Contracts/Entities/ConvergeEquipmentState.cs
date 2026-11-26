using Convergence.Client;

namespace Vela.Contracts.Entities;

// What a player wears outside their equipment presets, keyed by the player's entity id. An equipped
// item is held here or in one preset (ConvergeEquipmentPresetState), never both and never also in an
// inventory, so holdings add the two up.
[ConvergenceEntity("EquipmentState")]
public partial struct ConvergeEquipmentState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    // One per equipment slot, in the game's order; an empty slot has ItemType 0, as in inventories.
    [Field(1, MaxCount = 64)] public ConvergeInventoryPocket[] Slots { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
