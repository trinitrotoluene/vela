using SpacetimeDB.Types;

public sealed class DbConnectionAccessor : IDbConnectionAccessor
{
  private readonly Lock @lock = new();
  private DbConnection? _current;
  private TaskCompletionSource<DbConnection?> _available = new(TaskCreationOptions.RunContinuationsAsynchronously);

  public bool TryGet(out DbConnection? conn)
  {
    using (@lock.EnterScope()) { conn = _current; return conn != null; }
  }

  public Task<DbConnection?> WaitForConnectionAsync(CancellationToken cancellationToken = default)
  {
    Task<DbConnection?> availableTask;
    using (@lock.EnterScope())
    {
      if (_current != null) return Task.FromResult<DbConnection?>(_current);
      availableTask = _available.Task;
    }

    if (!cancellationToken.CanBeCanceled)
      return availableTask;

    return WaitForTaskWithCancellationAsync(availableTask, cancellationToken);
  }

  private static async Task<DbConnection?> WaitForTaskWithCancellationAsync(Task<DbConnection?> task, CancellationToken ct)
  {
    var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, ct)).ConfigureAwait(false);
    if (completed == task)
      return await task.ConfigureAwait(false);

    ct.ThrowIfCancellationRequested();
    return null;
  }

  public void SetConnection(DbConnection? conn)
  {
    TaskCompletionSource<DbConnection?>? toComplete = null;
    TaskCompletionSource<DbConnection?> newTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    using (@lock.EnterScope())
    {
      toComplete = _available;
      _current = conn;
      _available = newTcs;
    }

    toComplete?.TrySetResult(conn);
  }
}