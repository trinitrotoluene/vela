namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftClaimState(
    string Id,
    string OwnerPlayerId,
    string OwnerBuildingId,
    string Name,
    bool IsNeutral
  ) : BitcraftEventBase(Id);
}