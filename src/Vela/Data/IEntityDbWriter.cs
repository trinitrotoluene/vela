using Vela.Events;

namespace Vela.Data;

public interface IEntityDbWriter
{
  Task PopulateAsync(Type entityType, string module, IReadOnlyList<BitcraftEventBase> entities);
  void EnqueueUpsert<T>(T entity) where T : BitcraftEventBase;
  void EnqueueDelete<T>(T entity) where T : BitcraftEventBase;
  void StartFlushLoop();
  Task StopFlushLoopAsync();
}
