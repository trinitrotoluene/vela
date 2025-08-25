namespace Vela.Events
{
  public record BitcraftEmpireState(
    string Id,
    string Name,
    int ShardTreasury
  ) : BitcraftEventBase(Id);
}