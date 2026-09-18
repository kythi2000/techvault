using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace TechVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Aliases",
                table: "Devices",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<string>(
                name: "ModelNumber",
                table: "Devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Devices",
                type: "tsvector",
                nullable: false,
                defaultValueSql: "''::tsvector");

            // A generated column cannot read the related Brand row. Triggers keep the one indexed
            // document current for both EF and direct SQL writes, without a background sync job.
            migrationBuilder.Sql("""
                CREATE FUNCTION catalog_search_document(device_name text, brand_name text,
                    summary text, description text, aliases text[], model_number text)
                RETURNS tsvector LANGUAGE sql IMMUTABLE PARALLEL SAFE AS $function$
                    SELECT setweight(to_tsvector('simple'::regconfig, coalesce(device_name, '') || ' ' ||
                               coalesce(array_to_string(aliases, ' '), '') || ' ' || coalesce(model_number, '')), 'A')
                        || setweight(to_tsvector('simple'::regconfig, coalesce(brand_name, '')), 'B')
                        || setweight(to_tsvector('simple'::regconfig, coalesce(summary, '') || ' ' ||
                               coalesce(description, '')), 'C');
                $function$;

                CREATE FUNCTION catalog_device_search_refresh() RETURNS trigger LANGUAGE plpgsql AS $function$
                DECLARE brand_name text;
                BEGIN
                    -- Serialize with brand renames so a concurrent insert cannot keep an old brand name.
                    SELECT "Name" INTO brand_name FROM "Brands" WHERE "Id" = NEW."BrandId" FOR SHARE;
                    NEW."SearchVector" := catalog_search_document(NEW."Name", brand_name,
                        NEW."ShortDescription", NEW."Description", NEW."Aliases", NEW."ModelNumber");
                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER catalog_device_search_refresh
                    BEFORE INSERT OR UPDATE OF "Name", "BrandId", "ShortDescription", "Description", "Aliases", "ModelNumber"
                    ON "Devices" FOR EACH ROW EXECUTE FUNCTION catalog_device_search_refresh();

                CREATE FUNCTION catalog_brand_search_refresh() RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    UPDATE "Devices" AS device
                    SET "SearchVector" = catalog_search_document(device."Name", NEW."Name",
                        device."ShortDescription", device."Description", device."Aliases", device."ModelNumber")
                    WHERE device."BrandId" = NEW."Id";
                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER catalog_brand_search_refresh AFTER UPDATE OF "Name" ON "Brands"
                    FOR EACH ROW WHEN (OLD."Name" IS DISTINCT FROM NEW."Name")
                    EXECUTE FUNCTION catalog_brand_search_refresh();

                -- Backfill existing records without changing editorial fields, status, or timestamps.
                UPDATE "Devices" AS device
                SET "SearchVector" = catalog_search_document(device."Name", brand."Name",
                    device."ShortDescription", device."Description", device."Aliases", device."ModelNumber")
                FROM "Brands" AS brand WHERE brand."Id" = device."BrandId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SearchVector",
                table: "Devices",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Timeline",
                table: "Devices",
                columns: new[] { "ReleaseYear", "ReleaseDate", "Slug" },
                filter: "\"Status\" = 1 AND \"ReleaseYear\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Devices_Aliases",
                table: "Devices",
                sql: "cardinality(\"Aliases\") <= 20 AND array_position(\"Aliases\", NULL) IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Devices_ModelNumber",
                table: "Devices",
                sql: "\"ModelNumber\" IS NULL OR length(btrim(\"ModelNumber\")) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER catalog_brand_search_refresh ON "Brands";
                DROP TRIGGER catalog_device_search_refresh ON "Devices";
                DROP FUNCTION catalog_brand_search_refresh();
                DROP FUNCTION catalog_device_search_refresh();
                DROP FUNCTION catalog_search_document(text, text, text, text, text[], text);
                """);

            migrationBuilder.DropIndex(
                name: "IX_Devices_SearchVector",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_Timeline",
                table: "Devices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Devices_Aliases",
                table: "Devices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Devices_ModelNumber",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Aliases",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ModelNumber",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Devices");
        }
    }
}
