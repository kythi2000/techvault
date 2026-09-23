using Npgsql;

namespace TechVault.IntegrationTests;

// No Docker or application connection fallback. Own a newly generated database, never a caller's database.
public sealed class LocalTestDatabase : IAsyncDisposable
{
    private string? _maintenanceConnection;
    private bool _created;
    public string DatabaseName { get; } = "techvault_tests_" + Guid.NewGuid().ToString("N");

    public static NpgsqlConnectionStringBuilder ReadMaintenanceConfiguration()
    {
        var value = Environment.GetEnvironmentVariable("TECHVAULT_TEST_DATABASE_URL");
        NpgsqlConnectionStringBuilder configuration;
        try { configuration = new NpgsqlConnectionStringBuilder(value); }
        catch { throw new InvalidOperationException("Invalid TECHVAULT_TEST_DATABASE_URL. Configure a loopback PostgreSQL maintenance connection; see docs/operations/TESTING.md."); }
        if (string.IsNullOrWhiteSpace(value) || configuration.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            configuration.Database != "postgres" || string.IsNullOrWhiteSpace(configuration.Username))
            throw new InvalidOperationException("Tests require TECHVAULT_TEST_DATABASE_URL with Host=localhost/127.0.0.1/::1 and Database=postgres; never use the application database.");
        configuration.Pooling = false;
        configuration.Timeout = 3;
        configuration.CommandTimeout = 10;
        configuration.IncludeErrorDetail = false;
        return configuration;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_created) throw new InvalidOperationException("The test database already exists.");
        var configuration = ReadMaintenanceConfiguration();
        _maintenanceConnection = configuration.ConnectionString;
        await using var connection = new NpgsqlConnection(_maintenanceConnection);
        await connection.OpenAsync(ct);
        // Name is generated internally; CREATE (not IF NOT EXISTS) ensures this instance owns the destination.
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{DatabaseName}\" TEMPLATE template0", connection);
        await command.ExecuteNonQueryAsync(ct);
        _created = true;
    }

    public string GetConnectionString()
    {
        if (!_created) throw new InvalidOperationException("Start the isolated database first.");
        return new NpgsqlConnectionStringBuilder(_maintenanceConnection!) { Database = DatabaseName }.ConnectionString;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (!_created) return;
        await using var connection = new NpgsqlConnection(_maintenanceConnection);
        await connection.OpenAsync(ct);
        await using var block = new NpgsqlCommand($"ALTER DATABASE \"{DatabaseName}\" ALLOW_CONNECTIONS false", connection);
        await block.ExecuteNonQueryAsync(ct);
        await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @database AND pid <> pg_backend_pid()", connection);
        terminate.Parameters.AddWithValue("database", DatabaseName);
        await terminate.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_created) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var connection = new NpgsqlConnection(_maintenanceConnection);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{DatabaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync(timeout.Token);
        _created = false;
    }
}
