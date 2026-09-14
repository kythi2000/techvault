using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.IntegrationTests;

public sealed class PublicCatalogChangesTests
{
    [Fact]
    public async Task Public_reads_reflect_editorial_changes_visibility_and_database_failure()
    {
        // This test mutates only its own database, never the shared read-only HTTP fixture.
        await using var fixture = new PublicCatalogFixture();
        await fixture.InitializeAsync();
        var ct = TestContext.Current.CancellationToken;
        await using (var db = fixture.CreateContext())
        {
            var device = await db.Devices.Include(x => x.Specifications).ThenInclude(x => x.Definition)
                .SingleAsync(x => x.Slug == "nokia-3310", ct);
            device.UpdateContent("Updated summary", "Updated description", "Updated history", "Updated title", "Updated SEO");
            device.SetSpecification(device.Specifications.Single(x => x.Definition.Key == "sms_chat").Definition,
                SpecificationValue.Boolean(false));
            device.SetSpecification(device.Specifications.Single(x => x.Definition.Key == "talk_time_max").Definition,
                SpecificationValue.Number(0));
            db.DeviceSpecifications.Remove(device.Specifications.Single(x => x.Definition.Key == "antenna"));
            await db.SaveChangesAsync(ct);
        }
        using (var response = await fixture.Client.GetAsync("/api/v1/devices/nokia-3310", ct))
        {
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var device = document.RootElement.GetProperty("data");
            Assert.Equal("Updated history", device.GetProperty("history").GetString());
            Assert.Equal("Updated title", device.GetProperty("seoTitle").GetString());
            var specs = device.GetProperty("specificationGroups").EnumerateArray()
                .SelectMany(x => x.GetProperty("specifications").EnumerateArray()).ToArray();
            Assert.Equal(8, specs.Length);
            Assert.False(specs.Single(x => x.GetProperty("key").GetString() == "sms_chat").GetProperty("valueBoolean").GetBoolean());
            Assert.Equal(0m, specs.Single(x => x.GetProperty("key").GetString() == "talk_time_max").GetProperty("valueNumber").GetDecimal());
            Assert.DoesNotContain(specs, x => x.GetProperty("key").GetString() == "antenna");
        }
        await using (var db = fixture.CreateContext())
        {
            var device = await db.Devices.SingleAsync(x => x.Slug == "nokia-3310", ct);
            db.Entry(device).Property(x => x.Status).CurrentValue = DeviceStatus.Draft;
            db.Entry(device).Property(x => x.PublishedAt).CurrentValue = null;
            await db.SaveChangesAsync(ct);
        }
        foreach (var path in new[] { "/api/v1/devices/nokia-3310", "/api/v1/devices/nokia-3310/specifications" })
        {
            using var response = await fixture.Client.GetAsync(path, ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using (var response = await fixture.Client.GetAsync("/api/v1/phones?brand=nokia", ct))
        {
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);
            Assert.DoesNotContain("nokia-3310", body);
            using var document = JsonDocument.Parse(body);
            Assert.Equal(5, document.RootElement.GetProperty("pagination").GetProperty("total").GetInt32());
        }
        using (var response = await fixture.Client.GetAsync("/api/v1/brands/nokia", ct))
        {
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            Assert.Equal(5, document.RootElement.GetProperty("data").GetProperty("publishedDeviceCount").GetInt32());
        }

        await fixture.StopDatabaseAsync(ct);
        using var failed = await fixture.Client.GetAsync("/api/v1/devices", ct);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using var failure = JsonDocument.Parse(await failed.Content.ReadAsStringAsync(ct));
        var error = failure.RootElement.GetProperty("error");
        Assert.Equal("UNEXPECTED_ERROR", error.GetProperty("code").GetString());
        Assert.Equal("An unexpected error occurred.", error.GetProperty("message").GetString());
        Assert.Equal(3, error.EnumerateObject().Count());
        Assert.Equal(Assert.Single(failed.Headers.GetValues("X-Trace-Id")), error.GetProperty("traceId").GetString());
        using var live = await fixture.Client.GetAsync("/health/live", ct);
        using var ready = await fixture.Client.GetAsync("/health/ready", ct);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }
}
