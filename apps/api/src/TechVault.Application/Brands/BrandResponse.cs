namespace TechVault.Application.Brands;

public sealed record BrandResponse(Guid Id, string Name, string Slug, string Description,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int PublishedDeviceCount);
