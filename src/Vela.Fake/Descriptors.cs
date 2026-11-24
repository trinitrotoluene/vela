using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Vela.Data;
using Vela.Events;
using Vela.Services.Impl;

namespace Vela.Fake;

/// <summary>
/// Fills a Vela Postgres database with made-up descriptors covering the ids the fake's rows refer
/// to, through Vela's own migrations and writer, so consumers that join against VelaDb find them.
/// </summary>
public static class Descriptors
{
    // The fake's rows use item and cargo ids 1-2999, recipe ids 1-1999 and building descs 1-399 plus
    // 405, the town hall.
    private const int Items = 2_999;
    private const int CraftRecipes = 1_999;
    private const int UnpackRecipes = 300;
    private const int BuildingDescs = 405;

    public static async Task SeedAsync(string connectionString, int seed, CancellationToken ct)
    {
        using var loggers = LoggerFactory.Create(b => b.AddSerilog(new LoggerConfiguration().WriteTo.Console().CreateLogger(), dispose: true));
        // Vela's own options, so jsonb columns hold what Vela writes.
        var json = new ServiceCollection().AddJsonSerializer().BuildServiceProvider().GetRequiredService<JsonSerializerOptions>();
        var factory = new ContextFactory(new DbContextOptionsBuilder<VelaDbContext>()
            .UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options, json);

        await using (var db = factory.CreateDbContext()) await db.Database.MigrateAsync(ct);

        var rng = new Random(seed);
        var writer = new DescriptorDbWriter(factory, loggers.CreateLogger<DescriptorDbWriter>());
        List<(Type Type, BitcraftEventBase[] Rows)> sets =
        [
            (typeof(BitcraftItem), Enumerable.Range(1, Items).Select(i => new BitcraftItem(
                $"{i}", $"Item {i}", "", rng.Next(1, 100), i % 10 + 1, (BitcraftItemRarity)(i % 7), 0, true, "")).ToArray()),
            (typeof(BitcraftCargoItem), Enumerable.Range(1, Items).Select(i => new BitcraftCargoItem(
                $"{i}", $"Cargo {i}", "", rng.Next(100, 6000), i % 10 + 1, (BitcraftItemRarity)(i % 7), "", false, false)).ToArray()),
            (typeof(BitcraftBuildingDesc), Enumerable.Range(1, BuildingDescs).Select(i => new BitcraftBuildingDesc(
                $"{i}", i == BuildingDescs ? "Town Hall" : $"Building {i}", "", [new(i % 20 + 1, 1)])).ToArray()),
            (typeof(BitcraftRecipe), Enumerable.Range(1, CraftRecipes).Select(i => Recipe(
                    i, "Craft {0}", [Stack(rng, BitcraftItemType.Item, rng.Next(1, 20))], [Stack(rng, BitcraftItemType.Item, rng.Next(1, 5))]))
                // Unpack recipes turn one cargo into items, as Botrealis' contribution totals expect.
                .Concat(Enumerable.Range(1, UnpackRecipes).Select(c => Recipe(
                    CraftRecipes + c, "Unpack {1}", [new ItemStack($"{c}", 1, BitcraftItemType.Cargo)], [Stack(rng, BitcraftItemType.Item, 100)])))
                .ToArray()),
        ];

        foreach (var (type, rows) in sets)
        {
            await writer.PopulateAsync(type, rows);
            // PopulateAsync logs failures rather than throwing.
            await using var db = factory.CreateDbContext();
            string table = db.Model.FindEntityType(type)!.GetTableName()!;
            int count = await db.Database.SqlQueryRaw<int>($"SELECT count(*)::int AS \"Value\" FROM {table}").SingleAsync(ct);
            if (count != rows.Length) throw new InvalidOperationException($"{type.Name}: wrote {rows.Length} rows, found {count}");
        }
        Console.WriteLine($"Seeded descriptors: {string.Join(", ", sets.Select(s => $"{s.Rows.Length} {s.Type.Name}"))}");
    }

    private static ItemStack Stack(Random rng, BitcraftItemType type, int quantity) => new($"{rng.Next(1, Items + 1)}", quantity, type);

    private static BitcraftRecipe Recipe(int id, string name, ItemStack[] consumed, ItemStack[] produced) =>
        new($"{id}", name, new BuildingRequirement(id % 20 + 1, 1), [new LevelRequirement(id % 15 + 1, 1)], [],
            consumed, produced, false, 10, 5, 1, 0, [], []);

    private sealed class ContextFactory(DbContextOptions<VelaDbContext> options, JsonSerializerOptions json) : IDbContextFactory<VelaDbContext>
    {
        public VelaDbContext CreateDbContext() => new(options, json);
    }
}
