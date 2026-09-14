using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Common;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.UnitTests;

public sealed class EntityIdentityTests
{
    [Fact]
    public void Guid_entities_inherit_unique_nonempty_identifiers_from_BaseEntity()
    {
        var brand = new Brand("Nokia", "nokia");
        var category = new Category("Phones", "phones", 10);
        var group = new SpecificationGroup("General", "general", 10);
        BaseEntity[] entities =
        [
            brand, category, group,
            new Device("Nokia 3310", "nokia-3310", brand, category),
            new SpecificationDefinition("SMS chat", "sms_chat", group, SpecificationDataType.Boolean, 10)
        ];

        Assert.All(entities, entity =>
        {
            Assert.NotEqual(Guid.Empty, entity.Id);
            Assert.Equal(typeof(BaseEntity), entity.GetType().GetProperty(nameof(BaseEntity.Id))!.DeclaringType);
        });
        Assert.Equal(entities.Length, entities.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void Editing_and_publishing_preserve_identity_and_specification_ownership()
    {
        var device = new Device("Nokia 3310", "nokia-3310",
            new Brand("Nokia", "nokia"), new Category("Phones", "phones", 10));
        var definition = new SpecificationDefinition("SMS chat", "sms_chat",
            new SpecificationGroup("General", "general", 10), SpecificationDataType.Boolean, 10);
        var originalId = device.Id;

        device.UpdateContent("Summary", "Overview", "History", "SEO title", "SEO description");
        device.SetRelease(2000);
        device.SetPhysicalDetails(null, null, null, 133);
        device.SetSpecification(definition, SpecificationValue.Boolean(true));
        device.Publish();

        Assert.Equal(originalId, device.Id);
        var specification = Assert.Single(device.Specifications);
        Assert.Equal(originalId, specification.DeviceId);
        Assert.Equal(definition.Id, specification.DefinitionId);
    }

    [Fact]
    public void DeviceSpecification_keeps_its_composite_identity_without_a_surrogate_Id()
    {
        Assert.False(typeof(BaseEntity).IsAssignableFrom(typeof(DeviceSpecification)));
        Assert.Null(typeof(DeviceSpecification).GetProperty(nameof(BaseEntity.Id)));
    }
}
