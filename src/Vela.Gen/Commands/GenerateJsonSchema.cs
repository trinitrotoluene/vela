using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Vela.Events;


namespace Vela.Gen.Commands;

public static class GenerateJsonSchema
{
  public static Command Command()
  {
    Option<DirectoryInfo> outOption = new("--outDir", "-o");
    outOption.DefaultValueFactory = (result) => new(".out");

    Command command = new("generate-json-schema", "Generate a JSON schema for published events") {
      outOption
    };

    command.SetAction((result) =>
    {
      var outFile = result.GetRequiredValue(outOption);
      GenerateSchema(outFile);
    });

    return command;
  }

  private static void GenerateSchema(DirectoryInfo outDir)
  {
    if (!outDir.Exists) outDir.Create();
    foreach (var file in outDir.GetFiles())
    {
      file.Delete();
    }

    var services = new ServiceCollection();
    services.AddJsonSerializer();
    var container = services.BuildServiceProvider();
    var options = container.GetRequiredService<JsonSerializerOptions>();

    foreach (var type in BitcraftEventBase.SchemaTypes)
    {
      var typeFile = new FileInfo(Path.Combine(outDir.FullName, $"{type.Name}.schema.json"));
      using var file = typeFile.CreateText();

      var schema = options.GetJsonSchemaAsNode(type, new() { TreatNullObliviousAsNonNullable = true });
      file.Write(schema);
    }
  }
}
