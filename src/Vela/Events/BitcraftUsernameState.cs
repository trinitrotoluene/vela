namespace Vela.Events
{
  public record BitcraftUsernameState(
    string Id,
    string Username
  ) : BitcraftEventBase(Id);
}