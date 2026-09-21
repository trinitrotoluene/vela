namespace Vela.Events
{
  [Postgres]
  public record BitcraftToolDesc(
    string Id,
    int ItemId,
    int ToolType,
    int Level,
    int Power
  ) : BitcraftEventBase(Id);
}
