using Vela.Events;

namespace Vela.Services.Contracts;

public interface IDescriptorDbWriter
{
    Task PopulateAsync(Type entityType, string module, IReadOnlyList<BitcraftEventBase> entities);
    void EnqueueUpsert<T>(T entity) where T : BitcraftEventBase;
    void EnqueueDelete<T>(T entity) where T : BitcraftEventBase;
}
