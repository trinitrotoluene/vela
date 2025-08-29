using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class PublicProgressiveActionStateMapper : MappedDbEntityBase<PublicProgressiveActionState, BitcraftPublicProgressiveAction>
  {
    public override BitcraftPublicProgressiveAction Map(PublicProgressiveActionState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          BuildingEntityId: entity.BuildingEntityId.ToString(),
          OwnerEntityId: entity.OwnerEntityId.ToString()
        );
  }
}