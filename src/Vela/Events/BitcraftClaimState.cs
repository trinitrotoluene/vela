namespace Vela.Events
{
  [GlobalEntity]
  public record BitcraftClaimState(
    string Id,
    string OwnerPlayerId,
    string OwnerBuildingId,
    string Name,
    bool IsNeutral
  ) : BitcraftEventBase(Id);
}