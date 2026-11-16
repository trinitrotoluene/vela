namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftDimensionDescriptionState(
    string Id,
    string DimensionNetworkId,
    uint DimensionId
  ) : BitcraftEventBase(Id);
}
