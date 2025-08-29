using Sdk = SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ItemListDescMapper : MappedDbEntityBase<Sdk::ItemListDesc, BitcraftItemList>
  {
    public override BitcraftItemList Map(Sdk::ItemListDesc entity) =>
        new(
          Id: entity.Id.ToString(),
          Name: entity.Name,
          Possibilities: [.. entity.Possibilities.Select(x => new ItemListPossibility(
          Probability: x.Probability,
          Items: [.. x.Items.Select(y => new ItemListItem(
            ItemId: y.ItemId,
            Quantity: y.Quantity
          ))]
        ))]
        );
  }
}