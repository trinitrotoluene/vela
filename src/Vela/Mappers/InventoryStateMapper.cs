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
        Pockets: [.. entity.Pockets.Select(x => MapPocket(x.Contents))]
      );

    // An inventory pocket's or equipment slot's contents; null when it is empty.
    internal static BitcraftInventoryPocket MapPocket(SpacetimeDB.Types.ItemStack? contents) => new(
      ItemId: contents?.ItemId.ToString(),
      Quantity: contents?.Quantity,
      ItemType: contents?.ItemType.ToString()
    );
  }
}
