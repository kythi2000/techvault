using System.Net;
using System.Text.Json;
using TechVault.Application.Brands.GetBrand;
using TechVault.Application.Brands.GetBrands;
using TechVault.Application.Categories.GetCategories;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Application.Devices.GetDevice;
using TechVault.Application.Devices.GetDeviceSpecifications;

namespace TechVault.IntegrationTests;

public sealed class PublicCatalogApiTests(PublicCatalogFixture fixture) : IClassFixture<PublicCatalogFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/devices")]
    [InlineData("/phones")]
    [InlineData("/computers")]
    [InlineData("/brands")]
    [InlineData("/categories")]
    public async Task Lists_have_bounded_defaults_and_a_trace_header(string path)
    {
        var body = await GetAsync(path);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("data").ValueKind);
        var pagination = body.GetProperty("pagination");
        Assert.Equal(1, pagination.GetProperty("page").GetInt32());
        Assert.Equal(24, pagination.GetProperty("pageSize").GetInt32());
        var total = pagination.GetProperty("total").GetInt32();
        Assert.Equal((int)Math.Ceiling(total / 24d), pagination.GetProperty("totalPages").GetInt32());
        if (path == "/computers") Assert.Equal(0, total);
    }

    [Fact]
    public async Task Nokia_detail_and_specifications_project_editorial_content_and_ordered_typed_values()
    {
        var detail = (await GetAsync("/devices/nokia-3310")).GetProperty("data");
        Assert.Equal("Nokia 3310", detail.GetProperty("name").GetString());
        foreach (var field in new[] { "shortDescription", "description", "history", "seoTitle", "seoDescription" })
            Assert.False(string.IsNullOrWhiteSpace(detail.GetProperty(field).GetString()));
        Assert.Equal("nokia", detail.GetProperty("brand").GetProperty("slug").GetString());
        Assert.Equal("feature-phones", detail.GetProperty("category").GetProperty("slug").GetString());
        Assert.Equal("phones", detail.GetProperty("category").GetProperty("parentSlug").GetString());
        Assert.Equal(2000, detail.GetProperty("releaseYear").GetInt32());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("releaseDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("discontinuedDate").ValueKind);
        Assert.Equal(133m, detail.GetProperty("physicalDetails").GetProperty("weightGrams").GetDecimal());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("physicalDetails").GetProperty("heightMm").ValueKind);
        Assert.NotEqual(default, detail.GetProperty("publishedAt").GetDateTimeOffset());
        Assert.False(detail.TryGetProperty("status", out _));

        var dedicated = (await GetAsync("/devices/nokia-3310/specifications")).GetProperty("data");
        Assert.Equal(detail.GetProperty("id").GetGuid(), dedicated.GetProperty("deviceId").GetGuid());
        var groups = detail.GetProperty("specificationGroups");
        Assert.Equal(groups.GetRawText(), dedicated.GetProperty("specificationGroups").GetRawText());
        Assert.Equal(new[] { "general", "design", "battery", "network", "software" },
            groups.EnumerateArray().Select(x => x.GetProperty("key").GetString()));
        var specs = groups.EnumerateArray().SelectMany(x => x.GetProperty("specifications").EnumerateArray()).ToArray();
        Assert.Equal(new[] { "announcement_date", "replaceable_covers", "antenna", "battery_chemistry",
            "talk_time_max", "standby_time_max", "network_bands", "sms_chat", "sms_segments_max" },
            specs.Select(x => x.GetProperty("key").GetString()));
        Assert.Equal("date", specs[0].GetProperty("dataType").GetString());
        Assert.Equal("2000-09-01", specs[0].GetProperty("valueDate").GetString());
        Assert.Equal("boolean", specs[1].GetProperty("dataType").GetString());
        Assert.True(specs[1].GetProperty("valueBoolean").GetBoolean());
        Assert.Equal("text", specs[3].GetProperty("dataType").GetString());
        Assert.Equal("NiMH", specs[3].GetProperty("valueText").GetString());
        Assert.Equal("number", specs[4].GetProperty("dataType").GetString());
        Assert.Equal(4.5m, specs[4].GetProperty("valueNumber").GetDecimal());
        Assert.Equal("h", specs[4].GetProperty("unit").GetString());
        Assert.All(specs, spec => Assert.Single(new[] { "valueText", "valueNumber", "valueBoolean", "valueDate" },
            field => spec.GetProperty(field).ValueKind != JsonValueKind.Null));
    }

    [Theory]
    [InlineData("missing-device")]
    [InlineData("private-draft")]
    [InlineData("private-archive")]
    [InlineData("private-computer")]
    public async Task Missing_and_unpublished_devices_have_identical_404_contracts(string slug)
    {
        foreach (var suffix in new[] { "", "/specifications" })
        {
            var error = await ErrorAsync($"/devices/{slug}{suffix}", HttpStatusCode.NotFound);
            Assert.Equal("DEVICE_NOT_FOUND", error.GetProperty("code").GetString());
            Assert.Equal("Device was not found.", error.GetProperty("message").GetString());
        }
    }

    [Fact]
    public async Task Published_devices_without_specifications_return_empty_groups_not_fabricated_values()
    {
        foreach (var path in new[] { "/devices/fixture-a", "/devices/fixture-a/specifications" })
        {
            var data = (await GetAsync(path)).GetProperty("data");
            Assert.Empty(data.GetProperty("specificationGroups").EnumerateArray());
        }
    }

    [Theory]
    [InlineData("", 7)]
    [InlineData("?brand=nokia", 6)]
    [InlineData("?brand=test-brand", 1)]
    [InlineData("?year=2000", 4)]
    [InlineData("?fromYear=2000&toYear=2001", 5)]
    [InlineData("?fromYear=2001", 1)]
    [InlineData("?toYear=1999", 1)]
    [InlineData("?decade=1990", 1)]
    [InlineData("?decade=2000", 5)]
    [InlineData("?category=phones", 7)]
    [InlineData("?category=feature-phones", 7)]
    [InlineData("?category=test-subtype", 1)]
    [InlineData("?type=phones", 7)]
    [InlineData("?type=computers", 0)]
    [InlineData("?type=computers&category=phones", 0)]
    [InlineData("?brand=missing-brand", 0)]
    [InlineData("?category=missing-category", 0)]
    [InlineData("?brand=unused-brand", 0)]
    [InlineData("?year=1999&decade=2000", 0)]
    [InlineData("?brand=nokia&category=phones&type=phones&year=2000&fromYear=1999&toYear=2001&decade=2000", 3)]
    public async Task Filters_intersect_and_never_include_unpublished_devices(string query, int total)
    {
        var body = await GetAsync("/devices" + query);
        Assert.Equal(total, body.GetProperty("pagination").GetProperty("total").GetInt32());
        Assert.Equal(total, body.GetProperty("data").GetArrayLength());
        Assert.DoesNotContain("private-", body.GetRawText());
    }

    [Fact]
    public async Task Type_endpoints_reuse_browse_filters_and_return_empty_pages_normally()
    {
        Assert.Equal((await GetAsync("/devices?type=phones&brand=nokia&year=2000")).GetRawText(),
            (await GetAsync("/phones?brand=nokia&year=2000")).GetRawText());
        Assert.Equal((await GetAsync("/phones")).GetRawText(), (await GetAsync("/phones?type=phones")).GetRawText());
        foreach (var path in new[] { "/computers", "/computers?brand=nokia", "/devices?page=10000", "/devices?year=1900" })
            Assert.Empty((await GetAsync(path)).GetProperty("data").EnumerateArray());
    }

    [Theory]
    [InlineData("release-asc", "fixture-old,fixture-a,fixture-b,fixture-other,nokia-3310,fixture-recent,fixture-unknown")]
    [InlineData("release-desc", "fixture-recent,fixture-a,fixture-b,fixture-other,nokia-3310,fixture-old,fixture-unknown")]
    [InlineData("name-asc", "fixture-a,fixture-b,fixture-old,fixture-other,fixture-recent,fixture-unknown,nokia-3310")]
    [InlineData("name-desc", "nokia-3310,fixture-a,fixture-b,fixture-old,fixture-other,fixture-recent,fixture-unknown")]
    public async Task Sorting_is_stable_across_pages_and_unknown_years_sort_last(string sort, string expected)
    {
        var slugs = new List<string>();
        for (var page = 1; page <= 4; page++)
        {
            var path = $"/devices?sort={sort}&page={page}&pageSize=2";
            var body = await GetAsync(path);
            Assert.Equal(body.GetRawText(), (await GetAsync(path)).GetRawText());
            Assert.Equal(7, body.GetProperty("pagination").GetProperty("total").GetInt32());
            Assert.Equal(4, body.GetProperty("pagination").GetProperty("totalPages").GetInt32());
            slugs.AddRange(body.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("slug").GetString()!));
        }
        Assert.Equal(expected.Split(','), slugs);
        Assert.Equal(7, slugs.Distinct().Count());
    }

    [Theory]
    [InlineData("/devices?page=0")]
    [InlineData("/devices?page=-1")]
    [InlineData("/devices?page=10001")]
    [InlineData("/devices?page=abc")]
    [InlineData("/devices?page=999999999999999999999")]
    [InlineData("/devices?pageSize=0")]
    [InlineData("/devices?pageSize=101")]
    [InlineData("/devices?year=0")]
    [InlineData("/devices?year=10000")]
    [InlineData("/devices?year=abc")]
    [InlineData("/devices?fromYear=2001&toYear=2000")]
    [InlineData("/devices?decade=1991")]
    [InlineData("/devices?decade=10000")]
    [InlineData("/devices?sort=random")]
    [InlineData("/devices?brand=Nokia")]
    [InlineData("/devices?category=feature_phones")]
    [InlineData("/devices?brand=")]
    [InlineData("/devices?type=")]
    [InlineData("/phones?type=computers")]
    [InlineData("/computers?type=phones")]
    [InlineData("/devices/Invalid_Slug")]
    [InlineData("/devices/Invalid_Slug/specifications")]
    [InlineData("/brands/Nokia")]
    [InlineData("/brands?pageSize=101")]
    [InlineData("/brands?page=abc")]
    [InlineData("/categories?page=0")]
    [InlineData("/categories?pageSize=abc")]
    public async Task Invalid_queries_and_binding_errors_use_the_same_400_envelope_in_production(string path)
    {
        var error = await ErrorAsync(path, HttpStatusCode.BadRequest);
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Brand_navigation_counts_only_published_devices_and_keeps_unused_reference_metadata_visible()
    {
        var brands = (await GetAsync("/brands")).GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal(new[] { "nokia", "test-brand", "unused-brand" }, brands.Select(x => x.GetProperty("slug").GetString()));
        Assert.Equal(new[] { 6, 1, 0 }, brands.Select(x => x.GetProperty("publishedDeviceCount").GetInt32()));
        Assert.Equal(brands[0].GetRawText(), (await GetAsync("/brands/nokia")).GetProperty("data").GetRawText());
        var secondPage = (await GetAsync("/brands?page=2&pageSize=1")).GetProperty("data");
        Assert.Equal("test-brand", Assert.Single(secondPage.EnumerateArray()).GetProperty("slug").GetString());
        Assert.Equal("BRAND_NOT_FOUND", (await ErrorAsync("/brands/missing-brand", HttpStatusCode.NotFound))
            .GetProperty("code").GetString());
    }

    [Fact]
    public async Task Categories_expose_ordered_flat_taxonomy_with_parent_identity()
    {
        var categories = (await GetAsync("/categories")).GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal(5, categories.Length);
        Assert.Equal(categories.OrderBy(x => x.GetProperty("displayOrder").GetInt32())
            .ThenBy(x => x.GetProperty("slug").GetString(), StringComparer.Ordinal).Select(x => x.GetRawText()),
            categories.Select(x => x.GetRawText()));
        var phones = categories.Single(x => x.GetProperty("slug").GetString() == "phones");
        Assert.Equal(JsonValueKind.Null, phones.GetProperty("parentCategoryId").ValueKind);
        var feature = categories.Single(x => x.GetProperty("slug").GetString() == "feature-phones");
        Assert.Equal(phones.GetProperty("id").GetGuid(), feature.GetProperty("parentCategoryId").GetGuid());
        Assert.Equal("phones", feature.GetProperty("parentSlug").GetString());
        Assert.Empty((await GetAsync("/categories?page=10000")).GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task Read_handlers_project_without_tracking_domain_entities()
    {
        await using var db = fixture.CreateContext();
        Assert.True((await new BrowseDevicesHandler(db).HandleAsync(new(Category: "phones"), Ct)).IsSuccess);
        Assert.True((await new GetDeviceHandler(db).HandleAsync(new("nokia-3310"), Ct)).IsSuccess);
        Assert.True((await new GetDeviceSpecificationsHandler(db).HandleAsync(new("nokia-3310"), Ct)).IsSuccess);
        Assert.True((await new GetBrandsHandler(db).HandleAsync(new(), Ct)).IsSuccess);
        Assert.True((await new GetBrandHandler(db).HandleAsync(new("nokia"), Ct)).IsSuccess);
        Assert.True((await new GetCategoriesHandler(db).HandleAsync(new(), Ct)).IsSuccess);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task OpenApi_describes_all_ten_routes_in_development_only()
    {
        using var production = await fixture.Client.GetAsync("/openapi/v1.json", Ct);
        Assert.Equal(HttpStatusCode.NotFound, production.StatusCode);
        await using var development = fixture.CreateFactory("Development");
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", Ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        foreach (var path in new[] { "/devices", "/devices/{slug}", "/devices/{slug}/specifications",
            "/phones", "/computers", "/brands", "/brands/{slug}", "/categories", "/search", "/timeline" })
        {
            var responses = document.RootElement.GetProperty("paths").GetProperty("/api/v1" + path)
                .GetProperty("get").GetProperty("responses");
            Assert.True(responses.GetProperty("200").TryGetProperty("content", out _));
            Assert.True(responses.GetProperty("400").TryGetProperty("content", out _));
        }
    }

    [Fact]
    public async Task Unknown_routes_and_unsupported_methods_use_json_errors()
    {
        Assert.Equal("NOT_FOUND", (await ErrorAsync("/missing", HttpStatusCode.NotFound)).GetProperty("code").GetString());
        using var response = await fixture.Client.PostAsync("/api/v1/devices", null, Ct);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("METHOD_NOT_ALLOWED", document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private async Task<JsonElement> GetAsync(string path)
    {
        using var response = await fixture.Client.GetAsync("/api/v1" + path, Ct);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(response.Headers.GetValues("X-Trace-Id"))));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> ErrorAsync(string path, HttpStatusCode expected)
    {
        using var response = await fixture.Client.GetAsync("/api/v1" + path, Ct);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.False(document.RootElement.TryGetProperty("data", out _));
        var error = document.RootElement.GetProperty("error");
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Trace-Id")), error.GetProperty("traceId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("message").GetString()));
        return error.Clone();
    }
}
