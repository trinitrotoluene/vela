namespace Vela.Events;

[ConvergeDb]
public record BitcraftUserState(
  string Id,
  string UserEntityId,
  bool CanSignIn
) : BitcraftEventBase(Id);