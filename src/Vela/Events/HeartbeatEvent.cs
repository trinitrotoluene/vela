namespace Vela.Events
{
  public record HeartbeatEvent(
    string Application,
    DateTime PublishedAt,
    int Seq
  ) : GenericEventBase;
}