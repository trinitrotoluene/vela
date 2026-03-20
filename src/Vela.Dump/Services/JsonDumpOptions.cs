using System.ComponentModel.DataAnnotations;

namespace Vela.Dump.Services;

public class JsonDumpOptions
{
  [Required]
  public required string OutputPath { get; set; }

  [Required]
  public required string[] Subscriptions { get; set; }
}
