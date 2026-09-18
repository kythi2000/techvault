using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Common;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;
using TechVault.Infrastructure.Persistence;
using TechVault.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace TechVault.IntegrationTests;

// Each test gets its own PostgreSQL container. No local/Compose database is used.
public sealed class CatalogPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("techvault_catalog_tests")
        .WithUsername("techvault_tests")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(CancellationToken);
        await using var db = CreateContext();
        await db.Database.MigrateAsync(CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Initial_migration_round_trips_the_complete_seed_with_ordered_typed_specifications()
    {
        await SeedAsync();
        await using var db = CreateContext();
        var device = await LoadDeviceAsync(db);
        Assert.Equal("Nokia 3310", device.Name);
        Assert.Equal("nokia", device.Brand.Slug);
        Assert.Equal("feature-phones", device.Category.Slug);
        Assert.Equal("phones", device.Category.Parent!.Slug);
        Assert.Equal(DeviceStatus.Published, device.Status);
        Assert.NotNull(device.PublishedAt);
        Assert.True(device.CreatedAt <= device.UpdatedAt);
        Assert.Equal(2000, device.ReleaseYear);
        Assert.Null(device.ReleaseDate);
        Assert.Null(device.DiscontinuedDate);
        Assert.Equal(133m, device.WeightGrams);
        Assert.Null(device.HeightMm);
        Assert.NotEmpty(device.Description);
        Assert.NotEmpty(device.ShortDescription);
        Assert.NotEmpty(device.History);
        Assert.NotEmpty(device.SeoTitle);
        Assert.NotEmpty(device.SeoDescription);

        var ordered = await db.DeviceSpecifications.AsNoTracking().Where(x => x.DeviceId == device.Id)
            .OrderBy(x => x.Definition.Group.DisplayOrder).ThenBy(x => x.Definition.DisplayOrder)
            .ThenBy(x => x.Definition.Key).Select(x => x.Definition.Key).ToListAsync(CancellationToken);
        Assert.Equal(new[] { "announcement_date", "replaceable_covers", "antenna", "battery_chemistry",
            "talk_time_max", "standby_time_max", "network_bands", "sms_chat", "sms_segments_max" }, ordered);
        Assert.Equal(4.5m, device.Specifications.Single(x => x.Definition.Key == "talk_time_max").ValueNumber);
        Assert.Equal("NiMH", device.Specifications.Single(x => x.Definition.Key == "battery_chemistry").ValueText);
        Assert.True(device.Specifications.Single(x => x.Definition.Key == "sms_chat").ValueBoolean);
        Assert.Equal(new DateOnly(2000, 9, 1), device.Specifications.Single(x => x.Definition.Key == "announcement_date").ValueDate);
        Assert.Collection(await db.Database.GetAppliedMigrationsAsync(CancellationToken),
            migration => Assert.EndsWith("_InitialCatalog", migration),
            migration => Assert.EndsWith("_AddCatalogDiscovery", migration),
            migration => Assert.EndsWith("_AddCatalogComparisons", migration));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(CancellationToken));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Null(db.Model.FindEntityType(typeof(BaseEntity)));
        Assert.Equal(7, db.Model.GetEntityTypes().Count());
        Assert.All(db.Model.GetEntityTypes(), entity => Assert.Null(entity.BaseType));

        var tables = await db.Database.SqlQuery<string>($"SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public'")
            .OrderBy(x => x).ToListAsync(CancellationToken);
        Assert.Equal(new[] { "Brands", "Categories", "ComparisonGroups", "DeviceSpecifications", "Devices", "SpecificationDefinitions",
            "SpecificationGroups", "__EFMigrationsHistory" }, tables);
    }

    [Fact]
    public async Task Rerunning_seed_preserves_editorial_edits_timestamps_and_deleted_specifications()
    {
        await SeedAsync();
        Guid deviceId;
        DateTimeOffset createdAt;
        DateTimeOffset updatedAt;
        DateTimeOffset? publishedAt;
        await using (var db = CreateContext())
        {
            var device = await db.Devices.Include(x => x.Specifications).ThenInclude(x => x.Definition)
                .SingleAsync(CancellationToken);
            device.UpdateContent("Edited summary", "Edited description", "Edited history", "Edited title", "Edited metadata");
            device.SetPhysicalDetails(113, 48, 22, 130);
            device.SetSpecification(device.Specifications.Single(x => x.Definition.Key == "sms_chat").Definition,
                SpecificationValue.Boolean(false));
            device.SetSpecification(device.Specifications.Single(x => x.Definition.Key == "talk_time_max").Definition,
                SpecificationValue.Number(0));
            db.DeviceSpecifications.Remove(device.Specifications.Single(x => x.Definition.Key == "antenna"));
            await db.SaveChangesAsync(CancellationToken);
            deviceId = device.Id;
            createdAt = device.CreatedAt;
            updatedAt = device.UpdatedAt;
            publishedAt = device.PublishedAt;
        }
        // Reload timestamps at PostgreSQL precision, then test a completely separate seeding scope.
        await using (var db = CreateContext())
        {
            var saved = await db.Devices.AsNoTracking().SingleAsync(CancellationToken);
            createdAt = saved.CreatedAt;
            updatedAt = saved.UpdatedAt;
            publishedAt = saved.PublishedAt;
        }
        await SeedAsync();
        await SeedAsync();
        await using (var db = CreateContext())
        {
            var device = await LoadDeviceAsync(db);
            Assert.Equal(deviceId, device.Id);
            Assert.Equal("Edited summary", device.ShortDescription);
            Assert.Equal("Edited description", device.Description);
            Assert.Equal("Edited history", device.History);
            Assert.Equal("Edited title", device.SeoTitle);
            Assert.Equal("Edited metadata", device.SeoDescription);
            Assert.Equal(130m, device.WeightGrams);
            Assert.Equal(113m, device.HeightMm);
            Assert.Equal(createdAt, device.CreatedAt);
            Assert.Equal(updatedAt, device.UpdatedAt);
            Assert.Equal(publishedAt, device.PublishedAt);
            Assert.False(device.Specifications.Single(x => x.Definition.Key == "sms_chat").ValueBoolean);
            Assert.Equal(0m, device.Specifications.Single(x => x.Definition.Key == "talk_time_max").ValueNumber);
            Assert.DoesNotContain(device.Specifications, x => x.Definition.Key == "antenna");
            Assert.Equal(8, device.Specifications.Count);
            Assert.Equal(1, await db.Devices.CountAsync(CancellationToken));
            Assert.Equal(1, await db.Brands.CountAsync(CancellationToken));
            Assert.Equal(2, await db.Categories.CountAsync(CancellationToken));
            Assert.Equal(5, await db.SpecificationGroups.CountAsync(CancellationToken));
            Assert.Equal(9, await db.SpecificationDefinitions.CountAsync(CancellationToken));
        }
    }

    [Fact]
    public async Task An_existing_draft_is_not_published_filled_in_or_given_specifications()
    {
        await using (var db = CreateContext())
        {
            db.Devices.Add(new Device("Editor draft", "nokia-3310", new Brand("Nokia", "nokia"),
                new Category("Phones", "phones", 0)));
            await db.SaveChangesAsync(CancellationToken);
        }
        await SeedAsync();
        await using (var db = CreateContext())
        {
            var device = await db.Devices.AsNoTracking().SingleAsync(CancellationToken);
            Assert.Equal("Editor draft", device.Name);
            Assert.Equal(DeviceStatus.Draft, device.Status);
            Assert.Equal("", device.Description);
            Assert.Null(device.PublishedAt);
            Assert.Empty(await db.DeviceSpecifications.ToListAsync(CancellationToken));
            Assert.Empty(await db.SpecificationGroups.ToListAsync(CancellationToken));
        }
    }

    [Fact]
    public async Task Existing_reference_data_is_reused_without_overwriting_editorial_metadata()
    {
        Guid brandId;
        Guid definitionId;
        await using (var db = CreateContext())
        {
            var brand = new Brand("Edited Nokia", "nokia", "Editor brand description");
            var phones = new Category("Edited Phones", "phones", 90, description: "Editor category description");
            var group = new SpecificationGroup("Edited General", "general", 90);
            var definition = new SpecificationDefinition("Edited announcement label", "announcement_date", group,
                SpecificationDataType.Date, 90);
            db.Brands.Add(brand);
            db.Categories.Add(phones);
            db.SpecificationDefinitions.Add(definition);
            await db.SaveChangesAsync(CancellationToken);
            brandId = brand.Id;
            definitionId = definition.Id;
        }
        await SeedAsync();
        await using (var db = CreateContext())
        {
            var device = await LoadDeviceAsync(db);
            Assert.Equal(brandId, device.BrandId);
            Assert.Equal("Editor brand description", device.Brand.Description);
            Assert.Equal("Edited Phones", device.Category.Parent!.Name);
            var spec = device.Specifications.Single(x => x.Definition.Key == "announcement_date");
            Assert.Equal(definitionId, spec.DefinitionId);
            Assert.Equal("Edited announcement label", spec.Definition.Name);
            Assert.Equal("Edited General", spec.Definition.Group.Name);
            Assert.Equal(90, spec.Definition.DisplayOrder);
            Assert.Equal(1, await db.Brands.CountAsync(CancellationToken));
            Assert.Equal(9, await db.SpecificationDefinitions.CountAsync(CancellationToken));
        }
    }

    [Fact]
    public async Task Incompatible_reference_data_aborts_without_partial_catalog_inserts()
    {
        await using (var db = CreateContext())
        {
            db.SpecificationDefinitions.Add(new SpecificationDefinition("Existing announcement", "announcement_date",
                new SpecificationGroup("General", "general", 10), SpecificationDataType.Text, 10));
            await db.SaveChangesAsync(CancellationToken);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(SeedAsync);
        await using (var db = CreateContext())
        {
            Assert.Empty(await db.Devices.ToListAsync(CancellationToken));
            Assert.Empty(await db.Brands.ToListAsync(CancellationToken));
            Assert.Empty(await db.Categories.ToListAsync(CancellationToken));
            Assert.Single(await db.SpecificationGroups.ToListAsync(CancellationToken));
            Assert.Equal(SpecificationDataType.Text, (await db.SpecificationDefinitions.SingleAsync(CancellationToken)).DataType);
        }
    }

    [Fact]
    public async Task PostgreSQL_rejects_duplicate_identifiers_and_device_definition_pairs()
    {
        await SeedAsync();
        await using (var db = CreateContext())
        {
            db.Brands.Add(new Brand("Duplicate", "nokia"));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(CancellationToken));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        // Duplicate via SQL to prove constraints protect imports as well as domain operations.
        var statements = new[]
        {
            "INSERT INTO \"Categories\" (\"Id\", \"Name\", \"Slug\", \"Description\", \"DisplayOrder\", \"ParentCategoryId\") SELECT gen_random_uuid(), \"Name\", \"Slug\", \"Description\", \"DisplayOrder\", \"ParentCategoryId\" FROM \"Categories\" WHERE \"Slug\" = 'phones'",
            "INSERT INTO \"SpecificationGroups\" (\"Id\", \"Name\", \"Key\", \"DisplayOrder\") SELECT gen_random_uuid(), \"Name\", \"Key\", \"DisplayOrder\" FROM \"SpecificationGroups\" LIMIT 1",
            "UPDATE \"SpecificationDefinitions\" SET \"Key\" = 'announcement_date' WHERE \"Key\" = 'antenna'",
            "INSERT INTO \"DeviceSpecifications\" SELECT * FROM \"DeviceSpecifications\" LIMIT 1"
        };
        await using (var db = CreateContext())
        {
            foreach (var statement in statements)
            {
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(statement, CancellationToken));
                Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
            }
            var existing = await db.Devices.Include(x => x.Brand).Include(x => x.Category).SingleAsync(CancellationToken);
            db.Devices.Add(new Device("Duplicate device", existing.Slug, existing.Brand, existing.Category));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(CancellationToken));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        }
    }

    [Fact]
    public async Task PostgreSQL_rejects_invalid_classification_publication_and_specification_values()
    {
        await SeedAsync();
        var checks = new (string Sql, string Code)[]
        {
            ("UPDATE \"Devices\" SET \"BrandId\" = '00000000-0000-0000-0000-000000000000'", PostgresErrorCodes.ForeignKeyViolation),
            ("UPDATE \"Devices\" SET \"CategoryId\" = '00000000-0000-0000-0000-000000000000'", PostgresErrorCodes.ForeignKeyViolation),
            ("UPDATE \"SpecificationDefinitions\" SET \"GroupId\" = '00000000-0000-0000-0000-000000000000'", PostgresErrorCodes.ForeignKeyViolation),
            ("UPDATE \"Categories\" SET \"ParentCategoryId\" = \"Id\"", PostgresErrorCodes.CheckViolation),
            ("UPDATE \"Devices\" SET \"Status\" = 99", PostgresErrorCodes.CheckViolation),
            ("UPDATE \"Devices\" SET \"PublishedAt\" = NULL", PostgresErrorCodes.CheckViolation),
            ("UPDATE \"Devices\" SET \"History\" = ''", PostgresErrorCodes.CheckViolation),
            ("UPDATE \"Devices\" SET \"ReleaseYear\" = 0", PostgresErrorCodes.CheckViolation),
            ("UPDATE \"Devices\" SET \"ReleaseDate\" = '1999-01-01'", PostgresErrorCodes.CheckViolation),
            ("UPDATE \"Devices\" SET \"WeightGrams\" = -1", PostgresErrorCodes.CheckViolation),
            ("UPDATE \"DeviceSpecifications\" SET \"ValueBoolean\" = FALSE WHERE \"DataType\" = 1", PostgresErrorCodes.CheckViolation),
            ("UPDATE \"DeviceSpecifications\" SET \"ValueNumber\" = NULL WHERE \"DataType\" = 2", PostgresErrorCodes.CheckViolation),
            ("UPDATE \"DeviceSpecifications\" SET \"DataType\" = 3, \"ValueBoolean\" = TRUE, \"ValueNumber\" = NULL WHERE \"DataType\" = 2", PostgresErrorCodes.ForeignKeyViolation)
        };
        await using var db = CreateContext();
        foreach (var (sql, code) in checks)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, CancellationToken));
            Assert.Equal(code, error.SqlState);
        }
    }

    [Fact]
    public async Task Migration_can_be_reversed_and_reapplied_on_a_disposable_database()
    {
        await SeedAsync();
        await using var db = CreateContext();
        await db.GetService<IMigrator>().MigrateAsync("0", CancellationToken);
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync(CancellationToken));
        var tables = await db.Database.SqlQuery<string>($"SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public'")
            .ToListAsync(CancellationToken);
        Assert.Equal("__EFMigrationsHistory", Assert.Single(tables));
        await db.Database.MigrateAsync(CancellationToken);
        await Nokia3310Seed.SeedAsync(db, CancellationToken);
        Assert.Equal("nokia-3310", (await db.Devices.SingleAsync(CancellationToken)).Slug);
    }

    [Fact]
    public async Task Migrated_catalog_keeps_health_working_with_public_catalog_endpoints()
    {
        await SeedAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["DATABASE_URL"] = _postgres.GetConnectionString() }));
        });
        using var client = factory.CreateClient();
        using var live = await client.GetAsync("/health/live", CancellationToken);
        using var ready = await client.GetAsync("/health/ready", CancellationToken);
        using var catalog = await client.GetAsync("/api/v1/devices/nokia-3310", CancellationToken);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
    }

    private TechVaultDbContext CreateContext() => new(new DbContextOptionsBuilder<TechVaultDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).Options);

    private async Task SeedAsync()
    {
        await using var db = CreateContext();
        await Nokia3310Seed.SeedAsync(db, CancellationToken);
    }

    private static Task<Device> LoadDeviceAsync(TechVaultDbContext db) => db.Devices.AsNoTracking()
        .Include(x => x.Brand).Include(x => x.Category).ThenInclude(x => x.Parent)
        .Include(x => x.Specifications).ThenInclude(x => x.Definition).ThenInclude(x => x.Group)
        .SingleAsync(CancellationToken);
}
