using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.UnitTests;

public sealed class CatalogDomainTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Nokia-3310")]
    [InlineData("nokia 3310")]
    [InlineData("nokia--3310")]
    [InlineData("../nokia")]
    public void Invalid_slugs_are_rejected(string slug)
    {
        Assert.Throws<ArgumentException>(() => new Brand("Nokia", slug));
        Assert.Throws<ArgumentException>(() => new Category("Phones", slug, 0));
        Assert.Throws<ArgumentException>(() => new Device("Nokia 3310", slug,
            new Brand("Nokia", "nokia"), new Category("Phones", "phones", 0)));
    }

    [Fact]
    public void Hierarchy_and_device_classification_use_required_entities()
    {
        var root = new Category("Phones", "phones", 10);
        var child = new Category("Feature Phones", "feature-phones", 10, root);
        var brand = new Brand("Nokia", "nokia");
        var device = new Device("Nokia 3310", "nokia-3310", brand, child);
        Assert.Null(root.ParentCategoryId);
        Assert.Equal(root.Id, child.ParentCategoryId);
        Assert.Equal(child.Id, device.CategoryId);
        Assert.Equal(brand.Id, device.BrandId);
        Assert.Throws<ArgumentNullException>(() => new Device("Test", "test", null!, child));
        Assert.Throws<ArgumentNullException>(() => new Device("Test", "test", brand, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Category("Phones", "phones", -1));
    }

    [Fact]
    public void New_device_is_a_draft_and_does_not_invent_unknown_measurements_or_dates()
    {
        var device = CreateDevice();
        Assert.Equal(DeviceStatus.Draft, device.Status);
        Assert.Null(device.PublishedAt);
        Assert.Null(device.ReleaseYear);
        Assert.Null(device.ReleaseDate);
        Assert.Null(device.DiscontinuedDate);
        Assert.Null(device.HeightMm);
        Assert.Null(device.WidthMm);
        Assert.Null(device.DepthMm);
        Assert.Null(device.WeightGrams);
        Assert.NotEqual(Guid.Empty, device.Id);
        Assert.Equal(TimeSpan.Zero, device.CreatedAt.Offset);
    }

    [Fact]
    public void Publication_requires_content_and_is_idempotent()
    {
        var device = CreateDevice();
        Assert.Throws<InvalidOperationException>(device.Publish);
        device.UpdateContent("Summary", "Overview", "History", "SEO title", "SEO description");
        device.Publish();
        var publishedAt = device.PublishedAt;
        var updatedAt = device.UpdatedAt;
        Assert.Equal(DeviceStatus.Published, device.Status);
        Assert.NotNull(publishedAt);
        device.Publish();
        Assert.Equal(publishedAt, device.PublishedAt);
        Assert.Equal(updatedAt, device.UpdatedAt);
    }

    [Fact]
    public void Invalid_editorial_update_leaves_previous_content_unchanged()
    {
        var device = CreateDevice();
        device.UpdateContent("Summary", "Overview", "History", "SEO title", "SEO description");
        var updatedAt = device.UpdatedAt;
        Assert.Throws<ArgumentException>(() => device.UpdateContent("Changed", "Changed", "Changed", " ", "Changed"));
        Assert.Equal("Summary", device.ShortDescription);
        Assert.Equal("History", device.History);
        Assert.Equal(updatedAt, device.UpdatedAt);
    }

    [Fact]
    public void Partial_release_information_does_not_invent_a_day()
    {
        var device = CreateDevice();
        device.SetRelease(2000);
        Assert.Equal(2000, device.ReleaseYear);
        Assert.Null(device.ReleaseDate);
        device.SetRelease(null, new DateOnly(2000, 10, 1));
        Assert.Equal(2000, device.ReleaseYear);
        Assert.Equal(new DateOnly(2000, 10, 1), device.ReleaseDate);
    }

    [Fact]
    public void Inconsistent_or_invalid_release_information_is_rejected()
    {
        var device = CreateDevice();
        Assert.Throws<ArgumentOutOfRangeException>(() => device.SetRelease(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => device.SetRelease(10000));
        Assert.Throws<ArgumentException>(() => device.SetRelease(2000, new DateOnly(2001, 1, 1)));
        Assert.Throws<ArgumentException>(() => device.SetRelease(2000, discontinuedDate: new DateOnly(1999, 1, 1)));
        Assert.Throws<ArgumentException>(() => device.SetRelease(2000, new DateOnly(2000, 10, 1), new DateOnly(2000, 9, 1)));
        Assert.Null(device.ReleaseYear);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Known_physical_measurements_must_be_positive(int invalid)
    {
        var device = CreateDevice();
        Assert.Throws<ArgumentOutOfRangeException>(() => device.SetPhysicalDetails(invalid, null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => device.SetPhysicalDetails(null, invalid, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => device.SetPhysicalDetails(null, null, invalid, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => device.SetPhysicalDetails(null, null, null, invalid));
        device.SetPhysicalDetails(null, null, null, 133);
        Assert.Equal(133m, device.WeightGrams);
        Assert.Null(device.HeightMm);
    }

    [Fact]
    public void All_four_specification_types_preserve_their_values()
    {
        var device = CreateDevice();
        var group = new SpecificationGroup("General", "general", 10);
        var values = new[]
        {
            SpecificationValue.Text("GSM"), SpecificationValue.Number(0),
            SpecificationValue.Boolean(false), SpecificationValue.Date(new DateOnly(2000, 9, 1))
        };
        foreach (var value in values)
        {
            var definition = new SpecificationDefinition(value.DataType.ToString(), value.DataType.ToString().ToLowerInvariant(),
                group, value.DataType, 10);
            device.SetSpecification(definition, value);
        }
        Assert.Equal(4, device.Specifications.Count);
        Assert.Equal("GSM", device.Specifications.Single(x => x.DataType == SpecificationDataType.Text).ValueText);
        Assert.Equal(0m, device.Specifications.Single(x => x.DataType == SpecificationDataType.Number).ValueNumber);
        Assert.False(device.Specifications.Single(x => x.DataType == SpecificationDataType.Boolean).ValueBoolean);
        Assert.Equal(new DateOnly(2000, 9, 1), device.Specifications.Single(x => x.DataType == SpecificationDataType.Date).ValueDate);
    }

    [Fact]
    public void Specification_edits_do_not_duplicate_a_definition_and_reject_wrong_types()
    {
        var device = CreateDevice();
        var group = new SpecificationGroup("Battery", "battery", 10);
        var definition = new SpecificationDefinition("Talk time", "talk_time", group, SpecificationDataType.Number, 10, "h");
        device.SetSpecification(definition, SpecificationValue.Number(4.5m));
        device.SetSpecification(definition, SpecificationValue.Number(5));
        Assert.Equal(5m, Assert.Single(device.Specifications).ValueNumber);
        Assert.Throws<ArgumentException>(() => device.SetSpecification(definition, SpecificationValue.Text("5")));
        Assert.Equal(5m, Assert.Single(device.Specifications).ValueNumber);
        Assert.Throws<ArgumentException>(() => SpecificationValue.Text(" "));
    }

    [Fact]
    public void Specification_metadata_rejects_invalid_keys_types_and_order()
    {
        var group = new SpecificationGroup("General", "general", 0);
        Assert.Throws<ArgumentException>(() => new SpecificationGroup("General", "Bad Key", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpecificationGroup("General", "general", -1));
        Assert.Throws<ArgumentException>(() => new SpecificationDefinition("Test", "Bad Key", group, SpecificationDataType.Text, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpecificationDefinition("Test", "test", group, (SpecificationDataType)99, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpecificationDefinition("Test", "test", group, SpecificationDataType.Text, -1));
        Assert.Throws<ArgumentNullException>(() => new SpecificationDefinition("Test", "test", null!, SpecificationDataType.Text, 0));
    }

    [Fact]
    public void Domain_has_no_persistence_or_host_dependencies()
    {
        Assert.DoesNotContain(typeof(Device).Assembly.GetReferencedAssemblies(), assembly =>
            assembly.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
            assembly.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            assembly.Name.StartsWith("TechVault.Infrastructure", StringComparison.Ordinal));
    }

    private static Device CreateDevice() => new("Nokia 3310", "nokia-3310",
        new Brand("Nokia", "nokia"), new Category("Phones", "phones", 0));
}
