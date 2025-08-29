namespace Vela.Events
{
  public record BitcraftInventoryState(
    string Id,
    BitcraftInventoryPocket[] Pockets
  ) : BitcraftEventBase(Id);

  public record BitcraftInventoryPocket(
    string? ItemId,
    int? Quantity
  );
}