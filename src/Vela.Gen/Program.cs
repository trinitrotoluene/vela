using System.CommandLine;
using Vela.Gen.Commands;

namespace Vela.Gen;

class Program
{
  static async Task<int> Main(string[] args)
  {
    RootCommand rootCommand = new("CLI tool for Vela");
    rootCommand.Subcommands.Add(GenerateJsonSchema.Command());

    return await rootCommand.Parse(args).InvokeAsync();
  }
}