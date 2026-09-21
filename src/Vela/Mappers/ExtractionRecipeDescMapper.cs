using Sdk = SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ExtractionRecipeDescMapper : MappedDbEntityBase<Sdk::ExtractionRecipeDesc, BitcraftExtractionRecipe>
  {
    public override BitcraftExtractionRecipe Map(
      Sdk::ExtractionRecipeDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        VerbPhrase: entity.VerbPhrase,
        ResourceId: entity.ResourceId,
        CargoId: entity.CargoId,
        LevelRequirements: [.. entity.LevelRequirements.Select(x => new LevelRequirement(SkillId: x.SkillId, Level: x.Level))],
        ToolRequirements: [.. entity.ToolRequirements.Select(x => new ToolRequirement(ToolType: x.ToolType, Level: x.Level, Power: x.Power))],
        ConsumedItemStacks: [.. entity.ConsumedItemStacks.Select(x => new ItemStack(ItemId: x.ItemId.ToString(), Quantity: x.Quantity, ItemType: (BitcraftItemType)x.ItemType))],
        // A probabilistic entry with no stack yields nothing, so there's nothing to record.
        ExtractedItemStacks: [.. entity.ExtractedItemStacks
          .Where(x => x.ItemStack != null)
          .Select(x => new BitcraftProbabilisticItemStack(
            ItemId: x.ItemStack!.ItemId.ToString(),
            Quantity: x.ItemStack.Quantity,
            ItemType: (BitcraftItemType)x.ItemStack.ItemType,
            Probability: x.Probability
          ))],
        TimeRequirement: entity.TimeRequirement,
        StaminaRequirement: entity.StaminaRequirement,
        Range: entity.Range,
        AllowUseHands: entity.AllowUseHands,
        RequiredKnowledges: [.. entity.RequiredKnowledges],
        BlockingKnowledges: [.. entity.BlockingKnowledges]
      );
  }
}
