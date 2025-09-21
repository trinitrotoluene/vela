namespace Vela.Events
{
  [GlobalEntity]
  public record BitcraftClaimLocalState(
    string Id,
    BitcraftLocation? Location,
    int Supplies,
    uint Treasury
  ) : BitcraftEventBase(Id);

  public record BitcraftLocation(
    int X,
    int Z,
    uint Dimension
  );
}