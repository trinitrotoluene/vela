namespace Vela.Mappers
{
  public abstract class MappedDbEntityBase<TEntity, TOutput>
  {
    public abstract string TopicName { get; }
    public abstract TOutput Map(TEntity entity);
  }
}