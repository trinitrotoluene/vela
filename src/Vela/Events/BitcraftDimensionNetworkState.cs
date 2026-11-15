namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftDimensionNetworkState(
    string Id,
    string BuildingId,
    string ClaimEntityId
  ) : BitcraftEventBase(Id);
}
