namespace Vela.Events
{
  // Keyed by the inventory's own entity id: one owner can have several inventories (one per storage
  // building function, plus a personal inventory per player in a bank).
  [ConvergeDb]
  public record BitcraftInventoryState(
    string Id,
    string OwnerEntityId,
    string PlayerOwnerEntityId,
    int InventoryIndex,
    BitcraftInventoryPocket[] Pockets
  ) : BitcraftEventBase(Id);

  public record BitcraftInventoryPocket(
    string? ItemId,
    int? Quantity,
    string? ItemType
  );
}
