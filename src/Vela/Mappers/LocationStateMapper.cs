using SpacetimeDB.Types;

namespace Vela.Mappers
{
  public class LocationStateMapper : MappedDbEntityBase<LocationState, BitcraftLocationState>
  {
    public override BitcraftLocationState Map(
      LocationState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        X: entity.X,
        Z: entity.Z,
        Dimension: entity.Dimension
      );
  }
}