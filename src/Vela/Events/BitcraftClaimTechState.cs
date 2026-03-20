namespace Vela.Events
{
  [GlobalEntity, Storage(StorageTarget.Database)]
  public record BitcraftClaimTechState(
    string Id,
    int[] Learned,
    int Researching
  ) : BitcraftEventBase(Id);
}
