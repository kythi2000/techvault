using TechVault.Application.Admin.Devices;
using TechVault.Application.Admin.References;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.UnitTests;

public sealed class AdminRulesTests
{
    [Fact]
    public void Draft_content_can_be_incomplete_but_publication_cannot()
    {
        var device = Create();
        device.UpdateDraftContent("Summary", "", "", "", "");
        Assert.Throws<InvalidOperationException>(device.Publish);
        device.UpdateDraftContent("Summary", "Description", "History", "Title", "SEO");
        device.Publish();
        Assert.Throws<InvalidOperationException>(() => device.UpdateDraftContent("", "", "", "", ""));
        Assert.Equal("Summary", device.ShortDescription);
    }

    [Fact]
    public void Lifecycle_is_idempotent_and_preserves_content_and_creation_identity()
    {
        var device = Create();
        var createdAt = device.CreatedAt;
        var id = device.Id;
        var initialUpdated = device.UpdatedAt;
        device.Unpublish();
        Assert.Equal(initialUpdated, device.UpdatedAt);
        device.UpdateContent("Summary", "Description", "History", "Title", "SEO");
        device.Publish();
        device.Unpublish();
        Assert.Equal(DeviceStatus.Draft, device.Status);
        Assert.Null(device.PublishedAt);
        device.Publish();
        device.Archive();
        var archivedAt = device.UpdatedAt;
        device.Archive();
        Assert.Equal(archivedAt, device.UpdatedAt);
        Assert.Equal(DeviceStatus.Archived, device.Status);
        Assert.Null(device.PublishedAt);
        Assert.Equal("History", device.History);
        Assert.Equal(id, device.Id);
        Assert.Equal(createdAt, device.CreatedAt);
        Assert.Throws<InvalidOperationException>(device.Publish);
        Assert.Throws<InvalidOperationException>(device.Unpublish);
    }

    [Fact]
    public void Invalid_identity_and_draft_edits_do_not_partially_mutate_domain_objects()
    {
        var device = Create();
        var updatedAt = device.UpdatedAt;
        Assert.Throws<ArgumentException>(() => device.UpdateIdentity("Changed", "BAD", device.Brand, device.Category));
        Assert.Equal("Device", device.Name);
        Assert.Throws<ArgumentException>(() => device.UpdateDraftContent("Changed", "", "", new string('x', 201), ""));
        Assert.Equal("", device.ShortDescription);
        Assert.Equal(updatedAt, device.UpdatedAt);
    }

    [Fact]
    public void Specification_removal_is_explicit_and_reference_edits_keep_semantic_identity()
    {
        var device = Create();
        var group = new SpecificationGroup("General", "general", 0);
        var definition = new SpecificationDefinition("Size", "size", group, SpecificationDataType.Number, 0, "mm");
        device.SetSpecification(definition, SpecificationValue.Number(0));
        Assert.False(device.RemoveSpecification(Guid.NewGuid()));
        Assert.True(device.RemoveSpecification(definition.Id));
        Assert.Empty(device.Specifications);
        definition.UpdateDetails("Width", group, 2, true);
        Assert.Equal("size", definition.Key);
        Assert.Equal("mm", definition.Unit);
        Assert.Equal(SpecificationDataType.Number, definition.DataType);
        Assert.True(definition.IsComparable);
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.UpdateDetails("Changed", group, -1, false));
        Assert.Equal("Width", definition.Name);
        device.Brand.UpdateDetails("Renamed", "Editorial description");
        device.Category.UpdateDetails("Edited category", "Description", 2);
        Assert.Equal("brand", device.Brand.Slug);
        Assert.Equal("category", device.Category.Slug);
    }

    [Fact]
    public void Validators_accept_partial_drafts_zero_false_and_unknown_optional_fields()
    {
        var input = new DeviceInput("Device", "device", Guid.NewGuid(), Guid.NewGuid());
        Assert.True(new DeviceInputValidator().Validate(input).IsValid);
        var validator = new SpecificationInputValidator();
        Assert.True(validator.Validate(new SpecificationInput(ValueNumber: 0)).IsValid);
        Assert.True(validator.Validate(new SpecificationInput(ValueBoolean: false)).IsValid);
        Assert.False(validator.Validate(new SpecificationInput()).IsValid);
        Assert.False(validator.Validate(new SpecificationInput(ValueNumber: 0, ValueBoolean: false)).IsValid);
        Assert.False(validator.Validate(new SpecificationInput(ValueText: " ")).IsValid);
        Assert.False(new DeviceInputValidator().Validate(input with { Aliases = null! }).IsValid);
        Assert.False(new DeviceInputValidator().Validate(input with { Aliases = [null!] }).IsValid);
        Assert.False(new DeviceInputValidator().Validate(input with { ComparisonGroupId = Guid.Empty }).IsValid);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("Text")]
    [InlineData("")]
    [InlineData(null)]
    public void Definition_type_requires_a_supported_name(string? dataType)
    {
        var input = new SpecificationDefinitionInput("Test", "test", Guid.NewGuid(), dataType!);
        Assert.False(new SpecificationDefinitionInputValidator().Validate(input).IsValid);
    }

    private static Device Create() => new("Device", "device", new Brand("Brand", "brand"), new Category("Category", "category", 0));
}
