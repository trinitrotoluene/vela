namespace Vela.Events
{
  [Storage(StorageTarget.Cache)]
  public record BitcraftPavedTileState(
    string Id,
    int TileTypeId,
    string RelatedEntityId
  ) : BitcraftEventBase(Id);
}
