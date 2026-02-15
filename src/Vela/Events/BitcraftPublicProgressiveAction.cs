namespace Vela.Events
{
  [Storage(StorageTarget.Cache)]
  public record BitcraftPublicProgressiveAction(
    string Id,
    string BuildingEntityId,
    string OwnerEntityId
  ) : BitcraftEventBase(Id);
}