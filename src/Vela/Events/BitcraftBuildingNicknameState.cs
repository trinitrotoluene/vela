namespace Vela.Events
{
  // Keyed by the building's entity id - the same id inventories carry as OwnerEntityId.
  [ConvergeDb]
  public record BitcraftBuildingNicknameState(
    string Id,
    string Nickname
  ) : BitcraftEventBase(Id);
}
