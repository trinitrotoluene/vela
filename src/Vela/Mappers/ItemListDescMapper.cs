using stdb = SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ItemListDescMapper : MappedDbEntityBase<stdb::ItemListDesc, BitcraftItemList>
  {
    public override string TopicName => "bitcraft.item_list";

    public override BitcraftItemList Map(stdb::ItemListDesc entity) =>
        new(
          Id: entity.Id,
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