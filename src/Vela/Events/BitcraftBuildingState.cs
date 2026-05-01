namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftBuildingState(
    string Id,
    string ClaimEntityId
  ) : BitcraftEventBase(Id);
}