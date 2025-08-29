namespace Vela.Events
{
  [GlobalEntity]
  public record BitcraftEmpireState(
    string Id,
    string Name,
    int ShardTreasury
  ) : BitcraftEventBase(Id);
}