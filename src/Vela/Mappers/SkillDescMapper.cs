using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class SkillDescMapper : MappedDbEntityBase<SkillDesc, BitcraftSkillDesc>
  {
    public override BitcraftSkillDesc Map(
      SkillDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        SkillType: entity.SkillType,
        Name: entity.Name,
        Title: entity.Title,
        SkillCategory: entity.SkillCategory.ToString(),
        MaxLevel: entity.MaxLevel
      );
  }
}
