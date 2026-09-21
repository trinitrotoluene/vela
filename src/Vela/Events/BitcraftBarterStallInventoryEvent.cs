namespace Vela.Events
{
  [ConvergeDbEvent]
  public record BitcraftBarterStallInventoryEvent(
    string Id,
    string ClaimId,
    string ActorId,
    string Reason,
    string? TradeOrderId,
    int? TradeAmount,
    BitcraftItemStack[] AddedItems,
    BitcraftItemStack[] RemovedItems,
    int TreasuryCoinsAdded,
    int TreasuryCoinsRemoved,
    long TimestampMicros
  ) : BitcraftEventBase(Id);
}
