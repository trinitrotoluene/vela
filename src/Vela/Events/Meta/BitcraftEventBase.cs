using System.Collections.ObjectModel;
using System.Reflection;

namespace Vela.Events
{
  public abstract record BitcraftEventBase(string Id)
  {
    // This is dynamically set when publishing to avoid having to copy the object
    public string? Module { get; set; }

    public static readonly Type[] SchemaTypes = [.. Assembly.GetEntryAssembly()!
      .GetTypes()
      .Where(x => x.IsAssignableTo(typeof(BitcraftEventBase))
    )];

    public static readonly string[] SchemaNames = [.. SchemaTypes.Select(x => x.Name)];

    public static readonly IReadOnlyDictionary<Type, string> CacheKeys;

    static BitcraftEventBase()
    {
      var cacheKeys = new Dictionary<Type, string>();
      foreach (var type in SchemaTypes)
      {
        cacheKeys.Add(type, $"cache:{type.Name}");
      }

      CacheKeys = new ReadOnlyDictionary<Type, string>(cacheKeys);
    }

    public static string? CacheKey<TEvent>(TEvent t) where TEvent : BitcraftEventBase
    {
      CacheKeys.TryGetValue(t.GetType(), out var cacheKey);
      return cacheKey;
    }
  };
}