using System.ComponentModel.DataAnnotations;

public class BitcraftServiceOptions
{
  [Required]
  public required string Uri { get; set; }

  [Required]
  public required string AuthToken { get; set; }

  [Required]
  public required string Module { get; set; }

  public string? HeartbeatUrl { get; set; }

  /// <summary>
  /// Also subscribe to the location of every inventory owner (players, buildings, chests), so a
  /// consumer can place inventories that carry no player id, such as housing chests. Off by default
  /// while it is rolled out region by region.
  /// </summary>
  public bool SubscribeOwnerLocations { get; set; }
}