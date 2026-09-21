using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Vela.Events
{
  public abstract record BitcraftEventBase(string Id)
  {
    // Set on every event before publish so ConvergeDB writes carry the module tag.
    // Not mapped to Postgres - descriptor tables are global and shouldn't carry a module column,
    // so on the Postgres path this assignment is a harmless no-op.
    [NotMapped]
    public string Module { get; set; } = null!;

    public static readonly Type[] SchemaTypes = [.. Assembly.GetExecutingAssembly()!
      .GetTypes()
      .Where(x => x.IsAssignableTo(typeof(BitcraftEventBase)) && !x.IsAbstract
    )];

    public static readonly Type[] PostgresTypes;
    public static readonly Type[] ConvergeDbTypes;
    public static readonly Type[] ConvergeDbEventTypes;

    static BitcraftEventBase()
    {
      var postgres = new List<Type>();
      var convergeDb = new List<Type>();
      var convergeDbEvent = new List<Type>();
      var violations = new List<string>();

      foreach (var type in SchemaTypes)
      {
        var markers = new List<string>();
        if (type.GetCustomAttribute<PostgresAttribute>() != null) markers.Add("[Postgres]");
        if (type.GetCustomAttribute<ConvergeDbAttribute>() != null) markers.Add("[ConvergeDb]");
        if (type.GetCustomAttribute<ConvergeDbEventAttribute>() != null) markers.Add("[ConvergeDbEvent]");

        if (markers.Count > 1)
          violations.Add($"{type.Name} has {string.Join(" and ", markers)} - pick one");
        else if (markers.Count == 0)
          violations.Add($"{type.Name} has none of [Postgres], [ConvergeDb] or [ConvergeDbEvent] - every entity must declare its storage");
        else if (markers[0] == "[Postgres]")
          postgres.Add(type);
        else if (markers[0] == "[ConvergeDb]")
          convergeDb.Add(type);
        else
          convergeDbEvent.Add(type);
      }

      if (violations.Count > 0)
        throw new InvalidOperationException(
          "BitcraftEventBase storage marker validation failed:\n  " +
          string.Join("\n  ", violations));

      PostgresTypes = [.. postgres];
      ConvergeDbTypes = [.. convergeDb];
      ConvergeDbEventTypes = [.. convergeDbEvent];
    }
  };
}
