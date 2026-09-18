using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechVault.Api.Responses;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Devices;

namespace TechVault.IntegrationTests;

// Mutating cases each own their container; shared real-seed HTTP fixtures remain unchanged.
public sealed class DiscoveryChangesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_search_field_is_indexed_and_weighted_with_stable_bounded_pagination()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeEmptyAsync();
        await using (var db = fixture.CreateContext())
        {
            var brand = new Brand("Brandprobe", "brandprobe");
            var category = new Category("Phones", "phones", 0);
            var first = Device("Rankingprobe Nameprobe", "a-name", brand, category);
            first.SetSearchMetadata("MODEL9384", ["Aliasprobe"]);
            first.UpdateContent("Summaryprobe", "Descriptionprobe", "History", "Title", "SEO");
            var tied = Device("Rankingprobe", "b-name", brand, category);
            var brandMatch = Device("Third", "c-brand", new Brand("Rankingprobe", "rankingprobe"), category);
            var descriptionMatch = Device("Fourth", "d-description", brand, category);
            descriptionMatch.UpdateContent("Summary", "Rankingprobe", "History", "Title", "SEO");
            db.Devices.AddRange(first, tied, brandMatch, descriptionMatch);
            await db.SaveChangesAsync(Ct);
        }
        foreach (var text in new[] { "nameprobe", "ALIASPROBE", "model9384", "summaryprobe", "descriptionprobe", "brandprobe aliasprobe" })
            Assert.Equal("a-name", Assert.Single((await GetAsync(fixture, "/search?q=" + Uri.EscapeDataString(text))).Data).Slug);
        Assert.Equal(3, (await GetAsync(fixture, "/search?q=brandprobe")).Pagination.Total);
        var firstPage = await GetAsync(fixture, "/search?q=rankingprobe&pageSize=2");
        var secondPage = await GetAsync(fixture, "/search?q=rankingprobe&pageSize=2&page=2");
        Assert.Equal(4, firstPage.Pagination.Total);
        Assert.Equal(2, secondPage.Pagination.TotalPages);
        Assert.Equal(new[] { "a-name", "b-name", "c-brand", "d-description" }, firstPage.Data.Concat(secondPage.Data).Select(x => x.Slug));
        Assert.Equal(firstPage.Data.Select(x => x.Id), (await GetAsync(fixture, "/search?q=rankingprobe&pageSize=2")).Data.Select(x => x.Id));
        Assert.Empty((await GetAsync(fixture, "/search?q=rankingprobe&page=3&pageSize=2")).Data);
        await using var read = fixture.CreateContext();
        var saved = await read.Devices.SingleAsync(x => x.Slug == "a-name", Ct);
        Assert.Equal("Aliasprobe", Assert.Single(saved.Aliases));
        Assert.Equal("MODEL9384", saved.ModelNumber);
    }

    [Fact]
    public async Task Search_immediately_reflects_device_brand_metadata_and_visibility_changes()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeEmptyAsync();
        await using (var db = fixture.CreateContext())
        {
            var brand = new Brand("Oldbrand", "brand");
            var category = new Category("Phones", "phones", 0);
            var first = Device("Oldname", "first", brand, category);
            first.SetSearchMetadata("Oldmodel", ["Oldalias"]);
            first.UpdateContent("Oldsummary", "Olddescription", "History", "Title", "SEO");
            db.Devices.AddRange(first, Device("Second", "second", brand, category));
            await db.SaveChangesAsync(Ct);
        }
        foreach (var term in new[] { "oldname", "oldalias", "oldmodel", "oldsummary", "olddescription" })
            Assert.Single((await GetAsync(fixture, "/search?q=" + term)).Data);

        await using (var db = fixture.CreateContext())
        {
            var device = await db.Devices.SingleAsync(x => x.Slug == "first", Ct);
            device.SetSearchMetadata("Newmodel", ["Newalias"]);
            device.UpdateContent("Newsummary", "Newdescription", "History", "Title", "SEO");
            db.Entry(device).Property(x => x.Name).CurrentValue = "Newname";
            await db.SaveChangesAsync(Ct);
            // Direct SQL/ExecuteUpdate changes must be indexed too, not only tracked SaveChanges.
            await db.Brands.Where(x => x.Slug == "brand").ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Name, "Newbrand"), Ct);
        }
        foreach (var term in new[] { "oldname", "oldalias", "oldmodel", "oldsummary", "olddescription", "oldbrand" })
            Assert.Empty((await GetAsync(fixture, "/search?q=" + term)).Data);
        foreach (var term in new[] { "newname", "newalias", "newmodel", "newsummary", "newdescription", "newbrand newalias" })
            Assert.Equal("first", Assert.Single((await GetAsync(fixture, "/search?q=" + Uri.EscapeDataString(term))).Data).Slug);
        Assert.Equal(2, (await GetAsync(fixture, "/search?q=newbrand")).Pagination.Total);

        await using (var db = fixture.CreateContext())
        {
            var other = new Brand("Reassignedbrand", "reassigned");
            db.Brands.Add(other);
            await db.SaveChangesAsync(Ct);
            await db.Devices.Where(x => x.Slug == "first").ExecuteUpdateAsync(x => x.SetProperty(d => d.BrandId, other.Id), Ct);
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            await db.Brands.Where(x => x.Slug == "reassigned").ExecuteUpdateAsync(x => x.SetProperty(b => b.Name, "Rolledbackbrand"), Ct);
            await transaction.RollbackAsync(Ct);
        }
        Assert.Equal("first", Assert.Single((await GetAsync(fixture, "/search?q=reassignedbrand")).Data).Slug);
        Assert.Equal("second", Assert.Single((await GetAsync(fixture, "/search?q=newbrand")).Data).Slug);
        Assert.Empty((await GetAsync(fixture, "/search?q=rolledbackbrand")).Data);

        await using (var db = fixture.CreateContext())
        {
            await db.Devices.Where(x => x.Slug == "first").ExecuteUpdateAsync(x => x
                .SetProperty(d => d.Status, DeviceStatus.Draft).SetProperty(d => d.PublishedAt, (DateTimeOffset?)null), Ct);
            await db.Devices.Where(x => x.Slug == "second").ExecuteUpdateAsync(x => x
                .SetProperty(d => d.Status, DeviceStatus.Archived).SetProperty(d => d.PublishedAt, (DateTimeOffset?)null), Ct);
        }
        foreach (var path in new[] { "/search?q=newname", "/search?q=reassignedbrand", "/search?q=newbrand", "/timeline" })
            Assert.Empty((await GetAsync(fixture, path)).Data);
        await using (var db = fixture.CreateContext())
        {
            var device = await db.Devices.SingleAsync(x => x.Slug == "first", Ct);
            device.Publish();
            device.SetSearchMetadata(null, []);
            await db.SaveChangesAsync(Ct);
        }
        Assert.Single((await GetAsync(fixture, "/search?q=newname")).Data);
        Assert.Single((await GetAsync(fixture, "/timeline")).Data);
        Assert.Empty((await GetAsync(fixture, "/search?q=newalias")).Data);
        Assert.Empty((await GetAsync(fixture, "/search?q=newmodel")).Data);
    }

    [Fact]
    public async Task Timeline_orders_exact_dates_then_year_only_records_and_excludes_unknown_years()
    {
        await using var fixture = new MixedCatalogFixture();
        await fixture.InitializeEmptyAsync();
        await using (var db = fixture.CreateContext())
        {
            var brand = new Brand("Example", "example");
            var category = new Category("Computers", "computers", 0);
            var early = Device("Early", "z-early", brand, category);
            early.SetRelease(2000, new DateOnly(2000, 1, 1));
            var later = Device("Later", "a-later", brand, category);
            later.SetRelease(2000, new DateOnly(2000, 12, 1));
            var unknown = Device("Unknown", "unknown", brand, category);
            unknown.SetRelease(null);
            var draft = new Device("Hidden", "hidden", brand, category);
            draft.SetRelease(2000);
            db.Devices.AddRange(early, later, Device("Year only B", "y-year-only-b", brand, category),
                Device("Year only A", "x-year-only-a", brand, category), unknown, draft);
            await db.SaveChangesAsync(Ct);
        }
        var result = await GetAsync(fixture, "/timeline?type=computers");
        Assert.Equal(4, result.Pagination.Total);
        Assert.Equal(new[] { "z-early", "a-later", "x-year-only-a", "y-year-only-b" }, result.Data.Select(x => x.Slug));
        Assert.Equal(5, (await GetAsync(fixture, "/devices?type=computers")).Pagination.Total);
        Assert.Single((await GetAsync(fixture, "/search?q=unknown")).Data);
        Assert.Empty((await GetAsync(fixture, "/search?q=hidden")).Data);
    }

    private static Device Device(string name, string slug, Brand brand, Category category)
    {
        var device = new Device(name, slug, brand, category);
        device.UpdateContent("Summary", "Description", "History", "Title", "SEO");
        device.SetRelease(2000);
        device.Publish();
        return device;
    }

    private static async Task<PaginatedResponse<DeviceCardResponse>> GetAsync(MixedCatalogFixture fixture, string path)
    {
        using var response = await fixture.Client.GetAsync("/api/v1" + path, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaginatedResponse<DeviceCardResponse>>(Ct))!;
    }
}
