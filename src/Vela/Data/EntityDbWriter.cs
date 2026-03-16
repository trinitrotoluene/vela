using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Polly;
using Polly.Retry;
using Vela.Events;

namespace Vela.Data;

public class EntityDbWriter : IEntityDbWriter
{
  private readonly IDbContextFactory<VelaDbContext> _factory;
  private readonly ILogger<EntityDbWriter> _logger;
  private readonly ConcurrentDictionary<Type, EntitySqlDefinition> _definitions = new();
  private readonly ConcurrentDictionary<(Type Type, string Id), BufferedWrite> _writeBuffer = new();
  private readonly ResiliencePipeline _dbRetryPipeline;

  private PeriodicTimer? _flushTimer;
  private CancellationTokenSource? _flushCts;
  private Task? _flushTask;

  public EntityDbWriter(
    IDbContextFactory<VelaDbContext> factory,
    ILogger<EntityDbWriter> logger)
  {
    _factory = factory;
    _logger = logger;
    _dbRetryPipeline = new ResiliencePipelineBuilder()
      .AddRetry(new RetryStrategyOptions
      {
        MaxRetryAttempts = 4,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromSeconds(1),
        UseJitter = true,
        ShouldHandle = new PredicateBuilder().Handle<NpgsqlException>(ex =>
          ex.SqlState is "53300" or "53000" or "40001" or "08000" or "08001" or "08003" or "08004" or "08006"),
        OnRetry = args =>
        {
          _logger.LogWarning(args.Outcome.Exception,
            "Retrying DB operation (attempt {Attempt}) after {Delay}s",
            args.AttemptNumber + 1, args.RetryDelay.TotalSeconds);
          return ValueTask.CompletedTask;
        }
      })
      .Build();
  }

  public async Task PopulateAsync(Type entityType, string module, IReadOnlyList<BitcraftEventBase> entities)
  {
    var def = GetOrBuildDefinition(entityType);

    try
    {
      await _dbRetryPipeline.ExecuteAsync(async ct =>
      {
        await using var dbContext = await _factory.CreateDbContextAsync(ct);
        var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        // Delete stale rows
        await using (var cmd = new NpgsqlCommand(
          $"DELETE FROM {def.TableName} WHERE \"module\" = @module AND \"id\" != ALL(@ids)", conn))
        {
          cmd.Parameters.AddWithValue("@module", module);
          cmd.Parameters.AddWithValue("@ids", entities.Select(e => e.Id).ToArray());
          var deleted = await cmd.ExecuteNonQueryAsync(ct);
          if (deleted > 0)
            _logger.LogInformation("Deleted {Count} stale {Type} rows for module {Module}", deleted, entityType.Name, module);
        }

        // Bulk upsert on same connection
        if (entities.Count > 0)
          await BulkUpsertOnConnectionAsync(conn, entityType, entities.ToList(), ct);
      });

      _logger.LogInformation("Populated {Count} {Type} entities for module {Module}", entities.Count, entityType.Name, module);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to populate {Count} {Type} entities for module {Module}", entities.Count, entityType.Name, module);
    }
  }

  public void EnqueueUpsert<T>(T entity) where T : BitcraftEventBase
  {
    _writeBuffer[(typeof(T), entity.Id)] = new BufferedWrite(entity, IsDelete: false);
  }

  public void EnqueueDelete<T>(T entity) where T : BitcraftEventBase
  {
    _writeBuffer[(typeof(T), entity.Id)] = new BufferedWrite(entity, IsDelete: true);
  }

  public void StartFlushLoop()
  {
    // Idempotent: stop any existing loop first (synchronous wait since callers may not be async)
    StopFlushLoopAsync().GetAwaiter().GetResult();

    _flushCts = new CancellationTokenSource();
    _flushTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    _flushTask = FlushLoopAsync(_flushCts.Token);

    _logger.LogInformation("Started buffered write flush loop");
  }

  public async Task StopFlushLoopAsync()
  {
    if (_flushCts == null) return;

    await _flushCts.CancelAsync();
    _flushTimer?.Dispose();

    if (_flushTask != null)
    {
      try { await _flushTask; }
      catch (OperationCanceledException) { }
    }

    _flushCts.Dispose();
    _flushCts = null;
    _flushTimer = null;
    _flushTask = null;

    // Final flush to drain any remaining buffered writes
    await FlushBufferAsync();

    _logger.LogInformation("Stopped buffered write flush loop");
  }

  private async Task FlushLoopAsync(CancellationToken ct)
  {
    try
    {
      while (await _flushTimer!.WaitForNextTickAsync(ct))
      {
        await FlushBufferAsync();
      }
    }
    catch (OperationCanceledException) { }
  }

  private async Task FlushBufferAsync()
  {
    // Snapshot and clear the buffer (done once, outside retry)
    var items = new List<(Type EntityType, BufferedWrite Write)>(_writeBuffer.Count);
    foreach (var kvp in _writeBuffer)
    {
      if (_writeBuffer.TryRemove(kvp.Key, out var write))
        items.Add((kvp.Key.Type, write));
    }

    if (items.Count == 0) return;

    try
    {
      // Retry wraps only the DB write — snapshot is not re-taken on retry
      await _dbRetryPipeline.ExecuteAsync(async ct =>
      {
        await using var dbContext = await _factory.CreateDbContextAsync(ct);
        var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        var groups = items.GroupBy(i => (i.EntityType, i.Write.IsDelete));

        foreach (var group in groups)
        {
          var entities = group.Select(i => i.Write.Entity).ToList();

          if (group.Key.IsDelete)
          {
            var def = GetOrBuildDefinition(group.Key.EntityType);
            foreach (var entity in entities)
            {
              await using var cmd = new NpgsqlCommand(def.DeleteSql, conn);
              cmd.Parameters.AddWithValue("@p_id", entity.Id);
              cmd.Parameters.AddWithValue("@p_module", entity.Module);
              await cmd.ExecuteNonQueryAsync(ct);
            }
          }
          else
          {
            await BulkUpsertOnConnectionAsync(conn, group.Key.EntityType, entities, ct);
          }
        }
      });

      _logger.LogDebug("Flushed {Count} buffered writes", items.Count);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to flush {Count} buffered writes", items.Count);
    }
  }

