namespace Vela.Events
{
  [Postgres]
  public record BitcraftToolTypeDesc(
    string Id,
    string Name,
    int SkillId
  ) : BitcraftEventBase(Id);
}
