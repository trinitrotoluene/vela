namespace Vela.Events
{
  [GlobalEntity, Storage(StorageTarget.Database)]
  public record BitcraftClaimState(
    string Id,
    string OwnerPlayerId,
    string OwnerBuildingId,
    string Name,
    bool IsNeutral
  ) : BitcraftEventBase(Id);
}