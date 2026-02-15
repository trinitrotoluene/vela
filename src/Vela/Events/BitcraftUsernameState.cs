namespace Vela.Events
{
  [GlobalEntity, Storage(StorageTarget.Database)]
  public record BitcraftUsernameState(
    string Id,
    string Username
  ) : BitcraftEventBase(Id);
}