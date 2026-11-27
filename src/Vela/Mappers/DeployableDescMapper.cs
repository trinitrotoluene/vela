using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class DeployableDescMapper : MappedDbEntityBase<DeployableDesc, BitcraftDeployableDesc>
  {
    public override BitcraftDeployableDesc Map(
      DeployableDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        Name: entity.Name
      );
  }
}
