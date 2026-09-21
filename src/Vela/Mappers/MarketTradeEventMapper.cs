using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class MarketTradeEventMapper : MappedDbEntityBase<MarketTradeEvent, BitcraftMarketTradeEvent>
  {
    public override BitcraftMarketTradeEvent Map(
      MarketTradeEvent entity
    ) => new(
        Id: entity.ListingEntityId.ToString(),
        ClaimId: entity.ClaimEntityId.ToString(),
        IsBuyOrder: entity.ListingType == MarketOrderType.BuyOrder,
        BuyerId: entity.BuyerEntityId.ToString(),
        SellerId: entity.SellerEntityId.ToString(),
        ItemId: entity.ItemId,
        IsCargo: entity.ItemType == ItemType.Cargo,
        Quantity: entity.Quantity,
        UnitPrice: entity.UnitPrice,
        ListingRemainingQuantity: entity.ListingRemainingQuantity,
        TimestampMicros: entity.Timestamp.MicrosecondsSinceUnixEpoch
      );
  }
}
