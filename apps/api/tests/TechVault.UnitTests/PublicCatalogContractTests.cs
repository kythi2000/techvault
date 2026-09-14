using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Application.Common.Validation;
using TechVault.Application.Devices.BrowseDevices;

namespace TechVault.UnitTests;

public sealed class PublicCatalogContractTests
{
    [Fact]
    public void Result_distinguishes_success_from_expected_failure()
    {
        var success = Result<string>.Success("value");
        Assert.True(success.IsSuccess);
        Assert.Equal("value", success.Value);
        Assert.Null(success.Error);
        var error = Error.DeviceNotFound();
        var failure = Result<string>.Failure(error);
        Assert.False(failure.IsSuccess);
        Assert.Same(error, failure.Error);
        Assert.Throws<InvalidOperationException>(() => failure.Value);
        Assert.Throws<ArgumentNullException>(() => Result<string>.Success(null!));
        Assert.Throws<ArgumentNullException>(() => Result<string>.Failure(null!));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(24, 1)]
    [InlineData(25, 2)]
    public void Pagination_reports_total_pages(int total, int totalPages)
    {
        var result = PagedResult<string>.Create([], 1, 24, total);
        Assert.Equal(new PaginationMetadata(1, 24, total, totalPages), result.Pagination);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10000, 100)]
    public void Pagination_accepts_boundaries(int page, int pageSize) =>
        Assert.Null(PaginationRules.Validate(page, pageSize));

    [Theory]
    [InlineData(0, 24)]
    [InlineData(-1, 24)]
    [InlineData(10001, 24)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Pagination_rejects_out_of_bounds_values(int page, int pageSize) =>
        Assert.Equal(ErrorType.Validation, PaginationRules.Validate(page, pageSize)!.Type);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nokia")]
    [InlineData("nokia_3310")]
    [InlineData("-nokia")]
    [InlineData("nokia--3310")]
    [InlineData("nokia\n")]
    public void Slugs_require_lowercase_hyphenated_identifiers(string? slug) =>
        Assert.NotNull(RequestValidation.Slug(slug, "slug"));

    [Fact]
    public void Slug_length_matches_the_stored_identifier_limit()
    {
        Assert.Null(RequestValidation.Slug("nokia-3310", "slug"));
        Assert.Null(RequestValidation.Slug(new string('a', 160), "slug"));
        Assert.NotNull(RequestValidation.Slug(new string('a', 161), "slug"));
    }

    [Fact]
    public void Browse_validation_allows_defaults_and_combined_filters()
    {
        Assert.Null(BrowseDevicesValidator.Validate(new()));
        Assert.Null(BrowseDevicesValidator.Validate(new(Brand: "nokia", Category: "feature-phones",
            Type: "phones", Year: 2000, FromYear: 1990, ToYear: 2009, Decade: 2000), "phones"));
        Assert.Null(BrowseDevicesValidator.Validate(new(FromYear: 1, ToYear: 9999, Decade: 9990)));
        Assert.Null(BrowseDevicesValidator.Validate(new(FromYear: 2000)));
        Assert.Null(BrowseDevicesValidator.Validate(new(ToYear: 2000)));
        // Valid but non-overlapping filters represent an empty intersection, not malformed input.
        Assert.Null(BrowseDevicesValidator.Validate(new(Year: 1990, Decade: 2000)));
    }

    [Fact]
    public void Browse_validation_rejects_invalid_filters_before_querying()
    {
        BrowseDevicesQuery[] invalid = [new(Type: "phone"), new(Type: ""), new(Sort: "random"),
            new(Year: 0), new(Year: 10000), new(FromYear: -1), new(ToYear: 10000),
            new(FromYear: 2001, ToYear: 2000), new(Decade: 1991), new(Decade: 0), new(Decade: 10000),
            new(Brand: ""), new(Category: "Feature Phones")];
        Assert.All(invalid, query => Assert.Equal(ErrorType.Validation, BrowseDevicesValidator.Validate(query)!.Type));
        Assert.NotNull(BrowseDevicesValidator.Validate(new(Type: "computers"), "phones"));
        Assert.NotNull(BrowseDevicesValidator.Validate(new(Type: "phones"), "computers"));
    }
}
