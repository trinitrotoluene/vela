namespace Vela.Events
{
  // A kind of deployable (a boat, a cart): what DeployableDescriptionId on DeployableState points at.
  [Postgres]
  public record BitcraftDeployableDesc(
    string Id,
    string Name
  ) : BitcraftEventBase(Id);
}
