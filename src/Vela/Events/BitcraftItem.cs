using System.Text.Json.Serialization;

namespace Vela.Events
{
  [Postgres]
  public record BitcraftItem(
    string Id,
    string Name,
    string Description,
    int Volume,
    int Tier,
    BitcraftItemRarity Rarity,
    int ItemListId,
    bool HasCompendiumEntry,
    string Tag
  ) : BitcraftEventBase(Id);

  [Postgres]
  public record BitcraftCargoItem(
    string Id,
    string Name,
    string Description,
    int Volume,
    int Tier,
    BitcraftItemRarity Rarity,
    string Tag,
    bool NotPickupable,
    bool BlocksPath
  ) : BitcraftEventBase(Id);

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public enum BitcraftItemRarity
  {
    Default = 0,
    Common = 1,
    Uncommon = 2,
    Rare = 3,
    Epic = 4,
    Legendary = 5,
    Mythic = 6,
  }

  // Item and cargo ids overlap, so any item reference needs this to pick the right table.
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public enum BitcraftItemType
  {
    Item = 0,
    Cargo = 1,
  }
}