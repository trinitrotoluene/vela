namespace Vela.Events
{
  [Postgres]
  public record BitcraftClaimTechDesc(
    string Id,
    string Name,
    int Tier,
    string TechType
  ) : BitcraftEventBase(Id);
}
