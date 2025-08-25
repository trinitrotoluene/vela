using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Json.More;
using Json.Schema;
using Json.Schema.Generation;
using Microsoft.Extensions.DependencyInjection;
using NJsonSchema.CodeGeneration.TypeScript;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using Polly;
using Vela.Events;


namespace Vela.Gen.Commands;

internal class SchemaRoot
{
  public Envelope<object> Envelope { get; set; }
  public UpdateEnvelope<object> UpdateEnvelope { get; set; }
  public BitcraftAuctionListingState BitcraftAuctionListingState { get; set; }
  public BitcraftBuildingDesc BitcraftBuildingDesc { get; set; }
  public BitcraftBuildingState BitcraftBuildingState { get; set; }
  public BitcraftChatMessage BitcraftChatMessage { get; set; }
  public BitcraftClaimState BitcraftClaimState { get; set; }
  public BitcraftEmpireState BitcraftEmpireState { get; set; }
  public BitcraftItem BitcraftItem { get; set; }
  public BitcraftItemList BitcraftItemList { get; set; }
  public BitcraftPublicProgressiveAction BitcraftPublicProgressiveAction { get; set; }
  public BitcraftRecipe BitcraftRecipe { get; set; }
  public BitcraftUserModerationState BitcraftUserModerationState { get; set; }
  public BitcraftUsernameState BitcraftUsernameState { get; set; }
}

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
