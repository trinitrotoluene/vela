using SpacetimeDB.Types;
using Vela.Events;

public interface IEventSubscriber
{
  void RegisterAllEventHandlers(DbConnection conn);
  void BeginStreamingSnapshot();
  Task EndStreamingSnapshotAsync();
  void PublishSystemEvent<TEvent>(TEvent e) where TEvent : GenericEventBase;
}
