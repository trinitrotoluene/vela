namespace Vela.Events
{
  [Postgres]
  public record BitcraftBuildingTypeDesc(
    string Id,
    string Name,
    string Category
  ) : BitcraftEventBase(Id);
}
