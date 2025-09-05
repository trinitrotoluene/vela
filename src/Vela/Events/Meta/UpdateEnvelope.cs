namespace Vela.Events
{
  public record UpdateEnvelope<T>(
    EnvelopeVersion Version,
    string Module,
    string CallerIdentity,
    T OldEntity,
    T NewEntity
  );
}