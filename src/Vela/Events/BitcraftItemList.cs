namespace Vela.Events
{
  [GlobalEntity]
  public record BitcraftItemList(
    string Id,
    string Name,
    ItemListPossibility[] Possibilities
  ) : BitcraftEventBase(Id);

  public record ItemListPossibility(
    float Probability,
    ItemListItem[] Items
  );

  public record ItemListItem(
    int ItemId,
    int Quantity
  );
}