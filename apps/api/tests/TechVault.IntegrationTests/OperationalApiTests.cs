using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TechVault.Api.Middleware;
using TechVault.Api.Operations;
using TechVault.Api.Responses;

namespace TechVault.IntegrationTests;

public sealed class OperationalApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("DATABASE_URL", "Password=must-not-leak")]
    [InlineData("DATABASE_URL", "Host=localhost;Database=unused;Username=unused;Port=must-not-leak")]
    [InlineData("DATABASE_URL", "Host=localhost;Database=unused;Username=unused;Command Timeout=0")]
    [InlineData("Admin:ApiKey", "must-not-leak")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("Operations:HttpsOrigin", "http://localhost")]
    [InlineData("Operations:HttpsOrigin", "")]
    [InlineData("Operations:AllowedOrigins:0", "*")]
    [InlineData("Operations:AllowedOrigins:0", "https://localhost/path")]
    [InlineData("Operations:TrustedProxies:0", "10.0.0.0/8")]
    [InlineData("Operations:MaxRequestBodyBytes", "0")]
    [InlineData("Operations:AdminRequestsPerMinute", "0")]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")]
    public async Task Unsafe_production_configuration_fails_startup_without_printing_values(string key, string value)
    {
        await using var factory = Factory(new() { [key] = value });
        var exception = Assert.ThrowsAny<Exception>(() => { using var client = factory.CreateHttpsClient(); });
        Assert.DoesNotContain("must-not-leak", exception.ToString());
    }

    [Fact]
    public async Task Https_redirect_uses_canonical_origin_and_never_redirects_admin_credentials_or_writes()
    {
        await using var factory = Factory(new());
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var redirect = await client.GetAsync("/api/v1/devices?page=2", Ct);
        Assert.Equal(HttpStatusCode.PermanentRedirect, redirect.StatusCode);
        Assert.Equal("https://localhost/api/v1/devices?page=2", redirect.Headers.Location!.ToString());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/devices");
        request.Headers.Authorization = new("Bearer", "private-credential");
        using var credentials = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, credentials.StatusCode);
        Assert.Null(credentials.Headers.Location);
        Assert.Equal("HTTPS_REQUIRED", (await credentials.Content.ReadFromJsonAsync<ApiErrorResponse>(Ct))!.Error.Code);
        using var write = await client.PostAsJsonAsync("/api/v1/admin/devices", new { }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, write.StatusCode);
        using var live = await client.GetAsync("/health/live", Ct);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.False(live.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Cors_allows_only_exact_configured_public_read_origins_and_never_cookie_or_admin_credentials()
    {
        await using var factory = Factory(new() { ["Operations:AllowedOrigins:0"] = "https://museum.example" });
        using var client = factory.CreateHttpsClient();
        foreach (var origin in new[] { "https://museum.example", "https://untrusted.example", "https://museum.example.evil" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/devices");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");
            using var response = await client.SendAsync(request, Ct);
            Assert.Equal(origin == "https://museum.example", response.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        }
        using var adminRequest = new HttpRequestMessage(HttpMethod.Options, "/api/v1/admin/devices");
        adminRequest.Headers.Add("Origin", "https://museum.example");
        adminRequest.Headers.Add("Access-Control-Request-Method", "POST");
        adminRequest.Headers.Add("Access-Control-Request-Headers", "authorization");
        using var denied = await client.SendAsync(adminRequest, Ct);
        Assert.DoesNotContain("POST", string.Join(",", denied.Headers.Where(x => x.Key == "Access-Control-Allow-Methods").SelectMany(x => x.Value)));
        Assert.DoesNotContain("authorization", string.Join(",", denied.Headers.Where(x => x.Key == "Access-Control-Allow-Headers").SelectMany(x => x.Value)));
    }

    [Fact]
    public async Task Rate_limits_cover_unauthorized_admin_attempts_ignore_spoofed_forwarded_headers_and_keep_liveness_available()
    {
        await using var factory = Factory(new() { ["Operations:AdminRequestsPerMinute"] = "2", ["Operations:PublicRequestsPerMinute"] = "1" });
        using var client = factory.CreateHttpsClient();
        for (var index = 0; index < 3; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/devices");
            request.Headers.Add("X-Forwarded-For", $"192.0.2.{index + 1}");
            using var response = await client.SendAsync(request, Ct);
            Assert.Equal(index < 2 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests, response.StatusCode);
            if (index == 2)
            {
                Assert.NotNull(response.Headers.RetryAfter);
                Assert.Equal("RATE_LIMITED", (await response.Content.ReadFromJsonAsync<ApiErrorResponse>(Ct))!.Error.Code);
            }
        }
        using var firstPublic = await client.GetAsync("/api/v1/devices?page=0", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, firstPublic.StatusCode);
        using var secondPublic = await client.GetAsync("/api/v1/devices?page=0", Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondPublic.StatusCode);
        for (var index = 0; index < 3; index++) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", Ct)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Oversized_known_and_unknown_length_bodies_return_413_with_safe_headers(bool unknownLength)
    {
        const string key = "test-only-generated-length-placeholder-credential";
        await using var factory = Factory(new() { ["Operations:MaxRequestBodyBytes"] = "1024", ["Admin:ApiKey"] = key });
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", key);
        var json = "{\"name\":\"" + new string('a', 2048) + "\"}";
        using HttpContent body = unknownLength ? new UnknownLengthContent(json) : new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/admin/brands", body, Ct);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiErrorResponse>(Ct))!.Error;
        Assert.Equal("PAYLOAD_TOO_LARGE", error.Code);
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), error.TraceId);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
        Assert.False(response.Headers.Contains("Server"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Only_explicitly_trusted_proxy_can_set_scheme_and_remote_address(bool trusted)
    {
        using var loggers = LoggerFactory.Create(_ => { });
        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, ForwardLimit = 1 };
        options.KnownProxies.Clear(); options.KnownIPNetworks.Clear();
        options.KnownProxies.Add(IPAddress.Parse("192.0.2.10"));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(trusted ? "192.0.2.10" : "192.0.2.11");
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.5";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, loggers, Options.Create(options));
        await middleware.Invoke(context);
        Assert.Equal(trusted ? "https" : "http", context.Request.Scheme);
        Assert.Equal(trusted ? "198.51.100.5" : "192.0.2.11", context.Connection.RemoteIpAddress!.ToString());
    }

    [Fact]
    public async Task Structured_logs_keep_trace_status_and_duration_without_request_or_exception_secrets()
    {
        var sink = new MemorySink();
        using var logger = OperationalServices.ConfigureLogs(new LoggerConfiguration()).WriteTo.Sink(sink).CreateLogger();
        using var loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(logger, dispose: false);
        var errorHandler = new ApiErrorHandlingMiddleware(_ => throw new InvalidOperationException("Password=exception-secret"),
            loggerFactory.CreateLogger<ApiErrorHandlingMiddleware>());
        var requestLogger = new RequestLogMiddleware(errorHandler.InvokeAsync, loggerFactory.CreateLogger<RequestLogMiddleware>());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/devices/path-secret";
        context.Request.QueryString = new("?token=query-secret");
        context.Request.Headers.Authorization = "Bearer header-secret";
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/api/v1/devices/{slug}"), 0, EndpointMetadataCollection.Empty, "Device"));
        using var responseBody = new MemoryStream(); context.Response.Body = responseBody;
        await requestLogger.InvokeAsync(context);
        loggerFactory.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command").LogError("Password=sql-secret");
        Assert.Equal(500, context.Response.StatusCode);
        var events = sink.Events.ToArray();
        Assert.Equal(2, events.Length);
        Assert.All(events, entry => Assert.Null(entry.Exception));
        var request = events.Single(x => x.Properties.ContainsKey("DurationMs"));
        Assert.Equal(500, ((ScalarValue)request.Properties["StatusCode"]).Value);
        Assert.Contains("/api/v1/devices/{slug}", request.Properties["RequestPath"].ToString());
        Assert.Equal(events[0].Properties["TraceId"].ToString(), events[1].Properties["TraceId"].ToString());
        var text = string.Join("\n", events.Select(x => x.RenderMessage()));
        Assert.DoesNotContain("secret", text);
        Assert.Contains("InvalidOperationException", text);
        responseBody.Position = 0;
        var response = await new StreamReader(responseBody).ReadToEndAsync(Ct);
        Assert.DoesNotContain("secret", response);
        Assert.Contains("UNEXPECTED_ERROR", response);
    }

    private static WebApplicationFactory<Program> Factory(Dictionary<string, string?> settings) => new TestApiFactory()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
        });

    private sealed class MemorySink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _bytes;
        public UnknownLengthContent(string value)
        {
            _bytes = Encoding.UTF8.GetBytes(value);
            Headers.ContentType = new("application/json");
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(_bytes).AsTask();
    }
}
