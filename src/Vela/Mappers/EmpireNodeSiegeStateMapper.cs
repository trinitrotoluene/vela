using System.Security.Cryptography.X509Certificates;
using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class EmpireNodeSiegeStateMapper : MappedDbEntityBase<EmpireNodeSiegeState, BitcraftEmpireNodeSiegeState>
  {
    public override BitcraftEmpireNodeSiegeState Map(
      EmpireNodeSiegeState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        EmpireId: entity.EmpireEntityId.ToString(),
        IsActive: entity.Active,
        BuildingEntityId: entity.BuildingEntityId.ToString(),
        Energy: entity.Energy
      );
  }
}