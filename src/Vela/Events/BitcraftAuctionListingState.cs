namespace Vela.Events
{
  public record BitcraftAuctionListingState(
    string Id,
    string OwnerId,
    string ClaimId,
    int Price,
    int Quantity,
    int StoredCoins,
    int ItemId
  ) : BitcraftEventBase(Id);
}