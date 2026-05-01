using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Vela.Events
{
  public abstract record BitcraftEventBase(string Id)
  {
    // Set on every event before publish so ConvergeDB writes carry the module tag.
    // Not mapped to Postgres — descriptor tables are global and shouldn't carry a module column,
    // so on the Postgres path this assignment is a harmless no-op.
    [NotMapped]
    public string Module { get; set; } = null!;

    public static readonly Type[] SchemaTypes = [.. Assembly.GetExecutingAssembly()!
      .GetTypes()
      .Where(x => x.IsAssignableTo(typeof(BitcraftEventBase)) && !x.IsAbstract
    )];

    public static readonly Type[] PostgresTypes;
    public static readonly Type[] ConvergeDbTypes;

    static BitcraftEventBase()
    {
      var postgres = new List<Type>();
      var convergeDb = new List<Type>();
      var violations = new List<string>();

      foreach (var type in SchemaTypes)
      {
        var hasPostgres = type.GetCustomAttribute<PostgresAttribute>() != null;
        var hasConvergeDb = type.GetCustomAttribute<ConvergeDbAttribute>() != null;

        if (hasPostgres && hasConvergeDb)
          violations.Add($"{type.Name} has both [Postgres] and [ConvergeDb] — pick one");
        else if (!hasPostgres && !hasConvergeDb)
          violations.Add($"{type.Name} has neither [Postgres] nor [ConvergeDb] — every entity must declare its storage");
        else if (hasPostgres)
          postgres.Add(type);
        else
          convergeDb.Add(type);
      }

      if (violations.Count > 0)
        throw new InvalidOperationException(
          "BitcraftEventBase storage marker validation failed:\n  " +
          string.Join("\n  ", violations));

      PostgresTypes = [.. postgres];
      ConvergeDbTypes = [.. convergeDb];
    }
  };
}
