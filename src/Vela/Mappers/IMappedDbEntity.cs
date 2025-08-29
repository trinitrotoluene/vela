namespace Vela.Mappers
{
  public abstract class MappedDbEntityBase<TEntity, TOutput>
  {
    public virtual string TopicName { get => $"bitcraft.{typeof(TOutput).Name}"; }

    public abstract TOutput Map(TEntity entity);
  }
}