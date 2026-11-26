namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftEquipmentPresetState(
    string Id,
    string PlayerEntityId,
    int Index,
    bool Active,
    BitcraftInventoryPocket[] Slots
  ) : BitcraftEventBase(Id);
}
