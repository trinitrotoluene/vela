using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class DeployableStateMapper : MappedDbEntityBase<DeployableStateV2, BitcraftDeployableState>
  {
    public override BitcraftDeployableState Map(DeployableStateV2 entity) =>
        new(
          Id: entity.EntityId.ToString(),
          OwnerId: entity.OwnerId.ToString(),
          ClaimEntityId: entity.ClaimEntityId.ToString(),
          Hidden: entity.Hidden
        );
  }
}
