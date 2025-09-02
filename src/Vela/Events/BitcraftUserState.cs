namespace Vela.Events;

public record BitcraftUserState(
  string Id,
  string UserEntityId,
  bool CanSignIn
) : BitcraftEventBase(Id);