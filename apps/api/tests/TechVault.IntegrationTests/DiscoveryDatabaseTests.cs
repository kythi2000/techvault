using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TechVault.Api.Responses;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Infrastructure.Persistence;

namespace TechVault.IntegrationTests;

public sealed class DiscoveryDatabaseTests(ITestOutputHelper output)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_backfills_existing_content_and_rolls_back_without_changing_editorial_data()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeAsync();
        await using var db = fixture.CreateContext();
        var migrator = db.GetService<IMigrator>();
        // Exercise an existing four-device database, not just inserts after schema creation.
        var before = await EditorialRowsAsync(db);
        await migrator.MigrateAsync("20260914062318_InitialCatalog", Ct);
        Assert.Single(await db.Database.GetAppliedMigrationsAsync(Ct));
        Assert.Equal(before, await EditorialRowsAsync(db));
        Assert.Equal(0, await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM pg_proc WHERE proname LIKE 'catalog_%search%'
            """).SingleAsync(Ct));
        await db.Database.MigrateAsync(Ct);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(before, await EditorialRowsAsync(db));
        Assert.Equal(4, await db.Devices.CountAsync(Ct));
        Assert.Equal(44, await db.DeviceSpecifications.CountAsync(Ct));
        foreach (var (query, slug) in new[] { ("Nokia 3310", "nokia-3310"), ("Nokia 3210", "nokia-3210"),
                     ("Apple Macintosh", "macintosh-128k"), ("Apple iMac", "imac-g3") })
        {
            var response = await fixture.Client.GetFromJsonAsync<PaginatedResponse<DeviceCardResponse>>(
                "/api/v1/search?q=" + Uri.EscapeDataString(query), Ct);
            Assert.Equal(slug, Assert.Single(response!.Data).Slug);
        }
        // A rename still refreshes vectors after the down/up rehearsal reinstalls triggers.
        await db.Brands.Where(x => x.Slug == "apple").ExecuteUpdateAsync(x => x.SetProperty(b => b.Name, "Renamedfruit"), Ct);
        var renamed = await fixture.Client.GetFromJsonAsync<PaginatedResponse<DeviceCardResponse>>("/api/v1/search?q=renamedfruit", Ct);
        Assert.Equal(2, renamed!.Pagination.Total);
    }

    [Fact]
    public async Task Representative_search_and_chronology_queries_use_their_indexes()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeEmptyAsync();
        await using var db = fixture.CreateContext();
        var brand = new Brand("Planbrand", "planbrand");
        var category = new Category("Phones", "phones", 0);
        db.AddRange(brand, category);
        await db.SaveChangesAsync(Ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Devices" ("Id", "Name", "Slug", "BrandId", "CategoryId", "ShortDescription",
                "Description", "History", "SeoTitle", "SeoDescription", "ReleaseYear", "Status", "PublishedAt", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), CASE WHEN i = 4000 THEN 'Needleplan' ELSE 'Plan device ' || i END,
                'plan-device-' || i, {brand.Id}, {category.Id}, 'Summary', 'Description', 'History', 'Title', 'SEO',
                1980 + i % 40, 1, now(), now(), now() FROM generate_series(1, 8000) AS series(i);
            """, Ct);
        // Flush GIN's pending insert list and refresh statistics, as routine autovacuum would.
        // VACUUM must run outside a transaction and as its own command.
        await db.Database.ExecuteSqlRawAsync("VACUUM (ANALYZE) \"Devices\"", Ct);
        await db.Database.ExecuteSqlRawAsync("ANALYZE \"Brands\"; ANALYZE \"Categories\";", Ct);

        // These are the indexed predicates/orderings of the actual card queries, including taxonomy joins.
        // Do not force enable_seqscan=false: the planner must choose indexes with representative data.
        var searchPlan = await ExplainAsync(db, """
            SELECT d."Id", d."Name", d."Slug", d."ShortDescription", b."Name", c."Name", p."Slug"
            FROM "Devices" d JOIN "Brands" b ON b."Id" = d."BrandId"
            JOIN "Categories" c ON c."Id" = d."CategoryId" LEFT JOIN "Categories" p ON p."Id" = c."ParentCategoryId"
            WHERE d."Status" = 1 AND d."SearchVector" @@ plainto_tsquery('simple', 'needleplan')
            ORDER BY ts_rank(d."SearchVector", plainto_tsquery('simple', 'needleplan')) DESC, d."Slug" LIMIT 24
            """);
        var timelinePlan = await ExplainAsync(db, """
            SELECT d."Id", d."Name", d."Slug", d."ShortDescription", b."Name", c."Name", p."Slug"
            FROM "Devices" d JOIN "Brands" b ON b."Id" = d."BrandId"
            JOIN "Categories" c ON c."Id" = d."CategoryId" LEFT JOIN "Categories" p ON p."Id" = c."ParentCategoryId"
            WHERE d."Status" = 1 AND d."ReleaseYear" IS NOT NULL
            ORDER BY d."ReleaseYear", d."ReleaseDate", d."Slug" LIMIT 24
            """);
        output.WriteLine("Search plan:\n" + searchPlan);
        output.WriteLine("Timeline plan:\n" + timelinePlan);
        Assert.Contains("IX_Devices_SearchVector", searchPlan);
        Assert.Contains("IX_Devices_Timeline", timelinePlan);
        var result = await fixture.Client.GetFromJsonAsync<PaginatedResponse<DeviceCardResponse>>("/api/v1/search?q=needleplan", Ct);
        Assert.Equal("plan-device-4000", Assert.Single(result!.Data).Slug);
        var timeline = await fixture.Client.GetFromJsonAsync<PaginatedResponse<DeviceCardResponse>>("/api/v1/timeline", Ct);
        Assert.Equal(8000, timeline!.Pagination.Total);
        Assert.Equal(24, timeline.Data.Count);
    }

    private static Task<List<string>> EditorialRowsAsync(TechVaultDbContext db) => db.Database.SqlQuery<string>($"""
        SELECT (to_jsonb(d) - 'Aliases' - 'ModelNumber' - 'SearchVector' - 'ComparisonGroupId')::text AS "Value" FROM "Devices" d
        """).OrderBy(x => x).ToListAsync(Ct);

    private static async Task<string> ExplainAsync(TechVaultDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync(Ct);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN (ANALYZE, BUFFERS) " + sql;
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var lines = new List<string>();
        while (await reader.ReadAsync(Ct)) lines.Add(reader.GetString(0));
        return string.Join('\n', lines);
    }
}
