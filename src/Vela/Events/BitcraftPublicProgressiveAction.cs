namespace Vela.Events
{
  public record BitcraftPublicProgressiveAction(
    string Id,
    string BuildingEntityId,
    string OwnerEntityId
  ) : BitcraftEventBase(Id);
}