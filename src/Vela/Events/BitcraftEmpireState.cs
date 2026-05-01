namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftEmpireState(
    string Id,
    string Name,
    int ShardTreasury
  ) : BitcraftEventBase(Id);
}