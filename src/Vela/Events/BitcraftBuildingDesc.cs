namespace Vela.Events
{
  [Postgres]
  public record BitcraftBuildingDesc(
    string Id,
    string Name,
    string Description
  ) : BitcraftEventBase(Id);
}