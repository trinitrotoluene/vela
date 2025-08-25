public interface IDbConnectionAccessor
{
  bool TryGet(out SpacetimeDB.Types.DbConnection? conn);
  Task<SpacetimeDB.Types.DbConnection?> WaitForConnectionAsync(CancellationToken cancellationToken = default);
  void SetConnection(SpacetimeDB.Types.DbConnection? conn);
}