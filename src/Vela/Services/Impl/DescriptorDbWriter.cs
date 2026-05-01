using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Polly;
using Polly.Retry;
using Vela.Data;
using Vela.Events;
using Vela.Services.Contracts;

namespace Vela.Services.Impl;

/// <summary>
/// Postgres writer for static descriptor entities (items, recipes, building descs, etc.).
/// Descriptor data is global (shared across all SpacetimeDB modules) and changes only on
/// game deploys (~biweekly). Multiple Vela instances populate the same dataset on startup,
/// so PopulateAsync uses a Postgres advisory lock to ensure exactly one writer per type;
/// the lock is session-scoped, so Postgres releases it automatically if the holder dies.
/// </summary>
public class DescriptorDbWriter : IDescriptorDbWriter
{
  private readonly IDbContextFactory<VelaDbContext> _factory;
  private readonly ILogger<DescriptorDbWriter> _logger;
  private readonly ConcurrentDictionary<Type, EntitySqlDefinition> _definitions = new();
  private readonly ResiliencePipeline _dbRetryPipeline;

  public DescriptorDbWriter(IDbContextFactory<VelaDbContext> factory, ILogger<DescriptorDbWriter> logger)
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
            "Retrying descriptor DB operation (attempt {Attempt}) after {Delay}s",
            args.AttemptNumber + 1, args.RetryDelay.TotalSeconds);
          return ValueTask.CompletedTask;
        }
      })
      .Build();
  }

  public async Task PopulateAsync(Type entityType, IReadOnlyList<BitcraftEventBase> entities)
  {
    var def = GetOrBuildDefinition(entityType);
    var lockKey = ComputeAdvisoryLockKey(def.TableName);

    try
    {
      await _dbRetryPipeline.ExecuteAsync(async ct =>
      {
        await using var dbContext = await _factory.CreateDbContextAsync(ct);
        var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        // Session-level advisory lock: only one Vela instance populates a given descriptor
        // type at a time. Postgres auto-releases the lock when the session ends — including
        // on hard process death — so a crash here cannot leave the lock stuck.
        bool acquired;
        await using (var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", conn))
        {
          cmd.Parameters.AddWithValue("@key", lockKey);
          acquired = (bool)(await cmd.ExecuteScalarAsync(ct))!;
        }

        if (!acquired)
        {
          _logger.LogInformation(
            "Skipping {Type} populate — another instance holds the advisory lock",
            entityType.Name);
          return;
        }

        try
        {
          await using var tx = await conn.BeginTransactionAsync(ct);

          if (entities.Count > 0)
            await BulkUpsertAsync(conn, tx, def, entities, ct);

          // Prune rows that aren't in the incoming set (empty input → wipe table).
          await using (var cmd = new NpgsqlCommand(
            $"DELETE FROM {def.TableName} WHERE id <> ALL(@ids)", conn, tx))
          {
            cmd.Parameters.AddWithValue("@ids", entities.Select(e => e.Id).ToArray());
            var deleted = await cmd.ExecuteNonQueryAsync(ct);
            if (deleted > 0)
              _logger.LogInformation(
                "Pruned {Count} stale {Type} descriptor rows", deleted, entityType.Name);
          }

          await tx.CommitAsync(ct);
        }
        finally
        {
          await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", conn);
          cmd.Parameters.AddWithValue("@key", lockKey);
          await cmd.ExecuteScalarAsync(ct);
        }

        _logger.LogInformation(
          "Populated {Count} {Type} descriptors", entities.Count, entityType.Name);
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to populate {Count} {Type} descriptors", entities.Count, entityType.Name);
    }
  }

  public void EnqueueUpsert<T>(T entity) where T : BitcraftEventBase
  {
    // Synchronous fire-and-forget: descriptor changes are extremely rare during normal gameplay.
    _ = Task.Run(async () =>
    {
      try
      {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.FindAsync(typeof(T), entity.Id);
        if (existing != null)
          db.Remove(existing);
        db.Add(entity);
        await db.SaveChangesAsync();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to upsert {Type} descriptor {Id}", typeof(T).Name, entity.Id);
      }
    });
  }

  public void EnqueueDelete<T>(T entity) where T : BitcraftEventBase
  {
    _ = Task.Run(async () =>
    {
      try
      {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.FindAsync(typeof(T), entity.Id);
        if (existing != null)
        {
          db.Remove(existing);
          await db.SaveChangesAsync();
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to delete {Type} descriptor {Id}", typeof(T).Name, entity.Id);
      }
    });
  }

  private static async Task BulkUpsertAsync(
    NpgsqlConnection conn, NpgsqlTransaction tx, EntitySqlDefinition def,
    IReadOnlyList<BitcraftEventBase> entities, CancellationToken ct)
  {
    // Staging table dropped automatically on commit/rollback. Name includes the target table
    // so concurrent populates on a shared connection can't collide on a single _vela_staging.
    await using (var cmd = new NpgsqlCommand(
      $"CREATE TEMP TABLE {def.StagingTableName} (LIKE {def.TableName}) ON COMMIT DROP", conn, tx))
    {
      await cmd.ExecuteNonQueryAsync(ct);
    }

    await using (var writer = await conn.BeginBinaryImportAsync(
      $"COPY {def.StagingTableName} ({def.ColumnList}) FROM STDIN (FORMAT BINARY)", ct))
    {
      foreach (var entity in entities)
      {
        await writer.StartRowAsync(ct);
        foreach (var col in def.Columns)
        {
          var value = ConvertValue(col.Getter(entity), col);
          if (value == null)
            await writer.WriteNullAsync(ct);
          else
            await writer.WriteAsync(value, col.DbType, ct);
        }
      }
      await writer.CompleteAsync(ct);
    }

    await using (var cmd = new NpgsqlCommand(
      $"""
      INSERT INTO {def.TableName} ({def.ColumnList})
      SELECT {def.ColumnList} FROM {def.StagingTableName}
      ON CONFLICT (id) DO UPDATE SET {def.UpdateSet}
      """, conn, tx))
    {
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

    var columns = new List<ColumnDef>();
    foreach (var property in entityType.GetProperties())
    {
      var columnName = property.GetColumnName()!;
      var clrProperty = property.PropertyInfo;
      if (clrProperty == null) continue;

      var converter = property.GetValueConverter();
      var storeType = property.GetColumnType();
      var getter = CompileGetter(clrProperty);

      columns.Add(new ColumnDef(columnName, getter, converter, storeType, MapStoreType(storeType)));
    }

    var columnNames = columns.Select(c => $"\"{c.ColumnName}\"").ToArray();
    var updateParts = columns
      .Where(c => c.ColumnName != "id")
      .Select(c => $"\"{c.ColumnName}\" = EXCLUDED.\"{c.ColumnName}\"")
      .ToArray();

    return new EntitySqlDefinition(
      TableName: fullTableName,
      StagingTableName: $"_vela_staging_{tableName}",
      ColumnList: string.Join(", ", columnNames),
      UpdateSet: string.Join(", ", updateParts),
      Columns: [.. columns]
    );
  }

  // Stable bigint key derived from the table name. Stable across processes and code revisions
  // so concurrent instances (including across rolling deploys) collide on the right lock.
  private static long ComputeAdvisoryLockKey(string tableName)
  {
    Span<byte> hash = stackalloc byte[20];
    SHA1.HashData(Encoding.UTF8.GetBytes(tableName), hash);
    return BitConverter.ToInt64(hash[..8]);
  }

  private static Func<object, object?> CompileGetter(PropertyInfo property)
  {
    var param = Expression.Parameter(typeof(object), "obj");
    var cast = Expression.Convert(param, property.DeclaringType!);
    var access = Expression.Property(cast, property);
    var boxed = Expression.Convert(access, typeof(object));
    return Expression.Lambda<Func<object, object?>>(boxed, param).Compile();
  }

  private static object? ConvertValue(object? value, ColumnDef col)
  {
    if (col.Converter != null && value != null)
      value = col.Converter.ConvertToProvider(value);

    if (value == null) return null;

    var valueType = value.GetType();
    if (valueType.IsEnum)
      return value.ToString()!;
    if (value is uint u)
      return (long)u;
    if (value is ulong ul)
      return (decimal)ul;
    if (value is ushort us)
      return (int)us;

    return value;
  }

  private static NpgsqlDbType MapStoreType(string? storeType)
  {
    if (string.IsNullOrEmpty(storeType)) return NpgsqlDbType.Text;

    var s = storeType.ToLowerInvariant();
    if (s == "jsonb") return NpgsqlDbType.Jsonb;
    if (s == "json") return NpgsqlDbType.Json;
    if (s is "integer" or "int4") return NpgsqlDbType.Integer;
    if (s is "bigint" or "int8") return NpgsqlDbType.Bigint;
    if (s is "smallint" or "int2") return NpgsqlDbType.Smallint;
    if (s is "boolean" or "bool") return NpgsqlDbType.Boolean;
    if (s is "double precision" or "float8") return NpgsqlDbType.Double;
    if (s is "real" or "float4") return NpgsqlDbType.Real;
    if (s == "numeric" || s.StartsWith("numeric(")) return NpgsqlDbType.Numeric;
    if (s == "uuid") return NpgsqlDbType.Uuid;
    if (s is "timestamp with time zone" or "timestamptz") return NpgsqlDbType.TimestampTz;
    if (s is "timestamp without time zone" or "timestamp") return NpgsqlDbType.Timestamp;
    if (s == "date") return NpgsqlDbType.Date;
    if (s == "bytea") return NpgsqlDbType.Bytea;
    return NpgsqlDbType.Text;
  }

  private record ColumnDef(
    string ColumnName,
    Func<object, object?> Getter,
    ValueConverter? Converter,
    string? StoreType,
    NpgsqlDbType DbType
  );

  private record EntitySqlDefinition(
    string TableName,
    string StagingTableName,
    string ColumnList,
    string UpdateSet,
    ColumnDef[] Columns
  );
}
