using Vela.Events;

namespace Vela.Data;

public interface IEntityDbWriter
{
  Task UpsertAsync<T>(T entity) where T : BitcraftEventBase;
  Task DeleteAsync<T>(T entity) where T : BitcraftEventBase;
  Task BulkUpsertAsync<T>(IReadOnlyList<T> entities) where T : BitcraftEventBase;
  Task DeleteStaleAsync<T>(string module, IReadOnlyList<string> currentIds) where T : BitcraftEventBase;
  void EnqueueUpsert<T>(T entity) where T : BitcraftEventBase;
  void EnqueueDelete<T>(T entity) where T : BitcraftEventBase;
  void StartFlushLoop();
  Task StopFlushLoopAsync();
}
