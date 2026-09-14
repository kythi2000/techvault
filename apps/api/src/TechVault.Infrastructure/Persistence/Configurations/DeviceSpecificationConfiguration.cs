using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechVault.Domain.Specifications;

namespace TechVault.Infrastructure.Persistence.Configurations;

internal sealed class DeviceSpecificationConfiguration : IEntityTypeConfiguration<DeviceSpecification>
{
    public void Configure(EntityTypeBuilder<DeviceSpecification> builder)
    {
        builder.ToTable("DeviceSpecifications", table =>
            table.HasCheckConstraint("CK_DeviceSpecifications_TypedValue", """
                ("DataType" = 1 AND "ValueText" IS NOT NULL AND length(btrim("ValueText")) > 0
                    AND "ValueNumber" IS NULL AND "ValueBoolean" IS NULL AND "ValueDate" IS NULL)
                OR ("DataType" = 2 AND "ValueNumber" IS NOT NULL
                    AND "ValueText" IS NULL AND "ValueBoolean" IS NULL AND "ValueDate" IS NULL)
                OR ("DataType" = 3 AND "ValueBoolean" IS NOT NULL
                    AND "ValueText" IS NULL AND "ValueNumber" IS NULL AND "ValueDate" IS NULL)
                OR ("DataType" = 4 AND "ValueDate" IS NOT NULL
                    AND "ValueText" IS NULL AND "ValueNumber" IS NULL AND "ValueBoolean" IS NULL)
                """));
        builder.HasKey(x => new { x.DeviceId, x.DefinitionId });
        builder.Property(x => x.ValueText).HasMaxLength(10_000);

        // The discriminator participates in the FK so SQL cannot assign a value of a different type.
        builder.HasOne(x => x.Definition).WithMany()
            .HasForeignKey(x => new { x.DefinitionId, x.DataType })
            .HasPrincipalKey(x => new { x.Id, x.DataType })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
