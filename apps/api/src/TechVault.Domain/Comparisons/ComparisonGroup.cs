using TechVault.Domain.Common;

namespace TechVault.Domain.Comparisons;

// Explicit compatibility, independent of navigational category names or device brands.
public sealed class ComparisonGroup : BaseEntity
{
    private ComparisonGroup() { }

    public ComparisonGroup(string name, string key)
    {
        Name = CatalogRules.Required(name, 200, nameof(name));
        Key = CatalogRules.Key(key);
    }

    public string Name { get; private set; } = null!;
    public string Key { get; private set; } = null!;
}
