using System.Collections;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace Vela.Dump.Services;

public class JsonDumpSubscriber
{
  private readonly ILogger<JsonDumpSubscriber> _logger;
  private readonly IOptions<JsonDumpOptions> _dumpOptions;
  private readonly JsonSerializerOptions _jsonOptions;

  public JsonDumpSubscriber(
    ILogger<JsonDumpSubscriber> logger,
    IOptions<JsonDumpOptions> dumpOptions,
    JsonSerializerOptions jsonOptions)
  {
    _logger = logger;
    _dumpOptions = dumpOptions;
    _jsonOptions = jsonOptions;
  }

  public async Task DumpTablesAsync(DbConnection conn)
  {
    var outputPath = _dumpOptions.Value.OutputPath;
    Directory.CreateDirectory(outputPath);

    var fields = conn.Db.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);

    foreach (var field in fields)
    {
      var handle = field.GetValue(conn.Db);
      if (handle == null) continue;

      var baseType = field.FieldType.BaseType;
      if (baseType is not { IsGenericType: true }) continue;
      if (baseType.GetGenericTypeDefinition() != typeof(RemoteTableHandle<,>)) continue;

      var iterMethod = field.FieldType.GetMethod("Iter", BindingFlags.Instance | BindingFlags.Public);
      if (iterMethod == null) continue;

      var result = iterMethod.Invoke(handle, null);
      if (result is not IEnumerable items) continue;

      var rows = new List<object>();
      foreach (var item in items)
        rows.Add(item);

      if (rows.Count == 0) continue;

      var filePath = Path.Combine(outputPath, $"{field.Name}.json");
      try
      {
        await using var stream = File.Create(filePath);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartArray();
        foreach (var row in rows)
          JsonSerializer.Serialize(writer, row, row.GetType(), _jsonOptions);
        writer.WriteEndArray();
        await writer.FlushAsync();

        _logger.LogDebug("Wrote {count} rows to {file}", rows.Count, field.Name);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error writing {file}", filePath);
      }
    }
  }
}
