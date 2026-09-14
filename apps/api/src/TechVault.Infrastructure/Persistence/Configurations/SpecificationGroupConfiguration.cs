using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechVault.Domain.Specifications;

namespace TechVault.Infrastructure.Persistence.Configurations;

internal sealed class SpecificationGroupConfiguration : IEntityTypeConfiguration<SpecificationGroup>
{
    public void Configure(EntityTypeBuilder<SpecificationGroup> builder)
    {
        builder.ToTable("SpecificationGroups", table =>
        {
            table.HasCheckConstraint("CK_SpecificationGroups_Key", "\"Key\" ~ '^[a-z][a-z0-9]*(_[a-z0-9]+)*$'");
            table.HasCheckConstraint("CK_SpecificationGroups_Name", "length(btrim(\"Name\")) > 0");
            table.HasCheckConstraint("CK_SpecificationGroups_DisplayOrder", "\"DisplayOrder\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
    }
}
