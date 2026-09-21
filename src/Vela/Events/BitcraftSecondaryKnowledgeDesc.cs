namespace Vela.Events
{
  // Target of RequiredKnowledges/BlockingKnowledges on crafting and extraction recipes.
  [Postgres]
  public record BitcraftSecondaryKnowledgeDesc(
    string Id,
    string Name
  ) : BitcraftEventBase(Id);
}
