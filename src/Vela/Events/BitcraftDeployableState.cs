namespace Vela.Events
{
  // From deployable_state_v2, keyed by the deployable's entity id.
  [ConvergeDb]
  public record BitcraftDeployableState(
    string Id,
    string OwnerId,
    string ClaimEntityId,
    bool Hidden
  ) : BitcraftEventBase(Id);
}
