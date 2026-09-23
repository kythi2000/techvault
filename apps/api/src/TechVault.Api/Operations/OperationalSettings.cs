using System.Net;
using Microsoft.Extensions.Options;
using TechVault.Api.Authentication;
using TechVault.Infrastructure.Persistence;

namespace TechVault.Api.Operations;

public sealed class OperationalSettings
{
    public string? HttpsOrigin { get; set; }
    public string[] AllowedOrigins { get; set; } = [];
    public string[] TrustedProxies { get; set; } = [];
    public int MaxRequestBodyBytes { get; set; } = 1_048_576;
    public int PublicRequestsPerMinute { get; set; } = 120;
    public int AdminRequestsPerMinute { get; set; } = 30;
    public int ReadinessRequestsPerMinute { get; set; } = 30;
}

public sealed class OperationalSettingsValidator(IConfiguration configuration, IHostEnvironment environment)
    : IValidateOptions<OperationalSettings>
{
    public ValidateOptionsResult Validate(string? name, OperationalSettings options)
    {
        var errors = new List<string>();
        if (!DatabaseConfiguration.IsValid(configuration["DATABASE_URL"]))
            errors.Add("DATABASE_URL must be a valid Npgsql connection with host, database, username, and bounded positive timeouts. See docs/operations/README.md.");
        if (options.MaxRequestBodyBytes is < 1024 or > 10_485_760)
            errors.Add("Operations:MaxRequestBodyBytes must be between 1024 and 10485760.");
        if (new[] { options.PublicRequestsPerMinute, options.AdminRequestsPerMinute, options.ReadinessRequestsPerMinute }.Any(x => x is < 1 or > 100_000))
            errors.Add("Operations rate limits must be between 1 and 100000 requests per minute.");
        if (options.TrustedProxies.Any(x => !IPAddress.TryParse(x, out _)))
            errors.Add("Operations:TrustedProxies must contain individual IP addresses, not wildcards or networks.");
        if (options.AllowedOrigins.Any(x => !IsOrigin(x, environment.IsDevelopment())))
            errors.Add("Operations:AllowedOrigins must contain exact HTTPS origins (loopback HTTP is allowed in Development only).");
        if (options.HttpsOrigin is not null && !IsOrigin(options.HttpsOrigin, false))
            errors.Add("Operations:HttpsOrigin must be an HTTPS origin without a path, query, or credentials.");
        if (environment.IsProduction())
        {
            if (!IsOrigin(options.HttpsOrigin, false)) errors.Add("Production requires Operations:HttpsOrigin.");
            if (!AdminAuthentication.IsValidKey(configuration["Admin:ApiKey"]))
                errors.Add("Production requires a securely generated Admin:ApiKey of 32–256 non-whitespace characters.");
            var hosts = configuration["AllowedHosts"]?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            if (hosts.Length == 0 || hosts.Any(x => x.Contains('*')) ||
                (Uri.TryCreate(options.HttpsOrigin, UriKind.Absolute, out var origin) && !hosts.Contains(origin.Host, StringComparer.OrdinalIgnoreCase)))
                errors.Add("Production AllowedHosts must explicitly include the HTTPS origin host and contain no wildcard.");
            if (configuration.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED"))
                errors.Add("Disable ASPNETCORE_FORWARDEDHEADERS_ENABLED; configure Operations:TrustedProxies explicitly.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsOrigin(string? value, bool allowLocalHttp) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || (allowLocalHttp && uri.Scheme == "http" && uri.IsLoopback)) &&
        string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) && value == uri.GetLeftPart(UriPartial.Authority);
}
