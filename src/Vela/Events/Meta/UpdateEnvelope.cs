namespace Vela.Events
{
  public record UpdateEnvelope<T>(
    EnvelopeVersion Version,
    string Module,
    string CallerIdentity,
    string Reducer,
    T OldEntity,
    T NewEntity
  );
}