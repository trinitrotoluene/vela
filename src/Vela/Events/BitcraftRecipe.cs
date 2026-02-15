namespace Vela.Events
{
  [GlobalEntity, Storage(StorageTarget.Database)]
  public record BitcraftRecipe(
    string Id,
    string NameFormatString,
    BuildingRequirement? BuildingRequirement,
    LevelRequirement[] LevelRequirements,
    ToolRequirement[] ToolRequirements,
    ItemStack[] ConsumedItemStacks,
    ItemStack[] ProducedItemStacks,
    bool IsPassive,
    int ActionsRequired
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
    int Level
  );

  public record ItemStack(
    string ItemId,
    int Quantity
  );
}