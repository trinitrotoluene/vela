using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ProgressiveActionStateMapper : MappedDbEntityBase<ProgressiveActionState, BitcraftProgressiveAction>
  {
    public override BitcraftProgressiveAction Map(ProgressiveActionState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          RecipeId: entity.RecipeId.ToString(),
          CraftCount: entity.CraftCount,
          Progress: entity.Progress
        );
  }
}