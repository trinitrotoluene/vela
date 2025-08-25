using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class BuyOrderStateMapper : MappedDbEntityBase<AuctionListingState, BitcraftAuctionListingState>
  {
    public override string TopicName => "bitcraft.orders.buy";

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