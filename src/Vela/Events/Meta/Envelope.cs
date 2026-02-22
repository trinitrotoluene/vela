namespace Vela.Events
{
  public record Envelope<T>(
    EnvelopeVersion Version,
    string Module,
    T Entity,
    string CallerIdentity,
    string Reducer
  );
}