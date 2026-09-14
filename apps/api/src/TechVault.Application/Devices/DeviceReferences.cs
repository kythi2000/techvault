namespace TechVault.Application.Devices;

public sealed record BrandReference(Guid Id, string Name, string Slug);
public sealed record CategoryReference(Guid Id, string Name, string Slug, Guid? ParentCategoryId, string? ParentSlug);
