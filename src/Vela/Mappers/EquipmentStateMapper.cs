using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class EquipmentStateMapper : MappedDbEntityBase<EquipmentState, BitcraftEquipmentState>
  {
    public override BitcraftEquipmentState Map(EquipmentState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          Slots: [.. entity.EquipmentSlots.Select(s => InventoryStateMapper.MapPocket(s.Item))]
        );
  }
}
