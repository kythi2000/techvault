using Microsoft.EntityFrameworkCore;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Comparisons;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.Application.Common.Abstractions;

public interface ITechVaultDbContext
{
    DbSet<Brand> Brands { get; }
    DbSet<Category> Categories { get; }
    DbSet<Device> Devices { get; }
    DbSet<ComparisonGroup> ComparisonGroups { get; }
    DbSet<SpecificationGroup> SpecificationGroups { get; }
    DbSet<SpecificationDefinition> SpecificationDefinitions { get; }
    DbSet<DeviceSpecification> DeviceSpecifications { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
