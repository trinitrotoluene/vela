using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class InventoryStateMapper : MappedDbEntityBase<InventoryState, BitcraftInventoryState>
  {
    public override BitcraftInventoryState Map(
      InventoryState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        OwnerEntityId: entity.OwnerEntityId.ToString(),
        PlayerOwnerEntityId: entity.PlayerOwnerEntityId.ToString(),
        InventoryIndex: entity.InventoryIndex,
        Pockets: [.. entity.Pockets.Select(x => new BitcraftInventoryPocket(
          ItemId: x.Contents?.ItemId.ToString(),
          Quantity: x.Contents?.Quantity,
          ItemType: x.Contents?.ItemType.ToString()
        ))]
      );
  }
}
