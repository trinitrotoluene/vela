namespace Vela.Events
{
  [Postgres]
  public record BitcraftSkillDesc(
    string Id,
    int SkillType,
    string Name,
    string Title,
    string SkillCategory,
    int MaxLevel
  ) : BitcraftEventBase(Id);
}
