namespace Vela.Events
{
  // Keyed by the owning player's entity id.
  [ConvergeDb]
  public record BitcraftPlayerHousingState(
    string Id,
    string EntranceBuildingId,
    string NetworkId
  ) : BitcraftEventBase(Id);
}
