namespace Vela.Events
{
  [Storage(StorageTarget.Cache)]
  public record BitcraftBuildingState(
    string Id,
    string ClaimEntityId
  ) : BitcraftEventBase(Id);
}