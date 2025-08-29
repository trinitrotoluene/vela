using System.Reflection;

public abstract record GenericEventBase
{
  public static readonly Type[] SchemaTypes = [.. Assembly.GetExecutingAssembly()!
      .GetTypes()
      .Where(x => x.IsAssignableTo(typeof(GenericEventBase)) && !x.IsAbstract
    )];
}
