namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftPublicProgressiveAction(
    string Id,
    string BuildingEntityId,
    string OwnerEntityId
  ) : BitcraftEventBase(Id);
}