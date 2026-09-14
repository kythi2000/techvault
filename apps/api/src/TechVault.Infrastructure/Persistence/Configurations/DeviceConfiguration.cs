using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechVault.Domain.Devices;

namespace TechVault.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices", table =>
        {
            table.HasCheckConstraint("CK_Devices_Slug", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
            table.HasCheckConstraint("CK_Devices_Name", "length(btrim(\"Name\")) > 0");
            table.HasCheckConstraint("CK_Devices_Status", "\"Status\" IN (0, 1, 2)");
            table.HasCheckConstraint("CK_Devices_Publication", """
                ("Status" <> 1 OR ("PublishedAt" IS NOT NULL
                    AND length(btrim("ShortDescription")) > 0 AND length(btrim("Description")) > 0
                    AND length(btrim("History")) > 0 AND length(btrim("SeoTitle")) > 0
                    AND length(btrim("SeoDescription")) > 0))
                AND ("Status" = 1 OR "PublishedAt" IS NULL)
                """);
            table.HasCheckConstraint("CK_Devices_ReleaseYear", "\"ReleaseYear\" IS NULL OR \"ReleaseYear\" BETWEEN 1 AND 9999");
            table.HasCheckConstraint("CK_Devices_ReleaseDate", """
                "ReleaseDate" IS NULL OR
                ("ReleaseYear" IS NOT NULL AND EXTRACT(YEAR FROM "ReleaseDate") = "ReleaseYear")
                """);
            table.HasCheckConstraint("CK_Devices_DiscontinuedDate", """
                "DiscontinuedDate" IS NULL OR
                (("ReleaseDate" IS NULL OR "DiscontinuedDate" >= "ReleaseDate")
                 AND ("ReleaseYear" IS NULL OR EXTRACT(YEAR FROM "DiscontinuedDate") >= "ReleaseYear"))
                """);
            table.HasCheckConstraint("CK_Devices_PhysicalMeasurements", """
                ("HeightMm" IS NULL OR "HeightMm" > 0) AND ("WidthMm" IS NULL OR "WidthMm" > 0)
                AND ("DepthMm" IS NULL OR "DepthMm" > 0) AND ("WeightGrams" IS NULL OR "WeightGrams" > 0)
                """);
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        builder.Property(x => x.ShortDescription).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(100_000).IsRequired();
        builder.Property(x => x.History).HasMaxLength(100_000).IsRequired();
        builder.Property(x => x.SeoTitle).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SeoDescription).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Specifications).WithOne().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Specifications).HasField("_specifications").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
