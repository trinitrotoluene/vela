namespace Vela.Events
{
  [Storage(StorageTarget.Database)]
  public record BitcraftClosedListingState(
    string Id,
    string OwnerId,
    string ClaimId,
    BitcraftItemStack ItemStack,
    string Timestamp
  ) : BitcraftEventBase(Id);

  public record BitcraftItemStack(
    int ItemId,
    int Quantity,
    bool IsCargo
  );
}