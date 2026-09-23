using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TechVault.IntegrationTests;

// Production safety defaults; individual tests override only what they exercise.
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "localhost",
            ["Operations:HttpsOrigin"] = "https://localhost",
            ["Operations:PublicRequestsPerMinute"] = "100000",
            ["Operations:AdminRequestsPerMinute"] = "100000",
            ["Operations:ReadinessRequestsPerMinute"] = "100000",
            ["Admin:ApiKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["DATABASE_URL"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1;Command Timeout=1"
        }));
    }
}

internal static class TestApiClients
{
    internal static HttpClient CreateHttpsClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
}
