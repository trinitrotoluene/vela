using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Vela.Gen.Commands;

namespace Vela.Gen;

class Program
{
  static async Task<int> Main(string[] args)
  {
    Option<FileInfo> fileOption = new("--json-schema-output")
    {
      Description = "Output file to write event JSON schemas to"
    };

    RootCommand rootCommand = new("CLI tool for Vela");
    rootCommand.Subcommands.Add(GenerateJsonSchema.Command());

    return await rootCommand.Parse(args).InvokeAsync();
  }
}