using System.Net;
using Microsoft.EntityFrameworkCore;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;
using TechVault.Infrastructure.Persistence;
using TechVault.Infrastructure.Persistence.Seed;

namespace TechVault.IntegrationTests;

public sealed class MixedCatalogSeedTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upgrading_a_phase_three_database_only_adds_missing_records_and_preserves_the_existing_draft()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeEmptyAsync();
        string[] before;
        await using (var db = fixture.CreateContext())
        {
            await Nokia3310Seed.SeedAsync(db, Ct);
            var nokia = await db.Devices.Include(x => x.Specifications).SingleAsync(Ct);
            nokia.UpdateContent("Editor summary", "Editor description", "Editor history", "Editor title", "Editor SEO");
            db.DeviceSpecifications.Remove(nokia.Specifications.First());
            db.Entry(nokia).Property(x => x.Status).CurrentValue = DeviceStatus.Draft;
            db.Entry(nokia).Property(x => x.PublishedAt).CurrentValue = null;
            var brand = await db.Brands.SingleAsync(Ct);
            db.Entry(brand).Property(x => x.Name).CurrentValue = "Edited Nokia";
            var group = await db.SpecificationGroups.SingleAsync(x => x.Key == "network", Ct);
            db.Entry(group).Property(x => x.DisplayOrder).CurrentValue = 99;
            var definition = await db.SpecificationDefinitions.SingleAsync(x => x.Key == "antenna", Ct);
            db.Entry(definition).Property(x => x.Name).CurrentValue = "Editor antenna label";
            await db.SaveChangesAsync(Ct);
            before = await StoredRowsAsync(db);
        }
        await SeedAsync(fixture);
        await using (var db = fixture.CreateContext())
        {
            var after = await StoredRowsAsync(db);
            Assert.All(before, row => Assert.Contains(row, after));
            Assert.Equal(4, await db.Devices.CountAsync(Ct));
            Assert.Equal(3, await db.Devices.CountAsync(x => x.Status == DeviceStatus.Published, Ct));
            Assert.Equal(2, await db.Brands.CountAsync(Ct));
            Assert.Equal(4, await db.Categories.CountAsync(Ct));
            Assert.Equal(29, await db.SpecificationDefinitions.CountAsync(Ct));
        }
        using var hidden = await fixture.Client.GetAsync("/api/v1/devices/nokia-3310", Ct);
        using var added = await fixture.Client.GetAsync("/api/v1/devices/nokia-3210", Ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
    }

    [Fact]
    public async Task Reseeding_preserves_all_editorial_rows_ids_timestamps_deleted_specs_and_hidden_computers()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeAsync();
        string[] before;
        await using (var db = fixture.CreateContext())
        {
            foreach (var device in await db.Devices.Include(x => x.Specifications).ThenInclude(x => x.Definition).ToListAsync(Ct))
            {
                device.UpdateContent("Edited " + device.Slug, "Editor description", "Editor history", "Editor title", "Editor SEO");
                device.SetPhysicalDetails(100, 200, 300, 400);
                var spec = device.Specifications.First(x => x.DataType == SpecificationDataType.Number);
                device.SetSpecification(spec.Definition, SpecificationValue.Number(0));
                db.DeviceSpecifications.Remove(device.Specifications.First(x => x.DataType == SpecificationDataType.Text));
                if (device.Slug is "macintosh-128k" or "imac-g3")
                {
                    db.Entry(device).Property(x => x.Status).CurrentValue =
                        device.Slug == "imac-g3" ? DeviceStatus.Draft : DeviceStatus.Archived;
                    db.Entry(device).Property(x => x.PublishedAt).CurrentValue = null;
                }
            }
            var apple = await db.Brands.SingleAsync(x => x.Slug == "apple", Ct);
            db.Entry(apple).Property(x => x.Description).CurrentValue = "Editor brand content";
            var category = await db.Categories.SingleAsync(x => x.Slug == "all-in-one-computers", Ct);
            db.Entry(category).Property(x => x.Name).CurrentValue = "Editor category label";
            await db.SaveChangesAsync(Ct);
            before = await StoredRowsAsync(db);
        }
        await SeedAsync(fixture);
        // A second run in the same context must also be a complete no-op.
        await using (var db = fixture.CreateContext())
        {
            await CatalogSeed.SeedAsync(db, Ct);
            await CatalogSeed.SeedAsync(db, Ct);
            Assert.Equal(before, await StoredRowsAsync(db));
            Assert.Empty(db.ChangeTracker.Entries());
        }
        foreach (var slug in new[] { "macintosh-128k", "imac-g3" })
        foreach (var suffix in new[] { "", "/specifications" })
        {
            using var response = await fixture.Client.GetAsync($"/api/v1/devices/{slug}{suffix}", Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var computers = await fixture.Client.GetAsync("/api/v1/computers", Ct);
        computers.EnsureSuccessStatusCode();
        using var document = System.Text.Json.JsonDocument.Parse(await computers.Content.ReadAsStringAsync(Ct));
        Assert.Empty(document.RootElement.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task Existing_partial_new_samples_are_not_filled_published_reclassified_or_given_references()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeEmptyAsync();
        string[] before;
        await using (var db = fixture.CreateContext())
        {
            var brand = new Brand("Editorial brand", "editorial-brand");
            var category = new Category("Editorial category", "editorial-category", 0);
            foreach (var slug in new[] { "nokia-3210", "macintosh-128k", "imac-g3" })
                db.Devices.Add(new Device("Unfinished draft", slug, brand, category));
            await db.SaveChangesAsync(Ct);
            before = await StoredRowsAsync(db);
        }
        await SeedAsync(fixture);
        await using (var db = fixture.CreateContext())
        {
            var after = await StoredRowsAsync(db);
            Assert.All(before, row => Assert.Contains(row, after));
            Assert.Equal(4, await db.Devices.CountAsync(Ct));
            Assert.Equal(1, await db.Devices.CountAsync(x => x.Status == DeviceStatus.Published, Ct));
            Assert.Equal(9, await db.DeviceSpecifications.CountAsync(Ct));
            Assert.False(await db.Brands.AnyAsync(x => x.Slug == "apple", Ct));
            Assert.False(await db.Categories.AnyAsync(x => x.Slug == "computers", Ct));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Incompatible_definition_type_or_unit_aborts_the_current_device_without_partial_inserts(bool wrongType)
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeEmptyAsync();
        await using (var db = fixture.CreateContext())
        {
            db.SpecificationDefinitions.Add(new SpecificationDefinition("Editor CPU clock", "cpu_clock",
                new SpecificationGroup("Processor", "processor", 21),
                wrongType ? SpecificationDataType.Text : SpecificationDataType.Number, 20, wrongType ? "MHz" : "GHz"));
            await db.SaveChangesAsync(Ct);
        }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(fixture));
        Assert.Contains("cpu_clock", error.Message);
        await using (var db = fixture.CreateContext())
        {
            // Earlier devices were saved independently; none of the failed computer's staged rows were saved.
            Assert.Equal(2, await db.Devices.CountAsync(Ct));
            Assert.False(await db.Brands.AnyAsync(x => x.Slug == "apple", Ct));
            Assert.False(await db.Categories.AnyAsync(x => x.Slug == "computers", Ct));
            Assert.False(await db.SpecificationDefinitions.AnyAsync(x => x.Key == "cpu_model", Ct));
            var existing = await db.SpecificationDefinitions.SingleAsync(x => x.Key == "cpu_clock", Ct);
            Assert.Equal("Editor CPU clock", existing.Name);
            Assert.Equal(wrongType ? "MHz" : "GHz", existing.Unit);
            Assert.Equal(wrongType ? SpecificationDataType.Text : SpecificationDataType.Number, existing.DataType);
        }
    }

    [Fact]
    public async Task Incompatible_all_in_one_parent_is_not_silently_reparented()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeEmptyAsync();
        await using (var db = fixture.CreateContext())
        {
            await Nokia3310Seed.SeedAsync(db, Ct);
            var phones = await db.Categories.SingleAsync(x => x.Slug == "phones", Ct);
            db.Categories.Add(new Category("Editor all-in-one", "all-in-one-computers", 99, phones));
            await db.SaveChangesAsync(Ct);
        }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(fixture));
        Assert.Contains("all-in-one-computers", error.Message);
        await using (var db = fixture.CreateContext())
        {
            var category = await db.Categories.Include(x => x.Parent).SingleAsync(x => x.Slug == "all-in-one-computers", Ct);
            Assert.Equal("phones", category.Parent!.Slug);
            Assert.Equal("Editor all-in-one", category.Name);
            Assert.False(await db.Brands.AnyAsync(x => x.Slug == "apple", Ct));
            Assert.False(await db.Categories.AnyAsync(x => x.Slug == "computers", Ct));
            Assert.Equal(2, await db.Devices.CountAsync(Ct));
        }
    }

    private static async Task SeedAsync(MixedCatalogFixture fixture)
    {
        await using var db = fixture.CreateContext();
        await CatalogSeed.SeedAsync(db, Ct);
    }

    private static async Task<string[]> StoredRowsAsync(TechVaultDbContext db)
    {
        var rows = new List<string>();
        // Only these fixed test table names are interpolated; no request/user input enters SQL.
        foreach (var table in new[] { "Brands", "Categories", "Devices", "SpecificationGroups", "SpecificationDefinitions", "DeviceSpecifications" })
        {
            var sql = $"SELECT to_jsonb(record)::text AS \"Value\" FROM \"{table}\" AS record";
            rows.AddRange((await db.Database.SqlQueryRaw<string>(sql).ToListAsync(Ct)).Select(row => table + ":" + row));
        }
        return rows.Order(StringComparer.Ordinal).ToArray();
    }
}
