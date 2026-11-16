using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class DimensionDescriptionStateMapper : MappedDbEntityBase<DimensionDescriptionState, BitcraftDimensionDescriptionState>
  {
    public override BitcraftDimensionDescriptionState Map(DimensionDescriptionState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          DimensionNetworkId: entity.DimensionNetworkEntityId.ToString(),
          DimensionId: entity.DimensionId
        );
  }
}
