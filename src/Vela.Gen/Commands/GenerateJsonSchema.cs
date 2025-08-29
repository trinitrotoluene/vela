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
    Console.WriteLine("Generating schema");

    if (!outDir.Exists)
    {
      Console.WriteLine("Output directory not found - creating it");
      outDir.Create();
    }

    foreach (var file in outDir.GetFiles())
    {
      Console.WriteLine($"Deleting {file.Name}");
      file.Delete();
    }

    var services = new ServiceCollection();
    services.AddJsonSerializer();
    var container = services.BuildServiceProvider();
    var options = container.GetRequiredService<JsonSerializerOptions>();


    Console.WriteLine("Emitting schemas");

    foreach (var type in BitcraftEventBase.SchemaTypes)
    {
      var fileName = $"{type.Name}.schema.json";

      Console.WriteLine($"Emitting {fileName}");
      var typeFile = new FileInfo(Path.Combine(outDir.FullName, fileName));
      using var file = typeFile.CreateText();

      var schema = options.GetJsonSchemaAsNode(type, new() { TreatNullObliviousAsNonNullable = true });
      file.Write(schema);
    }

    Console.WriteLine("Done");
  }
}
