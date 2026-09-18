using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechVault.Api.Responses;
using TechVault.Application.Admin.Devices;
using TechVault.Application.Admin.References;
using TechVault.Application.Brands;
using TechVault.Application.Comparisons;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Application.Devices.GetDevice;
using TechVault.Domain.Devices;
using TechVault.Infrastructure.Persistence;
using static TechVault.IntegrationTests.AdminTestHttp;

namespace TechVault.IntegrationTests;

public sealed class AdminDeviceTests(AdminCatalogFixture fixture) : IClassFixture<AdminCatalogFixture>
{
    private HttpClient Admin => fixture.Client;
    private HttpClient Public => fixture.Catalog.Client;

    [Fact]
    public async Task Editorial_lifecycle_updates_public_reads_search_comparison_and_visibility_without_deleting_content()
    {
        var brand = await Data<AdminBrandResponse>(await Admin.PostAsJsonAsync("/api/v1/admin/brands",
            new BrandInput("Editorial Manufacturer", "editorial-manufacturer"), Ct), HttpStatusCode.Created);
        var input = (await Input("editorial-phone")) with { BrandId = brand.Id };
        var state = await Data<AdminDeviceState>(await Admin.PostAsJsonAsync("/api/v1/admin/devices", input, Ct), HttpStatusCode.Created);
        var path = $"/api/v1/admin/devices/{state.Id}";
        Assert.Equal("draft", state.Status);
        Assert.Null(state.PublishedAt);
        await Visibility(input.Slug, brand.Slug, false);
        await Error(await Admin.PostAsync(path + "/publish", null, Ct), HttpStatusCode.BadRequest);
        input = input with
        {
            ShortDescription = "Editorial phone summary", Description = "Editorial device overview",
            History = "Recorded history", SeoTitle = "Editorial phone", SeoDescription = "Historical device",
            ModelNumber = "Orbitmodel", Aliases = ["Oldalias"], ReleaseYear = 2001, WeightGrams = 99
        };
        await Data<AdminDeviceState>(await Admin.PutAsJsonAsync(path, input, Ct));
        Guid definitionId;
        await using (var db = fixture.Catalog.CreateContext())
            definitionId = await db.SpecificationDefinitions.Where(x => x.Key == "talk_time_max").Select(x => x.Id).SingleAsync(Ct);
        var specPath = path + $"/specifications/{definitionId}";
        await Data<AdminDeviceState>(await Admin.PutAsJsonAsync(specPath, new SpecificationInput(ValueNumber: 0), Ct));
        await Data<AdminDeviceState>(await Admin.PutAsJsonAsync(specPath, new SpecificationInput(ValueNumber: 500), Ct));
        var detail = await Data<AdminDeviceDetail>(await Admin.GetAsync(path, Ct));
        var createdAt = detail.CreatedAt;
        Assert.Equal(500m, Assert.Single(detail.Specifications).ValueNumber);
        Assert.Equal(input.History, detail.Content.History);
        Assert.Equal(input.Aliases, detail.Content.Aliases);
        state = await Data<AdminDeviceState>(await Admin.PostAsync(path + "/publish", null, Ct));
        Assert.Equal("published", state.Status);
        Assert.NotNull(state.PublishedAt);
        await Visibility(input.Slug, brand.Slug, true);
        var publicDetail = await Data<GetDeviceResponse>(await Public.GetAsync($"/api/v1/devices/{input.Slug}", Ct));
        Assert.Equal("Recorded history", publicDetail.History);
        Assert.Equal(99m, publicDetail.PhysicalDetails.WeightGrams);
        Assert.Equal(input.SeoTitle, publicDetail.SeoTitle);
        await Search("Oldalias", input.Slug, true);
        await Search("Orbitmodel", input.Slug, true);
        var comparison = await Data<CompareDevicesResponse>(await Public.GetAsync($"/api/v1/compare?devices={input.Slug},nokia-3310", Ct));
        Assert.Equal(500m, comparison.SpecificationGroups.SelectMany(x => x.Specifications)
            .Single(x => x.Key == "talk_time_max").Values[0].ValueNumber);
        // A failed edit must not persist the identity changes made earlier within the request scope.
        await Error(await Admin.PutAsJsonAsync(path, input with { Name = "Must not persist", SeoTitle = "" }, Ct), HttpStatusCode.BadRequest);
        Assert.Equal(input.Name, (await Data<AdminDeviceDetail>(await Admin.GetAsync(path, Ct))).Content.Name);
        input = input with { Aliases = ["Newalias"], Description = "Freshdescription", History = "Revised history" };
        await Data<AdminDeviceState>(await Admin.PutAsJsonAsync(path, input, Ct));
        await Search("Oldalias", input.Slug, false);
        await Search("Newalias", input.Slug, true);
        await Search("Freshdescription", input.Slug, true);
        await Data<AdminBrandResponse>(await Admin.PutAsJsonAsync($"/api/v1/admin/brands/{brand.Id}",
            new BrandInput("Renamedmanufacturer", brand.Slug), Ct));
        await Search("Renamedmanufacturer", input.Slug, true);
        await Data<AdminDeviceState>(await Admin.DeleteAsync(specPath, Ct));
        Assert.Empty((await Data<AdminDeviceDetail>(await Admin.GetAsync(path, Ct))).Specifications);
        await Error(await Admin.DeleteAsync(specPath, Ct), HttpStatusCode.NotFound, "ADMIN_RESOURCE_NOT_FOUND");
        await Data<AdminDeviceState>(await Admin.PutAsJsonAsync(specPath, new SpecificationInput(ValueNumber: 800), Ct));
        state = await Data<AdminDeviceState>(await Admin.PostAsync(path + "/unpublish", null, Ct));
        Assert.Equal("draft", state.Status);
        Assert.Null(state.PublishedAt);
        await Visibility(input.Slug, brand.Slug, false);
        await Data<AdminDeviceState>(await Admin.PostAsync(path + "/publish", null, Ct));
        state = await Data<AdminDeviceState>(await Admin.DeleteAsync(path, Ct));
        Assert.Equal("archived", state.Status);
        await Visibility(input.Slug, brand.Slug, false);
        Assert.Equal("archived", (await Data<AdminDeviceState>(await Admin.PostAsync(path + "/archive", null, Ct))).Status);
        await Error(await Admin.PostAsync(path + "/publish", null, Ct), HttpStatusCode.Conflict, "CATALOG_CONFLICT");
        await Error(await Admin.PostAsync(path + "/unpublish", null, Ct), HttpStatusCode.Conflict, "CATALOG_CONFLICT");
        await Error(await Admin.PutAsJsonAsync(path, input, Ct), HttpStatusCode.Conflict, "CATALOG_CONFLICT");
        await Error(await Admin.PutAsJsonAsync(specPath, new SpecificationInput(ValueNumber: 1), Ct), HttpStatusCode.Conflict, "CATALOG_CONFLICT");
        await Error(await Admin.DeleteAsync(specPath, Ct), HttpStatusCode.Conflict, "CATALOG_CONFLICT");
        await Error(await Admin.DeleteAsync($"/api/v1/admin/brands/{brand.Id}", Ct), HttpStatusCode.Conflict, "REFERENCE_CONFLICT");
        await using var persisted = fixture.Catalog.CreateContext();
        var device = await persisted.Devices.Include(x => x.Specifications).SingleAsync(x => x.Id == state.Id, Ct);
        Assert.Equal(DeviceStatus.Archived, device.Status);
        Assert.Equal(createdAt, device.CreatedAt);
        Assert.Equal("Revised history", device.History);
        Assert.Equal(800m, Assert.Single(device.Specifications).ValueNumber);
    }

