using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ItemDescMapper : MappedDbEntityBase<ItemDesc, BitcraftItem>
  {
    public override string TopicName => "bitcraft.item";

    public override BitcraftItem Map(ItemDesc entity) =>
        new(
          Id: entity.Id,
          Name: entity.Name,
          Description: entity.Description,
          Volume: entity.Volume,
          Tier: entity.Tier,
          Rarity: (BitcraftItemRarity)entity.Rarity,
          ItemListId: entity.ItemListId,
          HasCompendiumEntry: entity.CompendiumEntry);
  }
}