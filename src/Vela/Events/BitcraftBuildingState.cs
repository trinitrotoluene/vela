namespace Vela.Events
{
  public record BitcraftBuildingState(
    string Id,
    string ClaimEntityId
  ) : BitcraftEventBase(Id);
}