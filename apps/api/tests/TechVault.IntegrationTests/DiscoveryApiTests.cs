using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TechVault.Api.Responses;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Application.Search;
using TechVault.Application.Timeline;
using TechVault.Infrastructure.Persistence;

namespace TechVault.IntegrationTests;

public sealed class DiscoveryApiTests(MixedCatalogFixture fixture) : IClassFixture<MixedCatalogFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Discovery_queries_leave_the_scoped_change_tracker_empty()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TechVaultDbContext>();
        var search = scope.ServiceProvider.GetRequiredService<SearchDevicesHandler>();
        var timeline = scope.ServiceProvider.GetRequiredService<GetTimelineHandler>();
        Assert.True((await search.HandleAsync(new("Apple"), Ct)).IsSuccess);
        Assert.True((await timeline.HandleAsync(new(Category: "computers"), Ct)).IsSuccess);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("NOKIA 3310", "nokia-3310")]
    [InlineData("Macintosh 128K", "macintosh-128k")]
    [InlineData("Apple iMac", "imac-g3")]
    [InlineData("  3210  ", "nokia-3210")]
    public async Task Search_finds_real_content_including_terms_across_name_and_brand(string query, string slug)
    {
        var result = await GetAsync("/search?q=" + Uri.EscapeDataString(query));
        Assert.Equal(slug, Assert.Single(result.Data).Slug);
        Assert.Equal(1, result.Pagination.Total);
        Assert.Equal(24, result.Pagination.PageSize);
    }

    [Theory]
    [InlineData("/timeline", "macintosh-128k,imac-g3,nokia-3210,nokia-3310")]
    [InlineData("/timeline?type=phones", "nokia-3210,nokia-3310")]
    [InlineData("/timeline?type=computers", "macintosh-128k,imac-g3")]
    [InlineData("/timeline?brand=apple", "macintosh-128k,imac-g3")]
    [InlineData("/timeline?brand=nokia", "nokia-3210,nokia-3310")]
    [InlineData("/timeline?category=phones", "nokia-3210,nokia-3310")]
    [InlineData("/timeline?category=computers", "macintosh-128k,imac-g3")]
    [InlineData("/timeline?category=all-in-one-computers", "macintosh-128k,imac-g3")]
    [InlineData("/timeline?era=1990s", "imac-g3,nokia-3210")]
    [InlineData("/timeline?year=2000", "nokia-3310")]
    [InlineData("/timeline?fromYear=1998&toYear=1999", "imac-g3,nokia-3210")]
    [InlineData("/timeline?type=phones&category=feature-phones&brand=nokia&era=1990s&fromYear=1999&toYear=1999&year=1999", "nokia-3210")]
    [InlineData("/timeline?type=computers&brand=nokia", "")]
    [InlineData("/timeline?category=missing", "")]
    [InlineData("/timeline?era=1990s&year=2000", "")]
    [InlineData("/timeline?page=10000", "")]
    [InlineData("/search?q=nonexistentterm", "")]
    public async Task Discovery_results_use_one_published_card_contract(string path, string expected)
    {
        var result = await GetAsync(path);
        Assert.Equal(expected.Length == 0 ? [] : expected.Split(','), result.Data.Select(x => x.Slug));
        Assert.All(result.Data, x =>
        {
            Assert.NotEmpty(x.ShortDescription);
            Assert.NotEmpty(x.Brand.Slug);
            Assert.NotEmpty(x.Category.Slug);
            Assert.NotNull(x.ReleaseYear);
        });
    }

    [Fact]
    public async Task Timeline_pagination_is_stable_and_preserves_unknown_exact_dates()
    {
        var first = await GetAsync("/timeline?pageSize=2");
        var second = await GetAsync("/timeline?pageSize=2&page=2");
        Assert.Equal(4, first.Pagination.Total);
        Assert.Equal(2, second.Pagination.TotalPages);
        Assert.Equal(new[] { "macintosh-128k", "imac-g3", "nokia-3210", "nokia-3310" },
            first.Data.Concat(second.Data).Select(x => x.Slug));
        Assert.All(first.Data, x => Assert.NotNull(x.ReleaseDate));
        Assert.All(second.Data, x => Assert.Null(x.ReleaseDate));
        Assert.Equal(second.Data.Select(x => x.Id), (await GetAsync("/timeline?pageSize=2&page=2")).Data.Select(x => x.Id));
    }

    [Theory]
    [InlineData("/search")]
    [InlineData("/search?q=")]
    [InlineData("/search?q=%20%20")]
    [InlineData("/search?q=%26%7C%21%3A%2A")]
    [InlineData("/search?q=nokia&page=0")]
    [InlineData("/search?q=nokia&page=10001")]
    [InlineData("/search?q=nokia&pageSize=0")]
    [InlineData("/search?q=nokia&pageSize=101")]
    [InlineData("/search?q=nokia&page=abc")]
    [InlineData("/timeline?page=0")]
    [InlineData("/timeline?page=10001")]
    [InlineData("/timeline?pageSize=101")]
    [InlineData("/timeline?pageSize=-1")]
    [InlineData("/timeline?year=unknown")]
    [InlineData("/timeline?year=0")]
    [InlineData("/timeline?fromYear=2000&toYear=1990")]
    [InlineData("/timeline?toYear=10000")]
    [InlineData("/timeline?era=1991s")]
    [InlineData("/timeline?era=1990")]
    [InlineData("/timeline?era=")]
    [InlineData("/timeline?category=Bad%20Slug")]
    [InlineData("/timeline?brand=APPLE")]
    [InlineData("/timeline?type=tablets")]
    public async Task Invalid_discovery_requests_use_the_existing_error_envelope(string path)
    {
        using var response = await fixture.Client.GetAsync("/api/v1" + path, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(Ct);
        Assert.Equal("VALIDATION_ERROR", result!.Error.Code);
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Trace-Id")), result.Error.TraceId);
    }

    [Fact]
    public async Task Overlong_search_is_rejected_and_plain_text_cannot_inject_sql_or_tsquery()
    {
        using var tooLong = await fixture.Client.GetAsync("/api/v1/search?q=" + new string('x', 201), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Empty((await GetAsync("/search?q=" + Uri.EscapeDataString("' ; DROP TABLE Devices; --"))).Data);
        Assert.Empty((await GetAsync("/search?q=" + Uri.EscapeDataString("nokia | apple"))).Data);
        Assert.Equal(4, (await GetAsync("/timeline")).Pagination.Total);
    }

    private async Task<PaginatedResponse<DeviceCardResponse>> GetAsync(string path)
    {
        using var response = await fixture.Client.GetAsync("/api/v1" + path, Ct);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(response.Headers.GetValues("X-Trace-Id"))));
        return (await response.Content.ReadFromJsonAsync<PaginatedResponse<DeviceCardResponse>>(Ct))!;
    }
}
