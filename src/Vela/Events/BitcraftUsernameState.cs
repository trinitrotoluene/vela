namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftUsernameState(
    string Id,
    string Username
  ) : BitcraftEventBase(Id);
}