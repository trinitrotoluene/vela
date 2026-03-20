using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ClaimTechDescMapper : MappedDbEntityBase<ClaimTechDesc, BitcraftClaimTechDesc>
  {
    public override BitcraftClaimTechDesc Map(
      ClaimTechDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        Name: entity.Name,
        Tier: entity.Tier,
        TechType: entity.TechType.ToString()
      );
  }
}
