namespace Vela.Events
{
  [ConvergeDbEvent]
  public record BitcraftMarketTradeEvent(
    string Id,
    string ClaimId,
    bool IsBuyOrder,
    string BuyerId,
    string SellerId,
    int ItemId,
    bool IsCargo,
    int Quantity,
    int UnitPrice,
    int ListingRemainingQuantity,
    long TimestampMicros
  ) : BitcraftEventBase(Id);
}
