namespace Vela.Events
{
  public record UpdateEnvelope<T>(
    EnvelopeVersion Version,
    string Module,
    T OldEntity,
    T NewEntity
  );
}