  private async Task BulkUpsertOnConnectionAsync(
    NpgsqlConnection conn, Type entityType, List<BitcraftEventBase> entities,
    CancellationToken ct = default)
  {
    var def = GetOrBuildDefinition(entityType);
    const int batchSize = 500;

    for (var offset = 0; offset < entities.Count; offset += batchSize)
    {
      var batch = entities.Skip(offset).Take(batchSize).ToList();

      var valueClauses = new List<string>();
      await using var cmd = new NpgsqlCommand();
      cmd.Connection = conn;

      for (var i = 0; i < batch.Count; i++)
      {
        var paramNames = def.Columns.Select((_, colIdx) => $"@p_{colIdx}_{i}").ToArray();
        valueClauses.Add($"({string.Join(", ", paramNames)})");

        for (var colIdx = 0; colIdx < def.Columns.Length; colIdx++)
        {
          var col = def.Columns[colIdx];
          var value = col.Getter(batch[i]);
          AddTypedParameter(cmd, $"@p_{colIdx}_{i}", value, col);
        }
      }

      cmd.CommandText = $"""
        INSERT INTO {def.TableName} ({def.ColumnList})
        VALUES {string.Join(", ", valueClauses)}
        ON CONFLICT (id) DO UPDATE SET {def.UpdateSet}
        """;

      await cmd.ExecuteNonQueryAsync(ct);
    }
  }

  private EntitySqlDefinition GetOrBuildDefinition(Type type)
  {
    return _definitions.GetOrAdd(type, BuildDefinition);
  }

  private EntitySqlDefinition BuildDefinition(Type type)
  {
    using var dbContext = _factory.CreateDbContext();
    var entityType = dbContext.Model.FindEntityType(type)
      ?? throw new InvalidOperationException($"Type {type.Name} is not registered in VelaDbContext");

    var tableName = entityType.GetTableName()!;
    var schema = entityType.GetSchema();
    var fullTableName = schema != null ? $"\"{schema}\".\"{tableName}\"" : $"\"{tableName}\"";

    // Collect all mapped columns
    var columns = new List<ColumnDef>();

    foreach (var property in entityType.GetProperties())
    {
      var columnName = property.GetColumnName()!;
      var clrProperty = property.PropertyInfo;
      if (clrProperty == null) continue;

      var converter = property.GetValueConverter();
      var storeType = property.GetColumnType();
      var getter = CompileGetter(clrProperty);

      columns.Add(new ColumnDef(columnName, getter, converter, storeType));
    }

    var columnNames = columns.Select(c => $"\"{c.ColumnName}\"").ToArray();
    var updateParts = columns
      .Where(c => c.ColumnName != "id")
      .Select(c => $"\"{c.ColumnName}\" = EXCLUDED.\"{c.ColumnName}\"")
      .ToArray();

    var deleteSql = $"DELETE FROM {fullTableName} WHERE id = @p_id AND \"module\" = @p_module";

    return new EntitySqlDefinition(
      DeleteSql: deleteSql,
      TableName: fullTableName,
      ColumnList: string.Join(", ", columnNames),
      UpdateSet: string.Join(", ", updateParts),
      Columns: [.. columns]
    );
  }

  private static Func<object, object?> CompileGetter(PropertyInfo property)
  {
    var param = Expression.Parameter(typeof(object), "obj");
    var cast = Expression.Convert(param, property.DeclaringType!);
    var access = Expression.Property(cast, property);
    var boxed = Expression.Convert(access, typeof(object));
    return Expression.Lambda<Func<object, object?>>(boxed, param).Compile();
  }

  private static void AddTypedParameter(NpgsqlCommand cmd, string paramName, object? value, ColumnDef col)
  {
    // Apply value converter (handles JSONB serialization for arrays and nested types)
    if (col.Converter != null && value != null)
      value = col.Converter.ConvertToProvider(value);

    // Handle CLR types that Npgsql can't write directly:
    // - Enums without a converter (or where GetValueConverter() returned null)
    // - Unsigned integers (PostgreSQL has no unsigned types)
    if (value != null)
    {
      var valueType = value.GetType();
      if (valueType.IsEnum)
        value = value.ToString()!;
      else if (value is uint u)
        value = (long)u;
      else if (value is ulong ul)
        value = (decimal)ul;
      else if (value is ushort us)
        value = (int)us;
    }

    var param = new NpgsqlParameter(paramName, value ?? DBNull.Value);

    if (string.Equals(col.StoreType, "jsonb", StringComparison.OrdinalIgnoreCase))
      param.NpgsqlDbType = NpgsqlDbType.Jsonb;

    cmd.Parameters.Add(param);
  }

  private readonly record struct BufferedWrite(BitcraftEventBase Entity, bool IsDelete);

  private record ColumnDef(
    string ColumnName,
    Func<object, object?> Getter,
    ValueConverter? Converter,
    string? StoreType
  );

  private record EntitySqlDefinition(
    string DeleteSql,
    string TableName,
    string ColumnList,
    string UpdateSet,
    ColumnDef[] Columns
  );
}
