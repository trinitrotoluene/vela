namespace Vela.Events
{
  public record BitcraftBuildingDesc(
    string Id,
    string Name,
    string Description
  ) : BitcraftEventBase(Id);
}