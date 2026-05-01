using SpacetimeDB.Types;
using Vela.Events;

public interface IEventSubscriber
{
  void RegisterAllEventHandlers(DbConnection conn);
  Dictionary<Type, List<BitcraftEventBase>> DrainSnapshotBuffer();
  Task PopulateBaseCachesAsync(Dictionary<Type, List<BitcraftEventBase>> merged);
  void PublishSystemEvent<TEvent>(TEvent e) where TEvent : GenericEventBase;
}
