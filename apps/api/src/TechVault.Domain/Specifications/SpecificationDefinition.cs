using TechVault.Domain.Common;

namespace TechVault.Domain.Specifications;

public sealed class SpecificationDefinition : BaseEntity
{
    private SpecificationDefinition() { }

    public SpecificationDefinition(string name, string key, SpecificationGroup group,
        SpecificationDataType dataType, int displayOrder, string? unit = null)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        if (!Enum.IsDefined(dataType))
            throw new ArgumentOutOfRangeException(nameof(dataType));

        Name = CatalogRules.Required(name, 200, nameof(name));
        Key = CatalogRules.Key(key);
        Group = group;
        GroupId = group.Id;
        DataType = dataType;
        DisplayOrder = displayOrder;
        Unit = unit is null ? null : CatalogRules.Required(unit, 50, nameof(unit));
    }

    public string Name { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public Guid GroupId { get; private set; }
    public SpecificationGroup Group { get; private set; } = null!;
    public SpecificationDataType DataType { get; private set; }
    public string? Unit { get; private set; }
    public int DisplayOrder { get; private set; }
}
