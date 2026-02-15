using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Vela.Events;

namespace Vela.Data;

public class EntityDbWriter : IEntityDbWriter
{
  private readonly IDbContextFactory<VelaDbContext> _factory;
  private readonly ILogger<EntityDbWriter> _logger;
  private readonly ConcurrentDictionary<Type, EntitySqlDefinition> _definitions = new();

  public EntityDbWriter(
    IDbContextFactory<VelaDbContext> factory,
    ILogger<EntityDbWriter> logger)
  {
    _factory = factory;
    _logger = logger;
  }

  public async Task UpsertAsync<T>(T entity) where T : BitcraftEventBase
  {
    var def = GetOrBuildDefinition(typeof(T));

    try
    {
      await using var dbContext = await _factory.CreateDbContextAsync();
      var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
      await conn.OpenAsync();

      await using var cmd = new NpgsqlCommand(def.UpsertSql, conn);
      AddParameters(cmd, def, entity);
      await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to upsert {Type} with Id {Id}", typeof(T).Name, entity.Id);
    }
  }

  public async Task DeleteAsync<T>(T entity) where T : BitcraftEventBase
  {
    var def = GetOrBuildDefinition(typeof(T));

    try
    {
      await using var dbContext = await _factory.CreateDbContextAsync();
      var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
      await conn.OpenAsync();

      await using var cmd = new NpgsqlCommand(def.DeleteSql, conn);
      cmd.Parameters.AddWithValue("@p_id", entity.Id);
      await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to delete {Type} with Id {Id}", typeof(T).Name, entity.Id);
    }
  }

  public async Task BulkUpsertAsync<T>(IReadOnlyList<T> entities) where T : BitcraftEventBase
  {
    if (entities.Count == 0) return;

    var def = GetOrBuildDefinition(typeof(T));
    const int batchSize = 500;

    try
    {
      await using var dbContext = await _factory.CreateDbContextAsync();
      var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
      await conn.OpenAsync();

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
            var value = ResolvePropertyValue(batch[i], col.PropertyPath);
            AddTypedParameter(cmd, $"@p_{colIdx}_{i}", value, col);
          }
        }

        cmd.CommandText = $"""
          INSERT INTO {def.TableName} ({def.ColumnList})
          VALUES {string.Join(", ", valueClauses)}
          ON CONFLICT (id) DO UPDATE SET {def.UpdateSet}
          """;

        await cmd.ExecuteNonQueryAsync();
      }

      _logger.LogInformation("Bulk upserted {Count} {Type} entities", entities.Count, typeof(T).Name);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to bulk upsert {Count} {Type} entities", entities.Count, typeof(T).Name);
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

      columns.Add(new ColumnDef(columnName, clrProperty, [clrProperty.Name], converter, storeType));
    }

    var columnNames = columns.Select(c => $"\"{c.ColumnName}\"").ToArray();
    var paramNames = columns.Select((c, i) => $"@p_{i}").ToArray();
    var updateParts = columns
      .Where(c => c.ColumnName != "id")
      .Select(c => $"\"{c.ColumnName}\" = EXCLUDED.\"{c.ColumnName}\"")
      .ToArray();

    var upsertSql = $"""
      INSERT INTO {fullTableName} ({string.Join(", ", columnNames)})
      VALUES ({string.Join(", ", paramNames)})
      ON CONFLICT (id) DO UPDATE SET {string.Join(", ", updateParts)}
      """;

    var deleteSql = $"DELETE FROM {fullTableName} WHERE id = @p_id";

    return new EntitySqlDefinition(
      UpsertSql: upsertSql,
      DeleteSql: deleteSql,
      TableName: fullTableName,
      ColumnList: string.Join(", ", columnNames),
      UpdateSet: string.Join(", ", updateParts),
      Columns: [.. columns]
    );
  }

  private static void AddParameters(NpgsqlCommand cmd, EntitySqlDefinition def, BitcraftEventBase entity)
  {
    for (var i = 0; i < def.Columns.Length; i++)
    {
      var col = def.Columns[i];
      var value = ResolvePropertyValue(entity, col.PropertyPath);
      AddTypedParameter(cmd, $"@p_{i}", value, col);
    }
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

  private static object? ResolvePropertyValue(object? obj, string[] propertyPath)
  {
    var current = obj;
    foreach (var segment in propertyPath)
    {
      if (current == null) return null;
      var prop = current.GetType().GetProperty(segment);
      if (prop == null) return null;
      current = prop.GetValue(current);
    }
    return current;
  }

  private record ColumnDef(
    string ColumnName,
    PropertyInfo PropertyInfo,
    string[] PropertyPath,
    ValueConverter? Converter,
    string? StoreType
  );

  private record EntitySqlDefinition(
    string UpsertSql,
    string DeleteSql,
    string TableName,
    string ColumnList,
    string UpdateSet,
    ColumnDef[] Columns
  );
}
