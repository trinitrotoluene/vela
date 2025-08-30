using SpacetimeDB.Types;

public interface IEventSubscriber
{
  void SubscribeToChanges(DbConnection conn);
  Task PopulateBaseCachesAsync(DbConnection conn);
  void PublishSystemEvent<TEvent>(TEvent e) where TEvent : GenericEventBase;
}