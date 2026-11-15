namespace Vela.Events
{
  // Keyed by the stall building's entity id.
  [ConvergeDb]
  public record BitcraftBarterStallState(
    string Id
  ) : BitcraftEventBase(Id);
}
