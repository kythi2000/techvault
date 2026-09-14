using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechVault.Domain.Specifications;

namespace TechVault.Infrastructure.Persistence.Configurations;

internal sealed class SpecificationDefinitionConfiguration : IEntityTypeConfiguration<SpecificationDefinition>
{
    public void Configure(EntityTypeBuilder<SpecificationDefinition> builder)
    {
        builder.ToTable("SpecificationDefinitions", table =>
        {
            table.HasCheckConstraint("CK_SpecificationDefinitions_Key", "\"Key\" ~ '^[a-z][a-z0-9]*(_[a-z0-9]+)*$'");
            table.HasCheckConstraint("CK_SpecificationDefinitions_Name", "length(btrim(\"Name\")) > 0");
            table.HasCheckConstraint("CK_SpecificationDefinitions_DisplayOrder", "\"DisplayOrder\" >= 0");
            table.HasCheckConstraint("CK_SpecificationDefinitions_DataType", "\"DataType\" IN (1, 2, 3, 4)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(50);
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
