using Google.Protobuf.WellKnownTypes;
using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ClosedListingStateMapper : MappedDbEntityBase<ClosedListingState, BitcraftClosedListingState>
  {
    public override BitcraftClosedListingState Map(
      ClosedListingState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        OwnerId: entity.OwnerEntityId.ToString(),
        ClaimId: entity.ClaimEntityId.ToString(),
        ItemStack: new BitcraftItemStack(
          ItemId: entity.ItemStack.ItemId,
          Quantity: entity.ItemStack.Quantity,
          IsCargo: entity.ItemStack.ItemType == ItemType.Cargo
        ),
        Timestamp: entity.Timestamp.ToStd().ToString()
      );
  }
}