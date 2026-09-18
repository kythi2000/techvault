using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TechVault.Api.Responses;

namespace TechVault.Api.Authentication;

public static class AdminAuthentication
{
    public const string Scheme = "AdminBearer";
    public const string Policy = "CatalogAdmin";

    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(Scheme).AddScheme<AdminAuthenticationOptions, AdminAuthenticationHandler>(Scheme,
            options => options.ApiKey = configuration["Admin:ApiKey"]);
        services.AddAuthorization(options => options.AddPolicy(Policy, policy =>
            policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireClaim("role", "catalog-admin")));
        return services;
    }
}

public sealed class AdminAuthenticationOptions : AuthenticationSchemeOptions
{
    public string? ApiKey { get; set; }
}

public sealed class AdminAuthenticationHandler(IOptionsMonitor<AdminAuthenticationOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AdminAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = Options.ApiKey;
        // Absent/invalid configuration disables admin access; public endpoints remain available.
        if (expected is null || expected.Length is < 32 or > 256 || expected.Any(char.IsWhiteSpace))
            return Task.FromResult(AuthenticateResult.NoResult());
        if (!Request.Headers.TryGetValue("Authorization", out var headers) || headers.Count != 1 ||
            !AuthenticationHeaderValue.TryParse(headers[0], out var header) ||
            !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
            header.Parameter is not { Length: >= 32 and <= 256 } candidate)
            return Task.FromResult(AuthenticateResult.NoResult());

        var valid = CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(candidate)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
        if (!valid) return Task.FromResult(AuthenticateResult.Fail("Invalid admin credential."));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "catalog-admin"), new Claim("role", "catalog-admin")], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Response.WriteAsJsonAsync(new ApiErrorResponse(new("UNAUTHORIZED", "Admin authentication is required.",
            ApiResponses.TraceId(Context))), Context.RequestAborted);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Response.WriteAsJsonAsync(new ApiErrorResponse(new("FORBIDDEN", "Admin access is required.",
            ApiResponses.TraceId(Context))), Context.RequestAborted);
    }
}
