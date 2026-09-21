using Sdk = SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ResourceDescMapper : MappedDbEntityBase<Sdk::ResourceDesc, BitcraftResourceDesc>
  {
    public override BitcraftResourceDesc Map(
      Sdk::ResourceDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        Name: entity.Name,
        Description: entity.Description,
        Tier: entity.Tier,
        Tag: entity.Tag,
        Rarity: (BitcraftItemRarity)entity.Rarity,
        MaxHealth: entity.MaxHealth,
        OnDestroyYield: [.. entity.OnDestroyYield.Select(x => new ItemStack(ItemId: x.ItemId.ToString(), Quantity: x.Quantity, ItemType: (BitcraftItemType)x.ItemType))],
        OnDestroyYieldResourceId: entity.OnDestroyYieldResourceId,
        HasCompendiumEntry: entity.CompendiumEntry
      );
  }
}
