using TechVault.Application.Comparisons;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Comparisons;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.UnitTests;

public sealed class ComparisonTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("nokia-3310")]
    [InlineData("nokia-3310,nokia-3310")]
    [InlineData("nokia-3310,nokia-3210,imac-g3")]
    [InlineData(",nokia-3310")]
    [InlineData("nokia-3310,")]
    [InlineData("Nokia-3310,nokia-3210")]
    [InlineData("nokia-3310, nokia-3210")]
    [InlineData("nokia_3310,nokia-3210")]
    public void Comparison_requires_exactly_two_distinct_valid_slugs(string? devices) =>
        Assert.NotNull(CompareDevicesValidator.Validate(new(devices)));

    [Fact]
    public void Comparison_accepts_both_modes_and_enforces_per_slug_and_total_bounds()
    {
        Assert.Null(CompareDevicesValidator.Validate(new("nokia-3310,nokia-3210")));
        Assert.Null(CompareDevicesValidator.Validate(new("imac-g3,macintosh-128k", true)));
        Assert.Null(CompareDevicesValidator.Validate(new(new string('a', 160) + "," + new string('b', 160))));
        Assert.NotNull(CompareDevicesValidator.Validate(new(new string('a', 161) + ",b")));
        Assert.NotNull(CompareDevicesValidator.Validate(new(new string('a', 322))));
    }

    [Fact]
    public void Compatibility_is_explicit_optional_and_independent_of_category()
    {
        var category = new Category("Phones", "phones", 0);
        var device = new Device("Example", "example", new Brand("Brand", "brand"), category);
        Assert.Null(device.ComparisonGroupId);
        var group = new ComparisonGroup("Phones", "phone");
        device.SetComparisonGroup(group);
        var editedAt = device.UpdatedAt;
        Assert.Equal(group.Id, device.ComparisonGroupId);
        Assert.Same(group, device.ComparisonGroup);
        Assert.Equal(category.Id, device.CategoryId);
        device.SetComparisonGroup(group);
        Assert.Equal(editedAt, device.UpdatedAt);
        device.SetComparisonGroup(null);
        Assert.Null(device.ComparisonGroupId);
        Assert.Null(device.ComparisonGroup);
    }

    [Fact]
    public void Group_identity_and_opt_in_definitions_are_validated()
    {
        Assert.Throws<ArgumentException>(() => new ComparisonGroup(" ", "phone"));
        Assert.Throws<ArgumentException>(() => new ComparisonGroup("Phones", "PHONE"));
        Assert.Throws<ArgumentException>(() => new ComparisonGroup("Phones", "bad-key"));
        var group = new SpecificationGroup("General", "general", 0);
        var definition = new SpecificationDefinition("Example", "example", group, SpecificationDataType.Number, 0);
        Assert.False(definition.IsComparable);
        definition.SetComparable(true);
        Assert.True(definition.IsComparable);
        definition.SetComparable(false);
        Assert.False(definition.IsComparable);
        Assert.True(new SpecificationDefinition("Other", "other", group, SpecificationDataType.Text, 0, isComparable: true).IsComparable);
    }

    [Fact]
    public void Comparison_equality_preserves_missing_zero_false_dates_and_exact_text()
    {
        var missing = new ComparisonValueResponse(true);
        Assert.NotEqual(missing, new ComparisonValueResponse(false, ValueNumber: 0));
        Assert.NotEqual(missing, new ComparisonValueResponse(false, ValueBoolean: false));
        Assert.Equal(new ComparisonValueResponse(false, ValueNumber: 1m), new ComparisonValueResponse(false, ValueNumber: 1.00m));
        Assert.NotEqual(new ComparisonValueResponse(false, ValueText: "GSM"), new ComparisonValueResponse(false, ValueText: "gsm"));
        Assert.Equal(new ComparisonValueResponse(false, ValueDate: new DateOnly(2000, 1, 1)),
            new ComparisonValueResponse(false, ValueDate: new DateOnly(2000, 1, 1)));
        Assert.NotEqual(new ComparisonValueResponse(false, ValueDate: new DateOnly(2000, 1, 1)),
            new ComparisonValueResponse(false, ValueDate: new DateOnly(2000, 1, 2)));
    }
}
