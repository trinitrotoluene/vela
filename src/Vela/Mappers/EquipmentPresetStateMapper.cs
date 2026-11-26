using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class EquipmentPresetStateMapper : MappedDbEntityBase<EquipmentPresetState, BitcraftEquipmentPresetState>
  {
    public override BitcraftEquipmentPresetState Map(EquipmentPresetState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          PlayerEntityId: entity.PlayerEntityId.ToString(),
          Index: entity.Index,
          Active: entity.Active,
          Slots: [.. entity.EquipmentSlots.Select(s => InventoryStateMapper.MapPocket(s.Item))]
        );
  }
}
