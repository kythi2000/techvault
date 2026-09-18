using TechVault.Domain.Common;

namespace TechVault.Domain.Categories;

public sealed class Category : BaseEntity
{
    private Category() { }

    // Parents are assigned at creation only, so domain operations cannot create cycles.
    public Category(string name, string slug, int displayOrder, Category? parent = null,
        string description = "")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        Name = CatalogRules.Required(name, 200, nameof(name));
        Slug = CatalogRules.Slug(slug);
        DisplayOrder = displayOrder;
        Parent = parent;
        ParentCategoryId = parent?.Id;
        Description = description.Trim();
    }

    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string Description { get; private set; } = "";
    public int DisplayOrder { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public Category? Parent { get; private set; }

    public void UpdateDetails(string name, string description, int displayOrder)
    {
        name = CatalogRules.Required(name, 200, nameof(name));
        description = CatalogRules.Text(description, 100_000, nameof(description));
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        Name = name;
        Description = description;
        DisplayOrder = displayOrder;
    }
}
