using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class InventoryStateMapper : MappedDbEntityBase<InventoryState, BitcraftInventoryState>
  {
    public override BitcraftInventoryState Map(
      InventoryState entity
    ) => new(
        Id: entity.OwnerEntityId.ToString(),
        Pockets: [.. entity.Pockets.Select(x => new BitcraftInventoryPocket(
          ItemId: x.Contents?.ItemId.ToString(),
          Quantity: x.Contents?.Quantity
        ))]
      );
  }
}