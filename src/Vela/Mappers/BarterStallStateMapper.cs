using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class BarterStallStateMapper : MappedDbEntityBase<BarterStallState, BitcraftBarterStallState>
  {
    public override BitcraftBarterStallState Map(BarterStallState entity) =>
        new(Id: entity.EntityId.ToString());
  }
}
