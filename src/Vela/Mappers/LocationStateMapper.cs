using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class LocationStateMapper : MappedDbEntityBase<LocationState, BitcraftLocationState>
  {
    public override BitcraftLocationState Map(
      LocationState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        X: entity.X,
        Z: entity.Z
      );
  }
}