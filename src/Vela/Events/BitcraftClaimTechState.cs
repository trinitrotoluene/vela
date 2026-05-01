namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftClaimTechState(
    string Id,
    int[] Learned,
    int Researching
  ) : BitcraftEventBase(Id);
}
