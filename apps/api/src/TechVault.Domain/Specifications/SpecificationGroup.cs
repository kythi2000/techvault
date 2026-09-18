using TechVault.Domain.Common;

namespace TechVault.Domain.Specifications;

public sealed class SpecificationGroup : BaseEntity
{
    private SpecificationGroup() { }

    public SpecificationGroup(string name, string key, int displayOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        Name = CatalogRules.Required(name, 200, nameof(name));
        Key = CatalogRules.Key(key);
        DisplayOrder = displayOrder;
    }

    public string Name { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public int DisplayOrder { get; private set; }

    public void UpdateDetails(string name, int displayOrder)
    {
        name = CatalogRules.Required(name, 200, nameof(name));
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        Name = name;
        DisplayOrder = displayOrder;
    }
}
