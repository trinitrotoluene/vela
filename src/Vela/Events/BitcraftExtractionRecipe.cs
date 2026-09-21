namespace Vela.Events
{
  [Postgres]
  public record BitcraftExtractionRecipe(
    string Id,
    string VerbPhrase,
    int ResourceId,
    int CargoId,
    LevelRequirement[] LevelRequirements,
    ToolRequirement[] ToolRequirements,
    ItemStack[] ConsumedItemStacks,
    BitcraftProbabilisticItemStack[] ExtractedItemStacks,
    float TimeRequirement,
    float StaminaRequirement,
    int Range,
    bool AllowUseHands,
    int[] RequiredKnowledges,
    int[] BlockingKnowledges
  ) : BitcraftEventBase(Id);

  public record BitcraftProbabilisticItemStack(
    string ItemId,
    int Quantity,
    BitcraftItemType ItemType,
    float Probability
  );
}
