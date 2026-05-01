namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftInventoryState(
    string Id,
    BitcraftInventoryPocket[] Pockets
  ) : BitcraftEventBase(Id);

  public record BitcraftInventoryPocket(
    string? ItemId,
    int? Quantity,
    string? ItemType
  );
}