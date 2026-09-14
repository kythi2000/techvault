using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;
using TechVault.Infrastructure.Persistence;
using TechVault.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace TechVault.IntegrationTests;

// Read-only HTTP tests share this isolated database. Synthetic records never enter the production seed.
public sealed class PublicCatalogFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("techvault_public_api_tests")
        .WithUsername("techvault_tests")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    private string ConnectionString => new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        { Timeout = 2, CommandTimeout = 5 }.ConnectionString;

    public TechVaultDbContext CreateContext() => new(new DbContextOptionsBuilder<TechVaultDbContext>()
        .UseNpgsql(ConnectionString).Options);

    public WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["DATABASE_URL"] = ConnectionString }));
        });

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await _postgres.StartAsync(ct);
        await using var db = CreateContext();
        await db.Database.MigrateAsync(ct);
        await Nokia3310Seed.SeedAsync(db, ct);

        var nokia = await db.Brands.SingleAsync(ct);
        var featurePhones = await db.Categories.SingleAsync(x => x.Slug == "feature-phones", ct);
        var nested = new Category("Nested test subtype", "test-subtype", 20, featurePhones);
        var computers = new Category("Computers", "computers", 20);
        var desktops = new Category("Desktops", "desktops", 10, computers);
        var otherBrand = new Brand("Test Brand", "test-brand");
        var emptyBrand = new Brand("Unused Brand", "unused-brand");
        db.Categories.AddRange(nested, desktops);
        db.Brands.AddRange(otherBrand, emptyBrand);
        db.Devices.AddRange(
            Published("fixture-old", 1999, nokia, featurePhones),
            Published("fixture-a", 2000, nokia, featurePhones),
            Published("fixture-b", 2000, nokia, nested),
            Published("fixture-recent", 2001, nokia, featurePhones),
            Published("fixture-unknown", null, nokia, featurePhones),
            Published("fixture-other", 2000, otherBrand, featurePhones));

        var draft = new Device("Private draft", "private-draft", nokia, featurePhones);
        var definition = await db.SpecificationDefinitions.SingleAsync(x => x.Key == "battery_chemistry", ct);
        draft.SetSpecification(definition, SpecificationValue.Text("Private specification"));
        var archived = new Device("Private archive", "private-archive", nokia, featurePhones);
        db.Devices.AddRange(draft, archived, new Device("Private computer", "private-computer", emptyBrand, desktops));
        // Archive writes are a later phase; set persisted status only in this visibility fixture.
        db.Entry(archived).Property(x => x.Status).CurrentValue = DeviceStatus.Archived;
        await db.SaveChangesAsync(ct);

        Factory = CreateFactory("Production");
        Client = Factory.CreateClient();
    }

    public Task StopDatabaseAsync(CancellationToken cancellationToken) => _postgres.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null) await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private static Device Published(string slug, int? year, Brand brand, Category category)
    {
        var device = new Device("Fixture device", slug, brand, category);
        device.UpdateContent("Fixture summary", "Fixture description", "Fixture history", "Fixture title", "Fixture SEO");
        device.SetRelease(year);
        device.Publish();
        return device;
    }
}
