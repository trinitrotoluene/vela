using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vela.Data;
using Vela.Events;
using Vela.Services.Contracts;

namespace Vela.Services.Impl;

/// <summary>
/// Simplified PostgreSQL writer for static descriptor entities (items, recipes, etc.).
/// These change only on game deploys (~biweekly), so no buffering or optimization needed.
/// </summary>
public class DescriptorDbWriter : IDescriptorDbWriter
{
    private readonly IDbContextFactory<VelaDbContext> _factory;
    private readonly ILogger<DescriptorDbWriter> _logger;

    /// <summary>
    /// The set of entity types that are descriptors (handled by PostgreSQL, not ConvergeDB).
    /// </summary>
    public static readonly HashSet<Type> DescriptorTypes =
    [
        typeof(BitcraftRecipe),
        typeof(BitcraftItemList),
        typeof(BitcraftItem),
        typeof(BitcraftCargoItem),
        typeof(BitcraftBuildingDesc),
        typeof(BitcraftPavingTileDesc),
        typeof(BitcraftClaimTechDesc),
    ];

    public static bool IsDescriptorType(Type t) => DescriptorTypes.Contains(t);

    public DescriptorDbWriter(IDbContextFactory<VelaDbContext> factory, ILogger<DescriptorDbWriter> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task PopulateAsync(Type entityType, string module, IReadOnlyList<BitcraftEventBase> entities)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();

            // Full replace: delete all existing rows for this entity type, then bulk insert.
            // Descriptor data is static and fully replaced on every SpacetimeDB reconnect.
            var tableName = db.Model.FindEntityType(entityType)?.GetTableName();
            if (tableName != null)
            {
                await db.Database.ExecuteSqlRawAsync($"DELETE FROM {tableName}");
            }

            foreach (var entity in entities)
            {
                entity.Module = module;
                db.Add(entity);
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("Populated {Count} {Type} descriptors for module {Module}",
                entities.Count, entityType.Name, module);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to populate {Type} descriptors for module {Module}",
                entityType.Name, module);
        }
    }

    public void EnqueueUpsert<T>(T entity) where T : BitcraftEventBase
    {
        // For descriptors, just do a synchronous fire-and-forget upsert.
        // These events are extremely rare during normal gameplay.
        _ = Task.Run(async () =>
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync();
                var existing = await db.FindAsync(typeof(T), entity.Id);
                if (existing != null)
                    db.Remove(existing);
                db.Add(entity);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert {Type} descriptor {Id}", typeof(T).Name, entity.Id);
            }
        });
    }

    public void EnqueueDelete<T>(T entity) where T : BitcraftEventBase
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync();
                var existing = await db.FindAsync(typeof(T), entity.Id);
                if (existing != null)
                {
                    db.Remove(existing);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete {Type} descriptor {Id}", typeof(T).Name, entity.Id);
            }
        });
    }

    private static IQueryable GetDbSet(DbContext db, Type entityType)
    {
        var method = typeof(DbContext).GetMethod(nameof(DbContext.Set), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)!
            .MakeGenericMethod(entityType);
        return (IQueryable)method.Invoke(db, null)!;
    }
}
