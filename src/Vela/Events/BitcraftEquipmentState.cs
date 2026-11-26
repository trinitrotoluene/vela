namespace Vela.Events
{
  // Keyed by the player's entity id.
  [ConvergeDb]
  public record BitcraftEquipmentState(
    string Id,
    BitcraftInventoryPocket[] Slots
  ) : BitcraftEventBase(Id);
}
