using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    Type[] schemaTypes = [.. BitcraftEventBase.SchemaTypes, .. GenericEventBase.SchemaTypes];
    foreach (var type in schemaTypes)
    {
      var fileName = $"{type.Name}.schema.json";

      Console.WriteLine($"Emitting {fileName}");
      var typeFile = new FileInfo(Path.Combine(outDir.FullName, fileName));
      using var file = typeFile.CreateText();


      var exporterOptions = new JsonSchemaExporterOptions
      {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = (context, node) =>
        {
          if (context.TypeInfo.Type.GetCustomAttribute<GlobalEntityAttribute>() != null)
          {
            if (node is JsonObject objNode)
            {
              objNode["x-global-entity"] = true;
            }
          }

          return node;
        }
      };

      var schema = options.GetJsonSchemaAsNode(type, exporterOptions);
      file.Write(schema);
    }

    Console.WriteLine("Done");
  }
}
