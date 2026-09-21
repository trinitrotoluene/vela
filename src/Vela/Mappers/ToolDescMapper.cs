using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ToolDescMapper : MappedDbEntityBase<ToolDesc, BitcraftToolDesc>
  {
    public override BitcraftToolDesc Map(
      ToolDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        ItemId: entity.ItemId,
        ToolType: entity.ToolType,
        Level: entity.Level,
        Power: entity.Power
      );
  }
}
