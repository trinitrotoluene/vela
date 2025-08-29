using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class EmpireStateMapper : MappedDbEntityBase<EmpireState, BitcraftEmpireState>
  {
    public override BitcraftEmpireState Map(
      EmpireState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        Name: entity.Name,
        ShardTreasury: (int)entity.ShardTreasury
      );
  }
}