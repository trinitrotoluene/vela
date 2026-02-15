namespace Vela.Events;

[Flags]
public enum StorageTarget
{
  Cache = 1,
  Database = 2
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class StorageAttribute(StorageTarget target) : Attribute
{
  public StorageTarget Target { get; } = target;
}
