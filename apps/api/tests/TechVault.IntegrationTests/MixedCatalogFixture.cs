using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TechVault.Infrastructure.Persistence;
using TechVault.Infrastructure.Persistence.Seed;

namespace TechVault.IntegrationTests;

// The real four-device seed, without synthetic browse records or any local database connection.
public sealed class MixedCatalogFixture : IAsyncLifetime
{
    private readonly LocalTestDatabase _postgres = new();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public TechVaultDbContext CreateContext() => new(new DbContextOptionsBuilder<TechVaultDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).Options);

    public ValueTask InitializeAsync() => StartAsync(seed: true);
    public ValueTask InitializeEmptyAsync() => StartAsync(seed: false);

    private async ValueTask StartAsync(bool seed)
    {
        var ct = TestContext.Current.CancellationToken;
        await _postgres.StartAsync(ct);
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync(ct);
            if (seed) await CatalogSeed.SeedAsync(db, ct);
        }
        Factory = new TestApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["DATABASE_URL"] = _postgres.GetConnectionString() }));
        });
        Client = Factory.CreateHttpsClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null) await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
