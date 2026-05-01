namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftAuctionListingState(
    string Id,
    string OwnerId,
    string ClaimId,
    int Price,
    int Quantity,
    int StoredCoins,
    int ItemId,
    bool IsCargoItem
  ) : BitcraftEventBase(Id);
}