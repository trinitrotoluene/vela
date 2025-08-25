using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class SellOrderStateMapper : MappedDbEntityBase<AuctionListingState, BitcraftAuctionListingState>
  {
    public override string TopicName => "bitcraft.orders.sell";

    public override BitcraftAuctionListingState Map(
      AuctionListingState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        OwnerId: entity.OwnerEntityId.ToString(),
        ClaimId: entity.ClaimEntityId.ToString(),
        Price: entity.PriceThreshold,
        Quantity: entity.Quantity,
        StoredCoins: entity.StoredCoins,
        ItemId: entity.ItemId
      );
  }
}