using TechVault.Application.Search;
using TechVault.Application.Timeline;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Devices;

namespace TechVault.UnitTests;

public sealed class DiscoveryTests
{
    [Fact]
    public void Search_metadata_is_optional_trimmed_deduplicated_and_copied()
    {
        var device = CreateDevice();
        Assert.Null(device.ModelNumber);
        Assert.Empty(device.Aliases);
        var aliases = new[] { " Classic phone ", "classic PHONE", "3310 original" };
        device.SetSearchMetadata(" NHM-5 ", aliases);
        aliases[0] = "Changed externally";
        Assert.Equal("NHM-5", device.ModelNumber);
        Assert.Equal(new[] { "Classic phone", "3310 original" }, device.Aliases);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)device.Aliases)[0] = "Mutation");
        device.SetSearchMetadata(null, []);
        Assert.Null(device.ModelNumber);
        Assert.Empty(device.Aliases);
    }

    [Fact]
    public void Invalid_search_metadata_leaves_previous_edit_unchanged()
    {
        var device = CreateDevice();
        device.SetSearchMetadata("Original", ["Old alias"]);
        var updatedAt = device.UpdatedAt;
        Assert.Throws<ArgumentException>(() => device.SetSearchMetadata(" ", []));
        Assert.Throws<ArgumentException>(() => device.SetSearchMetadata(new string('x', 101), []));
        Assert.Throws<ArgumentException>(() => device.SetSearchMetadata("Changed", ["Valid", " "]));
        Assert.Throws<ArgumentException>(() => device.SetSearchMetadata("Changed", [new string('x', 101)]));
        Assert.Throws<ArgumentException>(() => device.SetSearchMetadata("Changed", Enumerable.Range(1, 21).Select(x => $"Alias {x}")));
        Assert.Throws<ArgumentNullException>(() => device.SetSearchMetadata("Changed", null!));
        Assert.Equal("Original", device.ModelNumber);
        Assert.Equal("Old alias", Assert.Single(device.Aliases));
        Assert.Equal(updatedAt, device.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("& | ! : * ' ()")]
    public void Search_requires_searchable_text(string? text) =>
        Assert.NotNull(SearchDevicesValidator.Validate(new(text)));

    [Theory]
    [InlineData("NOKIA 3310")]
    [InlineData("the")]
    [InlineData("NHM-5")]
    [InlineData("điện thoại")]
    public void Search_accepts_plain_unicode_and_model_terms(string text) =>
        Assert.Null(SearchDevicesValidator.Validate(new(text)));

    [Fact]
    public void Search_has_bounded_input_and_pagination()
    {
        Assert.Null(SearchDevicesValidator.Validate(new(new string('x', 200), 10_000, 100)));
        Assert.NotNull(SearchDevicesValidator.Validate(new(new string('x', 201))));
        Assert.NotNull(SearchDevicesValidator.Validate(new("nokia", 0)));
        Assert.NotNull(SearchDevicesValidator.Validate(new("nokia", 10_001)));
        Assert.NotNull(SearchDevicesValidator.Validate(new("nokia", PageSize: 0)));
        Assert.NotNull(SearchDevicesValidator.Validate(new("nokia", PageSize: 101)));
    }

    [Theory]
    [InlineData("1990s")]
    [InlineData("2000s")]
    [InlineData("0010s")]
    [InlineData("9990s")]
    public void Timeline_accepts_decade_eras(string era) => Assert.Null(GetTimelineValidator.Validate(new(Era: era)));

    [Theory]
    [InlineData("")]
    [InlineData("1990")]
    [InlineData("1991s")]
    [InlineData("1990S")]
    [InlineData("0000s")]
    [InlineData("10000s")]
    [InlineData("+990s")]
    [InlineData(" 990s")]
    [InlineData("modern")]
    public void Timeline_rejects_invalid_eras(string era) => Assert.NotNull(GetTimelineValidator.Validate(new(Era: era)));

    [Fact]
    public void Timeline_reuses_browse_filter_and_pagination_validation()
    {
        Assert.Null(GetTimelineValidator.Validate(new(Brand: "nokia", Category: "phones", Type: "phones",
            Year: 1999, FromYear: 1990, ToYear: 2000, Era: "1990s", Page: 10_000, PageSize: 100)));
        Assert.NotNull(GetTimelineValidator.Validate(new(Brand: "Bad Slug")));
        Assert.NotNull(GetTimelineValidator.Validate(new(Category: "")));
        Assert.NotNull(GetTimelineValidator.Validate(new(Type: "tablets")));
        Assert.NotNull(GetTimelineValidator.Validate(new(Year: 0)));
        Assert.NotNull(GetTimelineValidator.Validate(new(ToYear: 10000)));
        Assert.NotNull(GetTimelineValidator.Validate(new(FromYear: 2000, ToYear: 1990)));
        Assert.NotNull(GetTimelineValidator.Validate(new(Page: 0)));
        Assert.NotNull(GetTimelineValidator.Validate(new(PageSize: 101)));
    }

    private static Device CreateDevice() => new("Example", "example", new Brand("Nokia", "nokia"), new Category("Phones", "phones", 0));
}