    [Fact]
    public async Task Bad_references_duplicate_slugs_and_invalid_content_are_rejected_without_partial_writes()
    {
        var input = await Input("validation-phone");
        foreach (var invalid in new[]
        {
            input with { BrandId = Guid.NewGuid() }, input with { CategoryId = Guid.NewGuid() },
            input with { ComparisonGroupId = Guid.NewGuid() }, input with { Name = " " }, input with { Name = null! },
            input with { Slug = "Bad Slug" }, input with { ShortDescription = null! }, input with { Aliases = null! },
            input with { Aliases = [" "] }, input with { Aliases = Enumerable.Repeat("alias", 21).ToArray() },
            input with { ReleaseYear = 2000, ReleaseDate = new DateOnly(2001, 1, 1) },
            input with { ReleaseYear = 2000, DiscontinuedDate = new DateOnly(1999, 1, 1) }, input with { WeightGrams = 0 },
            input with { History = "NUL\0text" }, input with { Aliases = ["NUL\0alias"] }
        }) await Error(await Admin.PostAsJsonAsync("/api/v1/admin/devices", invalid, Ct), HttpStatusCode.BadRequest);
        await using (var db = fixture.Catalog.CreateContext()) Assert.False(await db.Devices.AnyAsync(x => x.Slug == input.Slug, Ct));
        var created = await Data<AdminDeviceState>(await Admin.PostAsJsonAsync("/api/v1/admin/devices", input, Ct), HttpStatusCode.Created);
        await Error(await Admin.PostAsJsonAsync("/api/v1/admin/devices", input, Ct), HttpStatusCode.Conflict, "DUPLICATE_IDENTIFIER");
        await Error(await Admin.PutAsJsonAsync($"/api/v1/admin/devices/{created.Id}", input with { Slug = "nokia-3310" }, Ct),
            HttpStatusCode.Conflict, "DUPLICATE_IDENTIFIER");
        await Error(await Admin.PutAsJsonAsync($"/api/v1/admin/devices/{Guid.NewGuid()}", input, Ct), HttpStatusCode.NotFound, "ADMIN_RESOURCE_NOT_FOUND");
        await Error(await Admin.PostAsync($"/api/v1/admin/devices/{Guid.NewGuid()}/publish", null, Ct), HttpStatusCode.NotFound, "ADMIN_RESOURCE_NOT_FOUND");
        await Error(await Admin.PutAsJsonAsync($"/api/v1/admin/devices/{created.Id}/specifications/{Guid.NewGuid()}",
            new SpecificationInput(ValueText: "Test"), Ct), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("devices")]
    [InlineData("brands")]
    [InlineData("categories")]
    [InlineData("specification-groups")]
    [InlineData("specification-definitions")]
    [InlineData("comparison-groups")]
    public async Task Lists_are_bounded_and_bad_requests_have_consistent_errors(string resource)
    {
        foreach (var query in new[] { "page=0", "page=10001", "pageSize=0", "pageSize=101", "page=abc" })
            await Error(await Admin.GetAsync($"/api/v1/admin/{resource}?{query}", Ct), HttpStatusCode.BadRequest);
        using var response = await Admin.GetAsync($"/api/v1/admin/{resource}?pageSize=1", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Single(json.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(1, json.RootElement.GetProperty("pagination").GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Admin_queries_are_no_tracking_and_can_filter_non_public_states()
    {
        var input = await Input("filtered-draft");
        var created = await Data<AdminDeviceState>(await Admin.PostAsJsonAsync("/api/v1/admin/devices", input, Ct), HttpStatusCode.Created);
        var drafts = await Admin.GetFromJsonAsync<PaginatedResponse<AdminDeviceSummary>>("/api/v1/admin/devices?status=draft", Ct);
        Assert.Contains(drafts!.Data, x => x.Id == created.Id);
        Assert.All(drafts.Data, x => Assert.Equal("draft", x.Status));
        await Error(await Admin.GetAsync("/api/v1/admin/devices?status=99", Ct), HttpStatusCode.BadRequest);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetAdminDevicesHandler>();
        await handler.ListAsync(new(), Ct);
        await handler.GetAsync(created.Id, Ct);
        await scope.ServiceProvider.GetRequiredService<ManageBrandsHandler>().ListAsync(new(), Ct);
        await scope.ServiceProvider.GetRequiredService<ManageCategoriesHandler>().ListAsync(new(), Ct);
        await scope.ServiceProvider.GetRequiredService<ManageSpecificationGroupsHandler>().ListAsync(new(), Ct);
        await scope.ServiceProvider.GetRequiredService<ManageSpecificationDefinitionsHandler>().ListAsync(new(), Ct);
        await scope.ServiceProvider.GetRequiredService<GetAdminComparisonGroupsHandler>().HandleAsync(new(), Ct);
        var db = scope.ServiceProvider.GetRequiredService<TechVaultDbContext>();
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private async Task<DeviceInput> Input(string slug)
    {
        await using var db = fixture.Catalog.CreateContext();
        return new("Editorial phone", slug, await db.Brands.Where(x => x.Slug == "nokia").Select(x => x.Id).SingleAsync(Ct),
            await db.Categories.Where(x => x.Slug == "feature-phones").Select(x => x.Id).SingleAsync(Ct),
            await db.ComparisonGroups.Where(x => x.Key == "phone").Select(x => x.Id).SingleAsync(Ct));
    }

    private async Task Search(string query, string slug, bool visible)
    {
        var result = await Public.GetFromJsonAsync<PaginatedResponse<DeviceCardResponse>>($"/api/v1/search?q={query}", Ct);
        Assert.Equal(visible, result!.Data.Any(x => x.Slug == slug));
    }

    private async Task Visibility(string slug, string brand, bool visible)
    {
        foreach (var path in new[] { "/api/v1/devices", "/api/v1/phones", "/api/v1/timeline", "/api/v1/search?q=Editorial" })
        {
            var result = await Public.GetFromJsonAsync<PaginatedResponse<DeviceCardResponse>>(path, Ct);
            Assert.Equal(visible, result!.Data.Any(x => x.Slug == slug));
        }
        Assert.DoesNotContain((await Public.GetFromJsonAsync<PaginatedResponse<DeviceCardResponse>>("/api/v1/computers", Ct))!.Data, x => x.Slug == slug);
        foreach (var path in new[] { $"/api/v1/devices/{slug}", $"/api/v1/devices/{slug}/specifications", $"/api/v1/compare?devices={slug},nokia-3310" })
        {
            using var response = await Public.GetAsync(path, Ct);
            Assert.Equal(visible ? HttpStatusCode.OK : HttpStatusCode.NotFound, response.StatusCode);
        }
        var detail = await Data<BrandResponse>(await Public.GetAsync($"/api/v1/brands/{brand}", Ct));
        Assert.Equal(visible ? 1 : 0, detail.PublishedDeviceCount);
        var brands = await Public.GetFromJsonAsync<PaginatedResponse<BrandResponse>>("/api/v1/brands", Ct);
        Assert.Equal(visible ? 1 : 0, brands!.Data.Single(x => x.Slug == brand).PublishedDeviceCount);
        using var categories = await Public.GetAsync("/api/v1/categories", Ct);
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
    }
}
