using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using TechVault.Api.Responses;

namespace TechVault.IntegrationTests;

public sealed class AdminCatalogFixture : IAsyncLifetime
{
    public MixedCatalogFixture Catalog { get; } = new();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Catalog.InitializeAsync();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Factory = Catalog.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:ApiKey"] = key })));
        Client = Factory.CreateHttpsClient();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null) await Factory.DisposeAsync();
        await Catalog.DisposeAsync();
    }
}

internal static class AdminTestHttp
{
    internal static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static async Task<T> Data<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using (response)
        {
            Assert.Equal(expected, response.StatusCode);
            if (expected == HttpStatusCode.Created) Assert.NotNull(response.Headers.Location);
            return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(Ct))!.Data;
        }
    }

    internal static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code = "VALIDATION_ERROR")
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            var error = (await response.Content.ReadFromJsonAsync<ApiErrorResponse>(Ct))!.Error;
            Assert.Equal(code, error.Code);
            Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), error.TraceId);
        }
    }
}
