using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class DimensionNetworkStateMapper : MappedDbEntityBase<DimensionNetworkState, BitcraftDimensionNetworkState>
  {
    public override BitcraftDimensionNetworkState Map(DimensionNetworkState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          BuildingId: entity.BuildingId.ToString(),
          ClaimEntityId: entity.ClaimEntityId.ToString()
        );
  }
}
