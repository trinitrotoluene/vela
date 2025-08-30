using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class AuctionListingStateMapper : MappedDbEntityBase<AuctionListingState, BitcraftAuctionListingState>
  {
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