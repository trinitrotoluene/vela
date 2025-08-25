using stdb = SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class CraftingRecipeDescMapper : MappedDbEntityBase<stdb::CraftingRecipeDesc, BitcraftRecipe>
  {
    public override string TopicName => "bitcraft.recipe";

    public override BitcraftRecipe Map(
      stdb::CraftingRecipeDesc entity
    ) => new(
        Id: entity.Id,
        NameFormatString: entity.Name,
        BuildingRequirement: entity.BuildingRequirement != null
          ? new(BuildingType: entity.BuildingRequirement!.BuildingType, Tier: entity.BuildingRequirement!.Tier)
          : null,
        LevelRequirements: [.. entity.LevelRequirements.Select(x => new LevelRequirement(
        Level: x.Level,
        SkillId: x.SkillId
      ))],
        ToolRequirements: [.. entity.ToolRequirements.Select(x => new ToolRequirement(ToolType: x.ToolType, Level: x.Level))],
        ConsumedItemStacks: [.. entity.ConsumedItemStacks.Select(x => new ItemStack(ItemId: x.ItemId, Quantity: x.Quantity))],
        ProducedItemStacks: [.. entity.CraftedItemStacks.Select(x => new ItemStack(ItemId: x.ItemId, Quantity: x.Quantity))],
        IsPassive: entity.IsPassive,
        ActionsRequired: entity.ActionsRequired
      );
  }
}