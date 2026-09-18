using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechVault.Domain.Comparisons;

namespace TechVault.Infrastructure.Persistence.Configurations;

internal sealed class ComparisonGroupConfiguration : IEntityTypeConfiguration<ComparisonGroup>
{
    public void Configure(EntityTypeBuilder<ComparisonGroup> builder)
    {
        builder.ToTable("ComparisonGroups", table =>
        {
            table.HasCheckConstraint("CK_ComparisonGroups_Key", "\"Key\" ~ '^[a-z][a-z0-9]*(_[a-z0-9]+)*$'");
            table.HasCheckConstraint("CK_ComparisonGroups_Name", "length(btrim(\"Name\")) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
    }
}
