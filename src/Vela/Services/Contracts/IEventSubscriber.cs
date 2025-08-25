using SpacetimeDB.Types;

public interface IEventSubscriber
{
  void SubscribeToChanges(DbConnection conn);
}