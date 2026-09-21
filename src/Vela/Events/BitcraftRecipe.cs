namespace Vela.Events
{
  [Postgres]
  public record BitcraftRecipe(
    string Id,
    string NameFormatString,
    BuildingRequirement? BuildingRequirement,
    LevelRequirement[] LevelRequirements,
    ToolRequirement[] ToolRequirements,
    ItemStack[] ConsumedItemStacks,
    ItemStack[] ProducedItemStacks,
    bool IsPassive,
    int ActionsRequired,
    float TimeRequirement,
    float StaminaRequirement,
    int RequiredClaimTechId,
    int[] RequiredKnowledges,
    int[] BlockingKnowledges
  ) : BitcraftEventBase(Id);

  public record BuildingRequirement(
    int BuildingType,
    int Tier
  );
  public record LevelRequirement(
    int SkillId,
    int Level
  );

  public record ToolRequirement(
    int ToolType,
    int Level,
    int Power
  );

  public record ItemStack(
    string ItemId,
    int Quantity,
    BitcraftItemType ItemType
  );
}