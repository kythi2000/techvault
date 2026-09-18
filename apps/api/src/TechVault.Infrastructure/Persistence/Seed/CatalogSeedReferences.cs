using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.Infrastructure.Persistence.Seed;

// Shared only by the explicit sample seeds: find existing references or stage missing ones for the device's save.
// This is not an upsert/synchronization service; it never changes stored reference metadata.
internal sealed class CatalogSeedReferences(ITechVaultDbContext db, CancellationToken cancellationToken)
{
    public async Task<Brand> BrandAsync(string name, string slug, string description)
    {
        var brand = await db.Brands.SingleOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (brand is not null) return brand;
        brand = new Brand(name, slug, description);
        db.Brands.Add(brand);
        return brand;
    }

    public async Task<Category> CategoryAsync(string name, string slug, int order, string description,
        Category? parent = null)
    {
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (category is null)
        {
            category = new Category(name, slug, order, parent, description);
            db.Categories.Add(category);
        }
        else if (category.ParentCategoryId != parent?.Id)
        {
            throw new InvalidOperationException($"Existing category '{slug}' has an incompatible parent. Review it manually; seed did not overwrite it.");
        }
        return category;
    }

    public async Task<SpecificationGroup> GroupAsync(string name, string key, int order)
    {
        var group = await db.SpecificationGroups.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (group is not null) return group;
        group = new SpecificationGroup(name, key, order);
        db.SpecificationGroups.Add(group);
        return group;
    }

    public async Task SpecificationAsync(Device device, SpecificationGroup group, string name, string key,
        int order, SpecificationValue value, string? unit = null)
    {
        var definition = await db.SpecificationDefinitions.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (definition is null)
        {
            definition = new SpecificationDefinition(name, key, group, value.DataType, order, unit);
            db.SpecificationDefinitions.Add(definition);
        }
        else if (definition.DataType != value.DataType || definition.Unit != unit)
        {
            throw new InvalidOperationException($"Existing specification '{key}' has an incompatible type or unit. Review it manually; seed did not overwrite it.");
        }
        device.SetSpecification(definition, value);
    }
}
