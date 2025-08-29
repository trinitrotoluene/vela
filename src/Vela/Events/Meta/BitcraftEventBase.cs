using System.Collections.ObjectModel;
using System.Reflection;

namespace Vela.Events
{
  public abstract record BitcraftEventBase(string Id)
  {
    private record CacheKeyValue(string Value, bool IsGlobal);

    // This is dynamically set when publishing to avoid having to copy the object
    public string Module { get; set; } = null!;

    public static readonly Type[] SchemaTypes = [.. Assembly.GetExecutingAssembly()!
      .GetTypes()
      .Where(x => x.IsAssignableTo(typeof(BitcraftEventBase)) && !x.IsAbstract
    )];

    private static readonly IReadOnlyDictionary<Type, CacheKeyValue> CacheKeys;

    static BitcraftEventBase()
    {
      var cacheKeys = new Dictionary<Type, CacheKeyValue>();
      foreach (var type in SchemaTypes)
      {
        cacheKeys.Add(type, new(
          Value: $"cache:{type.Name}",
          IsGlobal: type.GetCustomAttribute<GlobalEntityAttribute>() != null
        ));
      }

      CacheKeys = new ReadOnlyDictionary<Type, CacheKeyValue>(cacheKeys);
    }

    public static string CacheKey<TEvent>(TEvent t, string module) where TEvent : BitcraftEventBase
    {
      return CacheKey(t.GetType(), module);
    }

    public static string CacheKey(Type t, string module)
    {
      if (!CacheKeys.TryGetValue(t, out var cacheKey))
        throw new InvalidDataException($"Attempted to create a cache key for unsupported type {t.Name}");

      return $"{cacheKey.Value}:{(cacheKey.IsGlobal ? "global" : module)}";
    }
  };
}