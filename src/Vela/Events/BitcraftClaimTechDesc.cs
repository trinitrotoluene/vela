namespace Vela.Events
{
  [GlobalEntity, Storage(StorageTarget.Database)]
  public record BitcraftClaimTechDesc(
    string Id,
    string Name,
    int Tier,
    string TechType
  ) : BitcraftEventBase(Id);
}
