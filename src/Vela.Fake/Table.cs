using Convergence.Client;

namespace Vela.Fake;

/// <summary>Buffers writes on one connection and flushes them in frames, as Vela's writer does.</summary>
public sealed class Writer(ConvergenceClient client, CancellationToken ct)
{
    private const int FlushEvery = 512;
    private readonly ConvergenceBatch _batch = client.Batch();
    private int _pending;

    public ConvergenceClient Client => client;

    public async ValueTask AssertAsync<T>(KindHandle<T> kind, T entity) where T : struct, IConvergenceEntity<T>
    {
        await _batch.AssertAsync(kind, entity);
        if (++_pending >= FlushEvery) await FlushAsync();
    }

    public async ValueTask RetractAsync<T>(KindHandle<T> kind, ReadOnlyMemory<byte> entityId) where T : struct, IConvergenceEntity<T>
    {
        await _batch.RetractAsync(kind, entityId);
        if (++_pending >= FlushEvery) await FlushAsync();
    }

    public async ValueTask FlushAsync()
    {
        ct.ThrowIfCancellationRequested();
        if (_pending == 0) return;
        _pending = 0;
        await client.FlushAsync(ct);
    }
}

public interface ITable
{
    string Name { get; }
    int Count { get; }
    Task RegisterAsync(ConvergenceClient client, CancellationToken ct);
    Task AssertAllAsync(Writer writer);
}

/// <summary>The fake's own copy of one kind, so it can re-publish it and pick rows to change.</summary>
public sealed class Table<T> : ITable where T : struct, IConvergenceEntity<T>
{
    private readonly Dictionary<ulong, T> _rows = new();
    private readonly List<ulong> _keys = new();
    private readonly Dictionary<ulong, int> _index = new();
    private KindHandle<T>? _kind;

    public string Name => T.KindName;
    public int Count => _rows.Count;
    public IReadOnlyList<ulong> Keys => _keys;
    public T this[ulong key] => _rows[key];
    public bool Contains(ulong key) => _rows.ContainsKey(key);

    public async Task RegisterAsync(ConvergenceClient client, CancellationToken ct) =>
        _kind = await client.RegisterKindAsync<T>(ct);

    public async Task AssertAllAsync(Writer writer)
    {
        foreach (var row in _rows.Values) await writer.AssertAsync(_kind!, row);
    }

    /// <summary>Adds or replaces a row without publishing it, for the initial world and between connections.</summary>
    public void Set(ulong key, T row)
    {
        if (_rows.TryAdd(key, row))
        {
            _index[key] = _keys.Count;
            _keys.Add(key);
        }
        else
        {
            _rows[key] = row;
        }
    }

    /// <summary>Removes a row without publishing it.</summary>
    public bool Drop(ulong key, out T row)
    {
        if (!_rows.Remove(key, out row)) return false;
        int at = _index[key];
        ulong last = _keys[^1];
        _keys[at] = last;
        _index[last] = at;
        _keys.RemoveAt(_keys.Count - 1);
        _index.Remove(key);
        return true;
    }

    public ValueTask PutAsync(Writer writer, ulong key, T row)
    {
        Set(key, row);
        return writer.AssertAsync(_kind!, row);
    }

    public async ValueTask RemoveAsync(Writer writer, ulong key)
    {
        if (Drop(key, out var row)) await writer.RetractAsync(_kind!, row.EntityId);
    }

    /// <summary>Retracts a row already dropped locally.</summary>
    public ValueTask RetractAsync(Writer writer, T row) => writer.RetractAsync(_kind!, row.EntityId);

    /// <summary>Publishes a row without keeping it: events, which Vela never re-publishes.</summary>
    public ValueTask EmitAsync(Writer writer, T row) => writer.AssertAsync(_kind!, row);

    public bool TryPick(Random rng, out ulong key)
    {
        key = _keys.Count == 0 ? 0 : _keys[rng.Next(_keys.Count)];
        return _keys.Count > 0;
    }
}
