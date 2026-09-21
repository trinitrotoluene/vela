namespace Vela.Events;

// Marks an insert-only SpacetimeDB event table. Each live insert is emitted as a single
// ConvergeDB event (IConvergeDbWriter.EmitAsync) - never snapshotted, updated or retracted.
[AttributeUsage(AttributeTargets.Class)]
public sealed class ConvergeDbEventAttribute : Attribute;
