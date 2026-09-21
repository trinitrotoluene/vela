namespace Vela.Events
{
  // Target of ResourceId on extraction recipes.
  [Postgres]
  public record BitcraftResourceDesc(
    string Id,
    string Name,
    string Description,
    int Tier,
    string Tag,
    BitcraftItemRarity Rarity,
    int MaxHealth,
    ItemStack[] OnDestroyYield,
    int OnDestroyYieldResourceId,
    bool HasCompendiumEntry
  ) : BitcraftEventBase(Id);
}
