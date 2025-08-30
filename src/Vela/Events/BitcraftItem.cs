using System.Text.Json.Serialization;

namespace Vela.Events
{
  [GlobalEntity]
  public record BitcraftItem(
    string Id,
    string Name,
    string Description,
    int Volume,
    int Tier,
    BitcraftItemRarity Rarity,
    int ItemListId,
    bool HasCompendiumEntry
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
}