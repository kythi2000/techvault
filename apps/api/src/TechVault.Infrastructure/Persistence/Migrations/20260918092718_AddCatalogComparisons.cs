using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogComparisons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsComparable",
                table: "SpecificationDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ComparisonGroupId",
                table: "Devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComparisonGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparisonGroups", x => x.Id);
                    table.CheckConstraint("CK_ComparisonGroups_Key", "\"Key\" ~ '^[a-z][a-z0-9]*(_[a-z0-9]+)*$'");
                    table.CheckConstraint("CK_ComparisonGroups_Name", "length(btrim(\"Name\")) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ComparisonGroupId",
                table: "Devices",
                column: "ComparisonGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ComparisonGroups_Key",
                table: "ComparisonGroups",
                column: "Key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_ComparisonGroups_ComparisonGroupId",
                table: "Devices",
                column: "ComparisonGroupId",
                principalTable: "ComparisonGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // One-time initialization of the NEW metadata, not a recurring seed synchronization.
            // Existing editorial fields, values, timestamps, and publication states stay untouched.
            migrationBuilder.Sql("""
                INSERT INTO "ComparisonGroups" ("Id", "Name", "Key") VALUES
                    (gen_random_uuid(), 'Phones', 'phone'),
                    (gen_random_uuid(), 'All-in-One Computers', 'all_in_one');

                UPDATE "Devices" AS device SET "ComparisonGroupId" = comparison."Id"
                FROM "Categories" AS category
                JOIN "Categories" AS parent ON parent."Id" = category."ParentCategoryId",
                    "Brands" AS brand, "ComparisonGroups" AS comparison
                WHERE device."CategoryId" = category."Id" AND device."BrandId" = brand."Id"
                    AND parent."ParentCategoryId" IS NULL
                    AND (
                        (device."Slug" IN ('nokia-3310', 'nokia-3210') AND brand."Slug" = 'nokia'
                         AND category."Slug" = 'feature-phones' AND parent."Slug" = 'phones'
                         AND comparison."Key" = 'phone')
                        OR
                        (device."Slug" IN ('macintosh-128k', 'imac-g3') AND brand."Slug" = 'apple'
                         AND category."Slug" = 'all-in-one-computers' AND parent."Slug" = 'computers'
                         AND comparison."Key" = 'all_in_one')
                    );

                UPDATE "SpecificationDefinitions" SET "IsComparable" = TRUE WHERE "Key" IN (
                    'replaceable_covers', 'antenna', 'battery_chemistry', 'talk_time_max', 'standby_time_max',
                    'network_bands', 'sms_chat', 'sms_segments_max', 'predictive_text', 'picture_messages',
                    'ringtone_composer', 'sms_character_limit', 'built_in_games', 'cpu_model', 'cpu_clock',
                    'ram_capacity', 'ram_slots', 'floppy_drive', 'floppy_format', 'internal_hard_disk',
                    'display_resolution', 'display_color', 'ethernet', 'storage_capacity', 'optical_drive',
                    'ethernet_link', 'usb_ports', 'operating_system');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_ComparisonGroups_ComparisonGroupId",
                table: "Devices");

            migrationBuilder.DropTable(
                name: "ComparisonGroups");

            migrationBuilder.DropIndex(
                name: "IX_Devices_ComparisonGroupId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsComparable",
                table: "SpecificationDefinitions");

            migrationBuilder.DropColumn(
                name: "ComparisonGroupId",
                table: "Devices");
        }
    }
}
