using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class SecondaryKnowledgeDescMapper : MappedDbEntityBase<SecondaryKnowledgeDesc, BitcraftSecondaryKnowledgeDesc>
  {
    public override BitcraftSecondaryKnowledgeDesc Map(
      SecondaryKnowledgeDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        Name: entity.Name
      );
  }
}
