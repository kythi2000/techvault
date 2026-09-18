using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechVault.Api.Responses;
using TechVault.Application.Comparisons;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Comparisons;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;
using TechVault.Infrastructure.Persistence.Seed;

namespace TechVault.IntegrationTests;

public sealed class ComparisonChangesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Typed_values_metadata_identity_and_empty_differences_work_for_non_sample_devices()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeEmptyAsync();
        Guid definitionToHide;
        await using (var db = fixture.CreateContext())
        {
            var compatibility = new ComparisonGroup("Custom compatible models", "custom_models");
            var category = new Category("Examples", "examples", 0);
            var brand = new Brand("Example", "example");
            var first = Create("first", brand, category, compatibility);
            var second = Create("second", brand, category, compatibility);
            var group = new SpecificationGroup("Group B", "group_b", 1);
            var earlierGroup = new SpecificationGroup("Group A", "group_a", 1);
            var date = new SpecificationDefinition("Same label", "date", group, SpecificationDataType.Date, 0, isComparable: true);
            var text = new SpecificationDefinition("Same label", "text", group, SpecificationDataType.Text, 0, isComparable: true);
            var number = new SpecificationDefinition("Number", "number", earlierGroup, SpecificationDataType.Number, 0, isComparable: true);
            var boolean = new SpecificationDefinition("Flag", "boolean", earlierGroup, SpecificationDataType.Boolean, 0, isComparable: true);
            var hidden = new SpecificationDefinition("Hidden", "hidden", group, SpecificationDataType.Text, 0);
            var unassigned = new SpecificationDefinition("Neither device has this", "unassigned", group, SpecificationDataType.Number, 0, isComparable: true);
            definitionToHide = text.Id;
            first.SetSpecification(date, SpecificationValue.Date(new DateOnly(2000, 1, 1)));
            second.SetSpecification(date, SpecificationValue.Date(new DateOnly(2000, 1, 2)));
            first.SetSpecification(text, SpecificationValue.Text("GSM"));
            second.SetSpecification(text, SpecificationValue.Text("gsm"));
            first.SetSpecification(number, SpecificationValue.Number(1.00m));
            second.SetSpecification(number, SpecificationValue.Number(1m));
            first.SetSpecification(boolean, SpecificationValue.Boolean(false));
            first.SetSpecification(hidden, SpecificationValue.Text("Never public in comparisons"));
            db.Devices.AddRange(first, second);
            db.SpecificationDefinitions.Add(unassigned);
            await db.SaveChangesAsync(Ct);
        }
        var result = await GetAsync(fixture, "first,second");
        Assert.Equal("custom_models", result.ComparisonGroup.Key);
        Assert.Equal(new[] { "group_a", "group_b" }, result.SpecificationGroups.Select(x => x.Key));
        var specs = result.SpecificationGroups.SelectMany(x => x.Specifications).ToArray();
        Assert.Equal(new[] { "boolean", "number", "date", "text" }, specs.Select(x => x.Key));
        Assert.True(specs.Single(x => x.Key == "date").IsDifferent);
        Assert.True(specs.Single(x => x.Key == "text").IsDifferent);
        Assert.False(specs.Single(x => x.Key == "number").IsDifferent);
        var missing = specs.Single(x => x.Key == "boolean");
        Assert.False(missing.Values[0].IsMissing);
        Assert.False(missing.Values[0].ValueBoolean);
        Assert.True(missing.Values[1].IsMissing);
        await using (var db = fixture.CreateContext())
        {
            (await db.SpecificationDefinitions.SingleAsync(x => x.Id == definitionToHide, Ct)).SetComparable(false);
            await db.SaveChangesAsync(Ct);
        }
        Assert.DoesNotContain((await GetAsync(fixture, "first,second")).SpecificationGroups.SelectMany(x => x.Specifications), x => x.Key == "text");
        await using (var db = fixture.CreateContext())
            await db.DeviceSpecifications.ExecuteDeleteAsync(Ct);
        Assert.Empty((await GetAsync(fixture, "first,second")).SpecificationGroups);
        Assert.Empty((await GetAsync(fixture, "first,second", true)).SpecificationGroups);
    }

    [Fact]
    public async Task Unassigned_incompatible_and_unpublished_devices_fail_without_seed_repair()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeAsync();
        await using (var db = fixture.CreateContext())
        {
            var first = await db.Devices.SingleAsync(x => x.Slug == "nokia-3310", Ct);
            first.SetComparisonGroup(null);
            (await db.SpecificationDefinitions.SingleAsync(x => x.Key == "antenna", Ct)).SetComparable(false);
            var phone = await db.ComparisonGroups.SingleAsync(x => x.Key == "phone", Ct);
            db.Entry(phone).Property(x => x.Name).CurrentValue = "Editorial phone label";
            await db.SaveChangesAsync(Ct);
            // Compare persisted timestamps on both sides (PostgreSQL stores microseconds, not .NET ticks).
            var updatedAt = await db.Devices.Where(x => x.Id == first.Id).Select(x => x.UpdatedAt).SingleAsync(Ct);
            await CatalogSeed.SeedAsync(db, Ct);
            db.ChangeTracker.Clear();
            Assert.Null((await db.Devices.SingleAsync(x => x.Id == first.Id, Ct)).ComparisonGroupId);
            Assert.Equal(updatedAt, (await db.Devices.SingleAsync(x => x.Id == first.Id, Ct)).UpdatedAt);
            Assert.False((await db.SpecificationDefinitions.SingleAsync(x => x.Key == "antenna", Ct)).IsComparable);
            Assert.Equal("Editorial phone label", (await db.ComparisonGroups.SingleAsync(x => x.Key == "phone", Ct)).Name);
        }
        await ErrorAsync(fixture, "COMPARISON_UNAVAILABLE", HttpStatusCode.BadRequest);
        await using (var db = fixture.CreateContext())
        {
            var first = await db.Devices.SingleAsync(x => x.Slug == "nokia-3310", Ct);
            first.SetComparisonGroup(await db.ComparisonGroups.SingleAsync(x => x.Key == "all_in_one", Ct));
            await db.SaveChangesAsync(Ct);
        }
        await ErrorAsync(fixture, "INCOMPATIBLE_DEVICES", HttpStatusCode.BadRequest);
        foreach (var status in new[] { DeviceStatus.Draft, DeviceStatus.Archived })
        {
            await using (var db = fixture.CreateContext())
                await db.Devices.Where(x => x.Slug == "nokia-3310").ExecuteUpdateAsync(x => x
                    .SetProperty(d => d.Status, status).SetProperty(d => d.PublishedAt, (DateTimeOffset?)null), Ct);
            await ErrorAsync(fixture, "DEVICE_NOT_FOUND", HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Identical_values_produce_an_empty_differences_view_without_mutating_either_device()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeEmptyAsync();
        await using (var db = fixture.CreateContext())
        {
            var group = new ComparisonGroup("Identical", "identical");
            var brand = new Brand("Brand", "brand");
            var category = new Category("Category", "category", 0);
            var definition = new SpecificationDefinition("Shared", "shared", new SpecificationGroup("General", "general", 0),
                SpecificationDataType.Boolean, 0, isComparable: true);
            var first = Create("first", brand, category, group);
            var second = Create("second", brand, category, group);
            first.SetSpecification(definition, SpecificationValue.Boolean(false));
            second.SetSpecification(definition, SpecificationValue.Boolean(false));
            db.Devices.AddRange(first, second);
            await db.SaveChangesAsync(Ct);
        }
        Assert.Single((await GetAsync(fixture, "first,second")).SpecificationGroups);
        var filtered = await GetAsync(fixture, "first,second", true);
        Assert.Equal(2, filtered.Devices.Count);
        Assert.Empty(filtered.SpecificationGroups);
        Assert.Single((await GetAsync(fixture, "first,second")).SpecificationGroups);
    }

    private static Device Create(string slug, Brand brand, Category category, ComparisonGroup group)
    {
        var device = new Device(slug, slug, brand, category);
        device.UpdateContent("Summary", "Description", "History", "Title", "SEO");
        device.SetComparisonGroup(group);
        device.Publish();
        return device;
    }

    private static async Task<CompareDevicesResponse> GetAsync(MixedCatalogFixture fixture, string pair, bool differencesOnly = false) =>
        (await fixture.Client.GetFromJsonAsync<ApiResponse<CompareDevicesResponse>>(
            $"/api/v1/compare?devices={pair}&differencesOnly={differencesOnly}", Ct))!.Data;

    private static async Task ErrorAsync(MixedCatalogFixture fixture, string code, HttpStatusCode status)
    {
        using var response = await fixture.Client.GetAsync("/api/v1/compare?devices=nokia-3310,nokia-3210", Ct);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<ApiErrorResponse>(Ct))!.Error.Code);
    }
}
