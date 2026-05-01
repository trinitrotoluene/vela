namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftPavedTileState(
    string Id,
    int TileTypeId,
    string RelatedEntityId
  ) : BitcraftEventBase(Id);
}
