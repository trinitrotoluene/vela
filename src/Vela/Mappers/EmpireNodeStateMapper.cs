using System.Security.Cryptography.X509Certificates;
using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class EmpireNodeStateMapper : MappedDbEntityBase<EmpireNodeState, BitcraftEmpireNodeState>
  {
    public override BitcraftEmpireNodeState Map(
      EmpireNodeState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        EmpireId: entity.EmpireEntityId.ToString(),
        IsActive: entity.Active,
        ChunkIndex: entity.ChunkIndex,
        Dimension: entity.Location.Dimension,
        LocationX: entity.Location.X,
        LocationZ: entity.Location.Z,
        Upkeep: entity.Upkeep,
        Energy: entity.Energy
      );
  }
}