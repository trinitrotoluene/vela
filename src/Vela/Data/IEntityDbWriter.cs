using Vela.Events;

namespace Vela.Data;

public interface IEntityDbWriter
{
  Task UpsertAsync<T>(T entity) where T : BitcraftEventBase;
  Task DeleteAsync<T>(T entity) where T : BitcraftEventBase;
  Task BulkUpsertAsync<T>(IReadOnlyList<T> entities) where T : BitcraftEventBase;
}
