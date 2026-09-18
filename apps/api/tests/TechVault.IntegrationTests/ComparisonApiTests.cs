using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechVault.Api.Responses;
using TechVault.Application.Comparisons;
using TechVault.Infrastructure.Persistence;

namespace TechVault.IntegrationTests;

public sealed class ComparisonApiTests(MixedCatalogFixture fixture) : IClassFixture<MixedCatalogFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("nokia-3310,nokia-3210", "phone", 13, 10)]
    [InlineData("macintosh-128k,imac-g3", "all_in_one", 15, 15)]
    public async Task Both_real_pairs_align_only_comparable_definitions_and_filter_differences(
        string devices, string groupKey, int rows, int differences)
    {
        var result = await CompareAsync(devices);
        Assert.Equal(devices.Split(','), result.Devices.Select(x => x.Slug));
        Assert.Equal(groupKey, result.ComparisonGroup.Key);
        Assert.False(result.DifferencesOnly);
        var specs = Specs(result);
        Assert.Equal(rows, specs.Length);
        Assert.Equal(rows, specs.Select(x => x.Id).Distinct().Count());
        Assert.DoesNotContain(specs, x => x.Key == "announcement_date");
        Assert.Equal(result.SpecificationGroups.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Key).Select(x => x.Key),
            result.SpecificationGroups.Select(x => x.Key));
        foreach (var group in result.SpecificationGroups)
        {
            Assert.Equal(group.Specifications.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Key).Select(x => x.Key),
                group.Specifications.Select(x => x.Key));
            foreach (var spec in group.Specifications)
            {
                Assert.Equal(2, spec.Values.Count);
                Assert.Equal(spec.Values[0] != spec.Values[1], spec.IsDifferent);
                foreach (var value in spec.Values)
                    Assert.Equal(value.IsMissing ? 0 : 1, new object?[] { value.ValueText, value.ValueNumber,
                        value.ValueBoolean, value.ValueDate }.Count(x => x is not null));
            }
        }
        var filtered = await CompareAsync(devices, true);
        Assert.True(filtered.DifferencesOnly);
        Assert.Equal(differences, Specs(filtered).Length);
        Assert.All(Specs(filtered), x => Assert.True(x.IsDifferent));
        Assert.All(filtered.SpecificationGroups, x => Assert.NotEmpty(x.Specifications));
        Assert.Equal(specs.Where(x => x.IsDifferent).Select(x => x.Id), Specs(filtered).Select(x => x.Id));
        Assert.Equal(specs.Select(x => x.Id), Specs(await CompareAsync(devices)).Select(x => x.Id));
    }

    [Fact]
    public async Task Reversing_devices_reverses_value_columns_without_changing_row_order_or_difference_flags()
    {
        var normal = Specs(await CompareAsync("nokia-3310,nokia-3210"));
        var reversed = Specs(await CompareAsync("nokia-3210,nokia-3310"));
        Assert.Equal(normal.Select(x => x.Id), reversed.Select(x => x.Id));
        for (var i = 0; i < normal.Length; i++)
        {
            Assert.Equal(normal[i].Values.Reverse(), reversed[i].Values);
            Assert.Equal(normal[i].IsDifferent, reversed[i].IsDifferent);
        }
        var talk = normal.Single(x => x.Key == "talk_time_max");
        Assert.Equal("h", talk.Unit);
        Assert.Equal(4.5m, talk.Values[0].ValueNumber);
        Assert.True(talk.Values[1].IsMissing);
        Assert.Null(talk.Values[1].ValueNumber);
        var same = normal.Single(x => x.Key == "replaceable_covers");
        Assert.False(same.IsDifferent);
    }

    [Fact]
    public async Task Computers_keep_zero_and_false_values_without_a_winner_or_implicit_conversion()
    {
        var specs = Specs(await CompareAsync("macintosh-128k,imac-g3"));
        var slots = specs.Single(x => x.Key == "ram_slots");
        Assert.False(slots.Values[0].IsMissing);
        Assert.Equal(0m, slots.Values[0].ValueNumber);
        Assert.Equal(2m, slots.Values[1].ValueNumber);
        var disk = specs.Single(x => x.Key == "internal_hard_disk");
        Assert.False(disk.Values[0].IsMissing);
        Assert.False(disk.Values[0].ValueBoolean);
        Assert.True(disk.Values[1].ValueBoolean);
        var ram = specs.Single(x => x.Key == "ram_capacity");
        Assert.Equal("MiB", ram.Unit);
        Assert.Equal(0.125m, ram.Values[0].ValueNumber);
        Assert.Equal(32m, ram.Values[1].ValueNumber);
        var floppy = specs.Single(x => x.Key == "floppy_format");
        Assert.Equal("400K", floppy.Values[0].ValueText);
        Assert.True(floppy.Values[1].IsMissing);
    }

    [Theory]
    [InlineData("", 400, "VALIDATION_ERROR")]
    [InlineData("?devices=", 400, "VALIDATION_ERROR")]
    [InlineData("?devices=nokia-3310", 400, "VALIDATION_ERROR")]
    [InlineData("?devices=nokia-3310,nokia-3310", 400, "VALIDATION_ERROR")]
    [InlineData("?devices=nokia-3310,nokia-3210,imac-g3", 400, "VALIDATION_ERROR")]
    [InlineData("?devices=,nokia-3310", 400, "VALIDATION_ERROR")]
    [InlineData("?devices=nokia-3310,nokia-3210,", 400, "VALIDATION_ERROR")]
    [InlineData("?devices=Nokia-3310,nokia-3210", 400, "VALIDATION_ERROR")]
    [InlineData("?devices=nokia-3310,%20nokia-3210", 400, "VALIDATION_ERROR")]
    [InlineData("?devices=nokia-3310,nokia-3210&differencesOnly=invalid", 400, "VALIDATION_ERROR")]
    [InlineData("?devices=nokia-3310,missing", 404, "DEVICE_NOT_FOUND")]
    [InlineData("?devices=missing,also-missing", 404, "DEVICE_NOT_FOUND")]
    [InlineData("?devices=nokia-3310,imac-g3", 400, "INCOMPATIBLE_DEVICES")]
    public async Task Invalid_pairs_have_explicit_errors_and_the_standard_trace_envelope(string query, int status, string code)
    {
        using var response = await fixture.Client.GetAsync("/api/v1/compare" + query, Ct);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiErrorResponse>(Ct))!.Error;
        Assert.Equal(code, error.Code);
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Trace-Id")), error.TraceId);
    }

    [Fact]
    public async Task Comparison_is_read_only_and_leaves_no_tracked_entities()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TechVaultDbContext>();
        Assert.True((await scope.ServiceProvider.GetRequiredService<CompareDevicesHandler>()
            .HandleAsync(new("nokia-3310,nokia-3210"), Ct)).IsSuccess);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(2, await db.ComparisonGroups.CountAsync(Ct));
        Assert.Equal(28, await db.SpecificationDefinitions.CountAsync(x => x.IsComparable, Ct));
        using var post = await fixture.Client.PostAsync("/api/v1/compare", null, Ct);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    private async Task<CompareDevicesResponse> CompareAsync(string devices, bool differencesOnly = false)
    {
        using var response = await fixture.Client.GetAsync($"/api/v1/compare?devices={devices}&differencesOnly={differencesOnly}", Ct);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(response.Headers.GetValues("X-Trace-Id"))));
        return (await response.Content.ReadFromJsonAsync<ApiResponse<CompareDevicesResponse>>(Ct))!.Data;
    }

    private static ComparisonSpecificationResponse[] Specs(CompareDevicesResponse response) =>
        response.SpecificationGroups.SelectMany(x => x.Specifications).ToArray();
}
