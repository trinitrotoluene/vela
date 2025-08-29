namespace Vela.Events
{
  [GlobalEntity]
  public record BitcraftUsernameState(
    string Id,
    string Username
  ) : BitcraftEventBase(Id);
}