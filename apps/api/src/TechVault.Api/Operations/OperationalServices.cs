using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using TechVault.Api.Responses;

namespace TechVault.Api.Operations;

public static class OperationalServices
{
    public static void AddOperations(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((_, logging) => ConfigureLogs(logging).WriteTo.Console(new JsonFormatter()));
        builder.Services.AddOptions<OperationalSettings>().BindConfiguration("Operations").ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<OperationalSettings>, OperationalSettingsValidator>();
        builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
        builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<OperationalSettings>>((options, settings) =>
        {
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            options.ForwardLimit = 1;
            options.ForwardedHeaders = settings.Value.TrustedProxies.Length == 0 ? ForwardedHeaders.None :
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var address in settings.Value.TrustedProxies)
            {
                var ip = IPAddress.Parse(address);
                options.KnownProxies.Add(ip);
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) options.KnownProxies.Add(ip.MapToIPv6());
            }
        });
        builder.Services.AddCors();
        builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IOptions<OperationalSettings>>((options, settings) => options.AddPolicy("Catalog", policy =>
            {
                if (settings.Value.AllowedOrigins.Length > 0) policy.WithOrigins(settings.Value.AllowedOrigins);
                // Public browser reads only. Admin clients are trusted non-browser tools, not credentialed CORS.
                policy.WithMethods("GET", "HEAD").WithHeaders("Accept", "Content-Type").WithExposedHeaders("X-Trace-Id");
            }));
        builder.Services.AddRateLimiter(_ => { });
        builder.Services.AddOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>()
            .Configure<IOptions<OperationalSettings>>((options, settings) =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var path = context.Request.Path;
                    if (path == "/health/live") return RateLimitPartition.GetNoLimiter("live");
                    var bucket = path.StartsWithSegments("/api/v1/admin") ? "admin" : path == "/health/ready" ? "ready" : "public";
                    var limit = bucket == "admin" ? settings.Value.AdminRequestsPerMinute :
                        bucket == "ready" ? settings.Value.ReadinessRequestsPerMinute : settings.Value.PublicRequestsPerMinute;
                    // Never partition by untrusted headers or credentials. Forwarded IPs are resolved first, from known proxies only.
                    var ip = context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter($"{bucket}:{ip}", _ => new()
                    { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
                });
                options.OnRejected = async (rejected, ct) =>
                {
                    var response = rejected.HttpContext.Response;
                    response.StatusCode = StatusCodes.Status429TooManyRequests;
                    var seconds = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : 60;
                    response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                    await response.WriteAsJsonAsync(new ApiErrorResponse(new("RATE_LIMITED", "Too many requests; retry later.",
                        ApiResponses.TraceId(rejected.HttpContext))), ct);
                };
            });
    }

    // Allowlisted event sources and explicitly selected fields, not regex-based scrubbing of arbitrary messages.
    // No automatic request logging, request scopes, SQL logging, or raw exception serialization.
    public static LoggerConfiguration ConfigureLogs(LoggerConfiguration logging) => logging.MinimumLevel.Information()
        .Filter.ByIncludingOnly(log => log.Properties.TryGetValue("SourceContext", out var source) && source is ScalarValue { Value: string name } &&
            (name == typeof(RequestLogMiddleware).FullName || name == typeof(Middleware.ApiErrorHandlingMiddleware).FullName || name == "TechVault.Operations"));
}
