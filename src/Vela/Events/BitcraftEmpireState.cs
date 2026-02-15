namespace Vela.Events
{
  [GlobalEntity, Storage(StorageTarget.Database)]
  public record BitcraftEmpireState(
    string Id,
    string Name,
    int ShardTreasury
  ) : BitcraftEventBase(Id);
}