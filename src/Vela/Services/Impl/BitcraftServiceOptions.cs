using System.ComponentModel.DataAnnotations;

public class BitcraftServiceOptions
{
  [Required]
  public required string Uri { get; set; }

  [Required]
  public required string AuthToken { get; set; }

  [Required]
  public required string Module { get; set; }
}