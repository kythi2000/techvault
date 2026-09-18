using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechVault.Api.Responses;
using TechVault.Application.Brands;
using TechVault.Application.Categories.GetCategories;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Application.Devices.GetDevice;
using TechVault.Application.Devices.GetDeviceSpecifications;
using TechVault.Application.Devices.Specifications;
using TechVault.Domain.Devices;

namespace TechVault.IntegrationTests;

public sealed class MixedCatalogApiTests(MixedCatalogFixture fixture) : IClassFixture<MixedCatalogFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("nokia-3310", "Nokia 3310", "nokia", "feature-phones", 2000, 9)]
    [InlineData("nokia-3210", "Nokia 3210", "nokia", "feature-phones", 1999, 10)]
    [InlineData("macintosh-128k", "Macintosh 128K", "apple", "all-in-one-computers", 1984, 10)]
    [InlineData("imac-g3", "iMac G3", "apple", "all-in-one-computers", 1998, 15)]
    public async Task All_four_devices_use_the_same_complete_detail_and_specification_contract(
        string slug, string name, string brand, string category, int year, int specCount)
    {
        var device = (await GetAsync<ApiResponse<GetDeviceResponse>>($"/devices/{slug}")).Data;
        Assert.Equal(name, device.Name);
        Assert.Equal(slug, device.Slug);
        Assert.Equal(brand, device.Brand.Slug);
        Assert.Equal(category, device.Category.Slug);
        Assert.Equal(year, device.ReleaseYear);
        Assert.NotNull(device.PublishedAt);
        Assert.All(new[] { device.ShortDescription, device.Description, device.History, device.SeoTitle, device.SeoDescription },
            value => Assert.False(string.IsNullOrWhiteSpace(value)));
        Assert.Contains(name, device.SeoTitle);
        Assert.Equal(specCount, Specs(device).Length);
        Assert.Equal(device.SpecificationGroups.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Key).Select(x => x.Key),
            device.SpecificationGroups.Select(x => x.Key));
        foreach (var group in device.SpecificationGroups)
        {
            Assert.Equal(group.Specifications.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Key).Select(x => x.Key),
                group.Specifications.Select(x => x.Key));
            foreach (var spec in group.Specifications)
            {
                Assert.Equal(1, new object?[] { spec.ValueText, spec.ValueNumber, spec.ValueBoolean, spec.ValueDate }
                    .Count(value => value is not null));
                Assert.True(spec.DataType switch
                {
                    "text" => spec.ValueText is not null,
                    "number" => spec.ValueNumber is not null,
                    "boolean" => spec.ValueBoolean is not null,
                    "date" => spec.ValueDate is not null,
                    _ => false
                });
            }
        }
        var dedicated = (await GetAsync<ApiResponse<GetDeviceSpecificationsResponse>>($"/devices/{slug}/specifications")).Data;
        Assert.Equal(device.Id, dedicated.DeviceId);
        Assert.Equal(device.Name, dedicated.Name);
        Assert.Equal(JsonSerializer.Serialize(device.SpecificationGroups), JsonSerializer.Serialize(dedicated.SpecificationGroups));
    }

    [Theory]
    [InlineData("/devices?sort=release-asc", "macintosh-128k,imac-g3,nokia-3210,nokia-3310")]
    [InlineData("/phones?sort=release-asc", "nokia-3210,nokia-3310")]
    [InlineData("/computers?sort=release-asc", "macintosh-128k,imac-g3")]
    [InlineData("/devices?type=phones&category=phones&brand=nokia", "nokia-3310,nokia-3210")]
    [InlineData("/devices?type=computers&category=computers&brand=apple", "imac-g3,macintosh-128k")]
    [InlineData("/devices?category=all-in-one-computers", "imac-g3,macintosh-128k")]
    [InlineData("/computers?category=all-in-one-computers&brand=apple&decade=1980", "macintosh-128k")]
    [InlineData("/computers?fromYear=1990&toYear=1999", "imac-g3")]
    [InlineData("/phones?decade=1990", "nokia-3210")]
    [InlineData("/devices?decade=1990", "nokia-3210,imac-g3")]
    [InlineData("/devices?year=1984", "macintosh-128k")]
    [InlineData("/devices?brand=apple", "imac-g3,macintosh-128k")]
    [InlineData("/computers?brand=nokia", "")]
    [InlineData("/phones?brand=apple", "")]
    [InlineData("/computers?category=feature-phones", "")]
    [InlineData("/phones?category=all-in-one-computers", "")]
    public async Task Existing_browse_filters_work_for_both_real_categories(string path, string expected)
    {
        var response = await GetAsync<PaginatedResponse<DeviceCardResponse>>(path);
        var slugs = expected.Length == 0 ? [] : expected.Split(',');
        Assert.Equal(slugs, response.Data.Select(x => x.Slug));
        Assert.Equal(slugs.Length, response.Pagination.Total);
    }

    [Theory]
    [InlineData("release-asc", "macintosh-128k,imac-g3,nokia-3210,nokia-3310")]
    [InlineData("release-desc", "nokia-3310,nokia-3210,imac-g3,macintosh-128k")]
    public async Task Mixed_catalog_pagination_has_no_duplicates_or_category_specific_ordering(string sort, string expected)
    {
        var first = await GetAsync<PaginatedResponse<DeviceCardResponse>>($"/devices?sort={sort}&pageSize=2&page=1");
        var second = await GetAsync<PaginatedResponse<DeviceCardResponse>>($"/devices?sort={sort}&pageSize=2&page=2");
        Assert.Equal(4, first.Pagination.Total);
        Assert.Equal(2, second.Pagination.TotalPages);
        Assert.Equal(expected.Split(','), first.Data.Concat(second.Data).Select(x => x.Slug));
        Assert.Equal(4, first.Data.Concat(second.Data).Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public async Task Brands_categories_and_cards_expose_the_shared_navigation_without_new_models()
    {
        var brands = await GetAsync<PaginatedResponse<BrandResponse>>("/brands");
        Assert.Equal(new[] { "apple", "nokia" }, brands.Data.Select(x => x.Slug));
        Assert.All(brands.Data, x => Assert.Equal(2, x.PublishedDeviceCount));
        Assert.Equal(2, (await GetAsync<ApiResponse<BrandResponse>>("/brands/apple")).Data.PublishedDeviceCount);
        var categories = (await GetAsync<PaginatedResponse<CategoryResponse>>("/categories")).Data;
        Assert.Equal(4, categories.Count);
        var root = categories.Single(x => x.Slug == "computers");
        var allInOne = categories.Single(x => x.Slug == "all-in-one-computers");
        Assert.Null(root.ParentCategoryId);
        Assert.Equal(root.Id, allInOne.ParentCategoryId);
        Assert.Equal("computers", allInOne.ParentSlug);
        var cards = (await GetAsync<PaginatedResponse<DeviceCardResponse>>("/computers")).Data;
        Assert.All(cards, x =>
        {
            Assert.Equal(allInOne.Id, x.Category.Id);
            Assert.Equal("computers", x.Category.ParentSlug);
            Assert.Equal("apple", x.Brand.Slug);
            Assert.NotEmpty(x.ShortDescription);
        });
        await using var db = fixture.CreateContext();
        Assert.Equal(7, db.Model.GetEntityTypes().Count());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Collection(await db.Database.GetAppliedMigrationsAsync(Ct),
            migration => Assert.EndsWith("_InitialCatalog", migration),
            migration => Assert.EndsWith("_AddCatalogDiscovery", migration),
            migration => Assert.EndsWith("_AddCatalogComparisons", migration));
        Assert.Equal(4, await db.Devices.CountAsync(x => x.Status == DeviceStatus.Published, Ct));
        Assert.Equal(10, await db.SpecificationGroups.CountAsync(Ct));
        Assert.Equal(29, await db.SpecificationDefinitions.CountAsync(Ct));
        Assert.Equal(44, await db.DeviceSpecifications.CountAsync(Ct));
    }

    [Fact]
    public async Task Shared_definitions_keep_units_types_and_zero_false_values_without_phone_spec_leakage()
    {
        var mac = (await GetAsync<ApiResponse<GetDeviceResponse>>("/devices/macintosh-128k")).Data;
        var imac = (await GetAsync<ApiResponse<GetDeviceResponse>>("/devices/imac-g3")).Data;
        var phone = (await GetAsync<ApiResponse<GetDeviceResponse>>("/devices/nokia-3210")).Data;
        var macSpecs = Specs(mac).ToDictionary(x => x.Key);
        var imacSpecs = Specs(imac).ToDictionary(x => x.Key);
        var phoneSpecs = Specs(phone).ToDictionary(x => x.Key);
        foreach (var key in macSpecs.Keys.Intersect(imacSpecs.Keys))
        {
            Assert.Equal(macSpecs[key].Id, imacSpecs[key].Id);
            Assert.Equal(macSpecs[key].DataType, imacSpecs[key].DataType);
            Assert.Equal(macSpecs[key].Unit, imacSpecs[key].Unit);
        }
        Assert.Equal("MiB", macSpecs["ram_capacity"].Unit);
        Assert.Equal(0.125m, macSpecs["ram_capacity"].ValueNumber);
        Assert.Equal(32m, imacSpecs["ram_capacity"].ValueNumber);
        Assert.Equal(0m, macSpecs["ram_slots"].ValueNumber);
        Assert.Equal(8m, macSpecs["cpu_clock"].ValueNumber);
        Assert.Equal(233m, imacSpecs["cpu_clock"].ValueNumber);
        Assert.Equal(4m, imacSpecs["storage_capacity"].ValueNumber);
        Assert.Equal(2m, imacSpecs["usb_ports"].ValueNumber);
        Assert.False(macSpecs["internal_hard_disk"].ValueBoolean);
        Assert.True(imacSpecs["internal_hard_disk"].ValueBoolean);
        Assert.False(imacSpecs["floppy_drive"].ValueBoolean);
        Assert.False(macSpecs.ContainsKey("battery_chemistry"));
        Assert.False(imacSpecs.ContainsKey("sms_chat"));
        Assert.False(phoneSpecs.ContainsKey("cpu_model"));
        Assert.Equal(160m, phoneSpecs["sms_character_limit"].ValueNumber);
        Assert.Equal(new DateOnly(1999, 3, 18), phoneSpecs["announcement_date"].ValueDate);
        Assert.Null(phone.ReleaseDate);
        Assert.Null(phone.PhysicalDetails.WeightGrams);
        Assert.Equal(new DateOnly(1984, 1, 24), mac.ReleaseDate);
        Assert.Equal(new DateOnly(1998, 8, 15), imac.ReleaseDate);
        Assert.Equal(new DateOnly(1998, 5, 6), imacSpecs["announcement_date"].ValueDate);
        Assert.Null(imac.DiscontinuedDate);
        Assert.Equal(345m, mac.PhysicalDetails.HeightMm);
        Assert.Equal(18100m, imac.PhysicalDetails.WeightGrams);
    }

    private static SpecificationResponse[] Specs(GetDeviceResponse device) =>
        device.SpecificationGroups.SelectMany(x => x.Specifications).ToArray();

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await fixture.Client.GetAsync("/api/v1" + path, Ct);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(response.Headers.GetValues("X-Trace-Id"))));
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
