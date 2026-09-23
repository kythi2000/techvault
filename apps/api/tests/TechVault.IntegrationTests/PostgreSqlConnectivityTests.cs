using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TechVault.Application.Common.Abstractions;
using TechVault.Infrastructure.Persistence;

namespace TechVault.IntegrationTests;

public sealed class PostgreSqlConnectivityTests : IAsyncLifetime
{
    private readonly LocalTestDatabase _postgres = new();

    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken);

        var connectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Timeout = 2,
            CommandTimeout = 2,
            Pooling = false
        }.ConnectionString;

        _factory = new TestApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DATABASE_URL"] = connectionString
                }));
        });
    }

    [Fact]
    public async Task DbContext_connects_to_PostgreSQL_without_creating_tables()
    {
        using var client = _factory.CreateHttpsClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TechVaultDbContext>();
        Assert.Same(db, scope.ServiceProvider.GetRequiredService<ITechVaultDbContext>());
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(await db.Database.CanConnectAsync(cancellationToken));

        var version = await db.Database
            .SqlQuery<string>($"SELECT version() AS \"Value\"")
            .SingleAsync(cancellationToken);
        Assert.StartsWith("PostgreSQL", version);

        var tables = await db.Database
            .SqlQuery<string>($"SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public'")
            .ToListAsync(cancellationToken);
        Assert.Empty(tables);
    }

    [Fact]
    public async Task Readiness_fails_during_database_outage_while_liveness_stays_healthy()
    {
        using var client = _factory.CreateHttpsClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var ready = await client.GetAsync("/health/ready", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync(cancellationToken));

        await _postgres.StopAsync(cancellationToken);

        using var unavailable = await client.GetAsync("/health/ready", cancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("Unhealthy", await unavailable.Content.ReadAsStringAsync(cancellationToken));

        using var live = await client.GetAsync("/health/live", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Api_starts_when_the_configured_database_is_unavailable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _postgres.StopAsync(cancellationToken);

        using var client = _factory.CreateHttpsClient();
        using var live = await client.GetAsync("/health/live", cancellationToken);
        using var ready = await client.GetAsync("/health/ready", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
            }
        }
        finally
        {
            await _postgres.DisposeAsync();
        }
    }
}
