using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                    table.CheckConstraint("CK_Brands_Name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_Brands_Slug", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Categories_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_Categories_Name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_Categories_Parent", "\"ParentCategoryId\" IS NULL OR \"ParentCategoryId\" <> \"Id\"");
                    table.CheckConstraint("CK_Categories_Slug", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpecificationGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecificationGroups", x => x.Id);
                    table.CheckConstraint("CK_SpecificationGroups_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_SpecificationGroups_Key", "\"Key\" ~ '^[a-z][a-z0-9]*(_[a-z0-9]+)*$'");
                    table.CheckConstraint("CK_SpecificationGroups_Name", "length(btrim(\"Name\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    History = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    SeoTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SeoDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReleaseYear = table.Column<int>(type: "integer", nullable: true),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DiscontinuedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HeightMm = table.Column<decimal>(type: "numeric", nullable: true),
                    WidthMm = table.Column<decimal>(type: "numeric", nullable: true),
                    DepthMm = table.Column<decimal>(type: "numeric", nullable: true),
                    WeightGrams = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.CheckConstraint("CK_Devices_DiscontinuedDate", "\"DiscontinuedDate\" IS NULL OR\n((\"ReleaseDate\" IS NULL OR \"DiscontinuedDate\" >= \"ReleaseDate\")\n AND (\"ReleaseYear\" IS NULL OR EXTRACT(YEAR FROM \"DiscontinuedDate\") >= \"ReleaseYear\"))");
                    table.CheckConstraint("CK_Devices_Name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_Devices_PhysicalMeasurements", "(\"HeightMm\" IS NULL OR \"HeightMm\" > 0) AND (\"WidthMm\" IS NULL OR \"WidthMm\" > 0)\nAND (\"DepthMm\" IS NULL OR \"DepthMm\" > 0) AND (\"WeightGrams\" IS NULL OR \"WeightGrams\" > 0)");
                    table.CheckConstraint("CK_Devices_Publication", "(\"Status\" <> 1 OR (\"PublishedAt\" IS NOT NULL\n    AND length(btrim(\"ShortDescription\")) > 0 AND length(btrim(\"Description\")) > 0\n    AND length(btrim(\"History\")) > 0 AND length(btrim(\"SeoTitle\")) > 0\n    AND length(btrim(\"SeoDescription\")) > 0))\nAND (\"Status\" = 1 OR \"PublishedAt\" IS NULL)");
                    table.CheckConstraint("CK_Devices_ReleaseDate", "\"ReleaseDate\" IS NULL OR\n(\"ReleaseYear\" IS NOT NULL AND EXTRACT(YEAR FROM \"ReleaseDate\") = \"ReleaseYear\")");
                    table.CheckConstraint("CK_Devices_ReleaseYear", "\"ReleaseYear\" IS NULL OR \"ReleaseYear\" BETWEEN 1 AND 9999");
                    table.CheckConstraint("CK_Devices_Slug", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.CheckConstraint("CK_Devices_Status", "\"Status\" IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_Devices_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Devices_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpecificationDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataType = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecificationDefinitions", x => x.Id);
                    table.UniqueConstraint("AK_SpecificationDefinitions_Id_DataType", x => new { x.Id, x.DataType });
                    table.CheckConstraint("CK_SpecificationDefinitions_DataType", "\"DataType\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_SpecificationDefinitions_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_SpecificationDefinitions_Key", "\"Key\" ~ '^[a-z][a-z0-9]*(_[a-z0-9]+)*$'");
                    table.CheckConstraint("CK_SpecificationDefinitions_Name", "length(btrim(\"Name\")) > 0");
                    table.ForeignKey(
                        name: "FK_SpecificationDefinitions_SpecificationGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SpecificationGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceSpecifications",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataType = table.Column<int>(type: "integer", nullable: false),
                    ValueText = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    ValueNumber = table.Column<decimal>(type: "numeric", nullable: true),
                    ValueBoolean = table.Column<bool>(type: "boolean", nullable: true),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSpecifications", x => new { x.DeviceId, x.DefinitionId });
                    table.CheckConstraint("CK_DeviceSpecifications_TypedValue", "(\"DataType\" = 1 AND \"ValueText\" IS NOT NULL AND length(btrim(\"ValueText\")) > 0\n    AND \"ValueNumber\" IS NULL AND \"ValueBoolean\" IS NULL AND \"ValueDate\" IS NULL)\nOR (\"DataType\" = 2 AND \"ValueNumber\" IS NOT NULL\n    AND \"ValueText\" IS NULL AND \"ValueBoolean\" IS NULL AND \"ValueDate\" IS NULL)\nOR (\"DataType\" = 3 AND \"ValueBoolean\" IS NOT NULL\n    AND \"ValueText\" IS NULL AND \"ValueNumber\" IS NULL AND \"ValueDate\" IS NULL)\nOR (\"DataType\" = 4 AND \"ValueDate\" IS NOT NULL\n    AND \"ValueText\" IS NULL AND \"ValueNumber\" IS NULL AND \"ValueBoolean\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_DeviceSpecifications_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceSpecifications_SpecificationDefinitions_DefinitionId_~",
                        columns: x => new { x.DefinitionId, x.DataType },
                        principalTable: "SpecificationDefinitions",
                        principalColumns: new[] { "Id", "DataType" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Slug",
                table: "Brands",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId",
                table: "Categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_BrandId",
                table: "Devices",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CategoryId",
                table: "Devices",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Slug",
                table: "Devices",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSpecifications_DefinitionId_DataType",
                table: "DeviceSpecifications",
                columns: new[] { "DefinitionId", "DataType" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationDefinitions_GroupId",
                table: "SpecificationDefinitions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationDefinitions_Key",
                table: "SpecificationDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationGroups_Key",
                table: "SpecificationGroups",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceSpecifications");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "SpecificationDefinitions");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "SpecificationGroups");
        }
    }
}
