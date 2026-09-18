using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Comparisons;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.Infrastructure.Persistence;

public sealed class TechVaultDbContext(DbContextOptions<TechVaultDbContext> options)
    : DbContext(options), ITechVaultDbContext
{
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ComparisonGroup> ComparisonGroups => Set<ComparisonGroup>();
    public DbSet<SpecificationGroup> SpecificationGroups => Set<SpecificationGroup>();
    public DbSet<SpecificationDefinition> SpecificationDefinitions => Set<SpecificationDefinition>();
    public DbSet<DeviceSpecification> DeviceSpecifications => Set<DeviceSpecification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TechVaultDbContext).Assembly);
}
