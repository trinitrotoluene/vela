using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class BuildingTypeDescMapper : MappedDbEntityBase<BuildingTypeDesc, BitcraftBuildingTypeDesc>
  {
    public override BitcraftBuildingTypeDesc Map(
      BuildingTypeDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        Name: entity.Name,
        Category: entity.Category.ToString()
      );
  }
}
