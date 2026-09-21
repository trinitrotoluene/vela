using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ToolTypeDescMapper : MappedDbEntityBase<ToolTypeDesc, BitcraftToolTypeDesc>
  {
    public override BitcraftToolTypeDesc Map(
      ToolTypeDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        Name: entity.Name,
        SkillId: entity.SkillId
      );
  }
}
