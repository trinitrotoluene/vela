using SpacetimeDB.Types;
using Vela.Events;

public interface IEventSubscriber
{
  void SubscribeToChanges(DbConnection conn);
  Dictionary<(Type OutputType, string CacheKey), List<BitcraftEventBase>> SnapshotBaseCaches(DbConnection conn);
  Task PopulateBaseCachesAsync(Dictionary<(Type OutputType, string CacheKey), List<BitcraftEventBase>> merged);
  void PublishSystemEvent<TEvent>(TEvent e) where TEvent : GenericEventBase;
}