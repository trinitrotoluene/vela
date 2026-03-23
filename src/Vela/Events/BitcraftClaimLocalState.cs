namespace Vela.Events
{
  [GlobalEntity, Storage(StorageTarget.Database)]
  public record BitcraftClaimLocalState(
    string Id,
    BitcraftLocation? Location,
    int Supplies,
    uint Treasury,
    int BuildingDescriptionId
  ) : BitcraftEventBase(Id);

  public record BitcraftLocation(
    int X,
    int Z,
    uint Dimension
  );
}