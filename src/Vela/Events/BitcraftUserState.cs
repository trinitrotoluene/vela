namespace Vela.Events;

[Storage(StorageTarget.Database)]
public record BitcraftUserState(
  string Id,
  string UserEntityId,
  bool CanSignIn
) : BitcraftEventBase(Id);