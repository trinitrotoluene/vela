namespace Vela.Events
{
  [Storage(StorageTarget.Database)]
  public record BitcraftBuildingDesc(
    string Id,
    string Name,
    string Description
  ) : BitcraftEventBase(Id);
}