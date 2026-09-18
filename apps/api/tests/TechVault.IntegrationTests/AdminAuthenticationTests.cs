using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechVault.Api.Authentication;
using TechVault.Api.Responses;

namespace TechVault.IntegrationTests;

public sealed class AdminAuthenticationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("basic")]
    [InlineData("query")]
    [InlineData("duplicate")]
    public async Task Every_admin_route_requires_the_header_credential_before_binding_or_database_access(string mode)
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var factory = CreateFactory(key);
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(x => x.RoutePattern.RawText!.StartsWith("/api/v1/admin", StringComparison.Ordinal)).ToArray();
        Assert.Equal(31, endpoints.Length);
        foreach (var endpoint in endpoints)
        {
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), x => x.Policy == AdminAuthentication.Policy);
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            var path = endpoint.RoutePattern.RawText!.Replace("{id:guid}", Guid.NewGuid().ToString())
                .Replace("{definitionId:guid}", Guid.NewGuid().ToString());
            if (mode == "query") path += $"?apiKey={Uri.EscapeDataString(key)}";
            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                if (mode is "wrong" or "basic") request.Headers.Authorization = new(mode == "basic" ? "Basic" : "Bearer",
                    mode == "wrong" ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) : key);
                if (mode == "duplicate") request.Headers.TryAddWithoutValidation("Authorization", new[] { $"Bearer {key}", $"Bearer {key}" });
                using var response = await client.SendAsync(request, Ct);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.Contains(response.Headers.WwwAuthenticate, x => x.Scheme == "Bearer");
                var error = (await response.Content.ReadFromJsonAsync<ApiErrorResponse>(Ct))!.Error;
                Assert.Equal("UNAUTHORIZED", error.Code);
                Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), error.TraceId);
                Assert.DoesNotContain(key, await response.Content.ReadAsStringAsync(Ct));
            }
        }
        using var live = await client.GetAsync("/health/live", Ct);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("contains whitespace even though this value is long enough")]
    public async Task Missing_or_invalid_configuration_disables_admin_only(string? configuredKey)
    {
        await using var factory = CreateFactory(configuredKey);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", configuredKey is { Length: > 0 } ? configuredKey : "anything");
        using var denied = await client.GetAsync("/api/v1/admin/devices", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var live = await client.GetAsync("/health/live", Ct);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Correct_key_reaches_validation_and_malformed_json_uses_the_standard_error_envelope()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var factory = CreateFactory(key);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", key);
        using var invalidPage = await client.GetAsync("/api/v1/admin/devices?page=0", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        using var body = new StringContent("{invalid", System.Text.Encoding.UTF8, "application/json");
        using var malformed = await client.PostAsync("/api/v1/admin/devices", body, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await malformed.Content.ReadFromJsonAsync<ApiErrorResponse>(Ct))!.Error.Code);
    }

    [Fact]
    public async Task Development_openapi_includes_admin_contracts_without_exposing_credentials()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var factory = CreateFactory(key, "Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(key, json);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var path = document.RootElement.GetProperty("paths").EnumerateObject()
            .Single(x => x.Name.TrimEnd('/') == "/api/v1/admin/devices").Value;
        Assert.True(path.GetProperty("get").GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(path.GetProperty("post").GetProperty("responses").TryGetProperty("201", out _));
    }

    private static WebApplicationFactory<Program> CreateFactory(string? key, string environment = "Production") => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:ApiKey"] = key,
                ["DATABASE_URL"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1"
            }));
        });
}
