using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TechVault.Api.Responses;
using TechVault.Application.Comparisons;
using TechVault.Domain.Comparisons;
using TechVault.Infrastructure.Persistence;
using TechVault.Infrastructure.Persistence.Seed;

namespace TechVault.IntegrationTests;

public sealed class ComparisonDatabaseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upgrade_initializes_only_new_metadata_preserves_editorial_rows_and_can_be_reapplied(bool reclassified)
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeAsync();
        await using var db = fixture.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260917105840_AddCatalogDiscovery", Ct);
        Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync(Ct)).Count());
        // All following writes are against the previous schema in this disposable database.
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Devices" SET "Description" = 'Preserved legacy editorial description' WHERE "Slug" = 'nokia-3310';
            DELETE FROM "DeviceSpecifications" WHERE "DeviceId" = (SELECT "Id" FROM "Devices" WHERE "Slug" = 'nokia-3310')
                AND "DefinitionId" = (SELECT "Id" FROM "SpecificationDefinitions" WHERE "Key" = 'standby_time_max');
            INSERT INTO "Devices" ("Id", "Name", "Slug", "BrandId", "CategoryId", "ShortDescription", "Description",
                "History", "SeoTitle", "SeoDescription", "Status", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), 'Editorial draft', 'editorial-draft', "BrandId", "CategoryId", '', '', '', '', '', 0, now(), now()
                FROM "Devices" WHERE "Slug" = 'nokia-3310';
            INSERT INTO "SpecificationDefinitions" ("Id", "Name", "Key", "GroupId", "DataType", "DisplayOrder")
            SELECT gen_random_uuid(), 'Editorial only', 'editorial_only', "Id", 1, 100 FROM "SpecificationGroups" WHERE "Key" = 'general';
            """, Ct);
        if (reclassified)
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "Devices" SET "CategoryId" = (SELECT "Id" FROM "Categories" WHERE "Slug" = 'phones')
                WHERE "Slug" = 'macintosh-128k'
                """, Ct);
        var before = await LegacyRowsAsync(db);
        await db.Database.MigrateAsync(Ct);
        Assert.Equal(before, await LegacyRowsAsync(db));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(2, await db.ComparisonGroups.CountAsync(Ct));
        Assert.True((await db.SpecificationDefinitions.SingleAsync(x => x.Key == "ram_capacity", Ct)).IsComparable);
        Assert.False((await db.SpecificationDefinitions.SingleAsync(x => x.Key == "announcement_date", Ct)).IsComparable);
        Assert.False((await db.SpecificationDefinitions.SingleAsync(x => x.Key == "editorial_only", Ct)).IsComparable);
        Assert.Null((await db.Devices.SingleAsync(x => x.Slug == "editorial-draft", Ct)).ComparisonGroupId);
        var pair = await fixture.Client.GetFromJsonAsync<ApiResponse<CompareDevicesResponse>>(
            "/api/v1/compare?devices=nokia-3310,nokia-3210", Ct);
        Assert.Equal("phone", pair!.Data.ComparisonGroup.Key);
        Assert.DoesNotContain(pair.Data.SpecificationGroups.SelectMany(x => x.Specifications), x => x.Key == "standby_time_max");
        using var computers = await fixture.Client.GetAsync("/api/v1/compare?devices=macintosh-128k,imac-g3", Ct);
        Assert.Equal(reclassified ? HttpStatusCode.BadRequest : HttpStatusCode.OK, computers.StatusCode);
        if (reclassified)
            Assert.Equal("COMPARISON_UNAVAILABLE", (await computers.Content.ReadFromJsonAsync<ApiErrorResponse>(Ct))!.Error.Code);

        // A deliberate rerun cannot repair an unassigned/reclassified device or restore a deleted spec.
        var metadataBeforeSeed = await db.Devices.AsNoTracking().OrderBy(x => x.Slug)
            .Select(x => new { x.Id, x.ComparisonGroupId, x.UpdatedAt }).ToListAsync(Ct);
        await CatalogSeed.SeedAsync(db, Ct);
        Assert.Equal(before, await LegacyRowsAsync(db));
        Assert.Equal(metadataBeforeSeed, await db.Devices.AsNoTracking().OrderBy(x => x.Slug)
            .Select(x => new { x.Id, x.ComparisonGroupId, x.UpdatedAt }).ToListAsync(Ct));

        db.ChangeTracker.Clear();
        await migrator.MigrateAsync("20260917105840_AddCatalogDiscovery", Ct);
        Assert.Equal(before, await LegacyRowsAsync(db));
        await db.Database.MigrateAsync(Ct);
        Assert.Equal(before, await LegacyRowsAsync(db));
        Assert.Equal(2, await db.ComparisonGroups.CountAsync(Ct));
    }

    [Fact]
    public async Task Comparison_metadata_has_unique_keys_valid_foreign_keys_and_restricted_deletion()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeAsync();
        await using (var db = fixture.CreateContext())
        {
            db.ComparisonGroups.Add(new ComparisonGroup("Duplicate", "phone"));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)duplicate.InnerException!).SqlState);
        }
        await using (var db = fixture.CreateContext())
        {
            var referenced = await db.ComparisonGroups.SingleAsync(x => x.Key == "phone", Ct);
            db.ComparisonGroups.Remove(referenced);
            var restricted = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)restricted.InnerException!).SqlState);
        }
        await using (var db = fixture.CreateContext())
        {
            var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Devices.Where(x => x.Slug == "nokia-3310")
                .ExecuteUpdateAsync(x => x.SetProperty(d => d.ComparisonGroupId, Guid.NewGuid()), Ct));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, invalid.SqlState);
        }
    }

    private static async Task<string[]> LegacyRowsAsync(TechVaultDbContext db)
    {
        var rows = new List<string>();
        foreach (var table in new[] { "Brands", "Categories", "Devices", "SpecificationGroups", "SpecificationDefinitions", "DeviceSpecifications" })
        {
            // Fixed table names only; all fields predating Phase 7 must survive unchanged.
            var sql = $"SELECT (to_jsonb(record) - ARRAY['ComparisonGroupId', 'IsComparable'])::text AS \"Value\" FROM \"{table}\" AS record";
            rows.AddRange((await db.Database.SqlQueryRaw<string>(sql).ToListAsync(Ct)).Select(row => table + ":" + row));
        }
        return rows.Order(StringComparer.Ordinal).ToArray();
    }
}
