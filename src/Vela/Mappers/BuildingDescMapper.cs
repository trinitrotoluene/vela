using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class BuildingDescMapper : MappedDbEntityBase<BuildingDesc, BitcraftBuildingDesc>
  {
    public override BitcraftBuildingDesc Map(
      BuildingDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        Name: entity.Name,
        Description: entity.Description
      );
  }
}