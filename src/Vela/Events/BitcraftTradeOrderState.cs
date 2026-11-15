namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftTradeOrderState(
    string Id,
    string ShopId,
    int RemainingStock,
    BitcraftItemStack[] OfferItems,
    BitcraftItemStack[] RequiredItems
  ) : BitcraftEventBase(Id);
}
