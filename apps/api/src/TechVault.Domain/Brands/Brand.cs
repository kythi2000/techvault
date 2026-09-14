using TechVault.Domain.Common;

namespace TechVault.Domain.Brands;

public sealed class Brand : BaseEntity
{
    private Brand() { }

    public Brand(string name, string slug, string description = "")
    {
        Name = CatalogRules.Required(name, 200, nameof(name));
        Slug = CatalogRules.Slug(slug);
        Description = description.Trim();
    }

    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string Description { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
