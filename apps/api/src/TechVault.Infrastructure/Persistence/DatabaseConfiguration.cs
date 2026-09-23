using Npgsql;

namespace TechVault.Infrastructure.Persistence;

public static class DatabaseConfiguration
{
    public static bool IsValid(string? value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var parsed = new NpgsqlConnectionStringBuilder(value);
            return !string.IsNullOrWhiteSpace(parsed.Host) && !string.IsNullOrWhiteSpace(parsed.Database) &&
                !string.IsNullOrWhiteSpace(parsed.Username) && parsed.Timeout is > 0 and <= 30 && parsed.CommandTimeout is > 0 and <= 60;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException) { return false; }
    }

    public static string ForApplication(string? value)
    {
        if (!IsValid(value)) throw new InvalidOperationException("Invalid DATABASE_URL configuration. See docs/operations/README.md.");
        // Never include PostgreSQL detail fields or retain credentials in connection diagnostics.
        return new NpgsqlConnectionStringBuilder(value!) { IncludeErrorDetail = false, PersistSecurityInfo = false }.ConnectionString;
    }
}
