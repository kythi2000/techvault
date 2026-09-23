CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE TABLE "Brands" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Slug" character varying(160) NOT NULL,
        "Description" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Brands" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Brands_Name" CHECK (length(btrim("Name")) > 0),
        CONSTRAINT "CK_Brands_Slug" CHECK ("Slug" ~ '^[a-z0-9]+(-[a-z0-9]+)*$')
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE TABLE "Categories" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Slug" character varying(160) NOT NULL,
        "Description" text NOT NULL,
        "DisplayOrder" integer NOT NULL,
        "ParentCategoryId" uuid,
        CONSTRAINT "PK_Categories" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Categories_DisplayOrder" CHECK ("DisplayOrder" >= 0),
        CONSTRAINT "CK_Categories_Name" CHECK (length(btrim("Name")) > 0),
        CONSTRAINT "CK_Categories_Parent" CHECK ("ParentCategoryId" IS NULL OR "ParentCategoryId" <> "Id"),
        CONSTRAINT "CK_Categories_Slug" CHECK ("Slug" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
        CONSTRAINT "FK_Categories_Categories_ParentCategoryId" FOREIGN KEY ("ParentCategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE TABLE "SpecificationGroups" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Key" character varying(100) NOT NULL,
        "DisplayOrder" integer NOT NULL,
        CONSTRAINT "PK_SpecificationGroups" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_SpecificationGroups_DisplayOrder" CHECK ("DisplayOrder" >= 0),
        CONSTRAINT "CK_SpecificationGroups_Key" CHECK ("Key" ~ '^[a-z][a-z0-9]*(_[a-z0-9]+)*$'),
        CONSTRAINT "CK_SpecificationGroups_Name" CHECK (length(btrim("Name")) > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE TABLE "Devices" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Slug" character varying(160) NOT NULL,
        "BrandId" uuid NOT NULL,
        "CategoryId" uuid NOT NULL,
        "ShortDescription" character varying(500) NOT NULL,
        "Description" character varying(100000) NOT NULL,
        "History" character varying(100000) NOT NULL,
        "SeoTitle" character varying(200) NOT NULL,
        "SeoDescription" character varying(500) NOT NULL,
        "ReleaseYear" integer,
        "ReleaseDate" date,
        "DiscontinuedDate" date,
        "HeightMm" numeric,
        "WidthMm" numeric,
        "DepthMm" numeric,
        "WeightGrams" numeric,
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "PublishedAt" timestamp with time zone,
        CONSTRAINT "PK_Devices" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Devices_DiscontinuedDate" CHECK ("DiscontinuedDate" IS NULL OR
    (("ReleaseDate" IS NULL OR "DiscontinuedDate" >= "ReleaseDate")
     AND ("ReleaseYear" IS NULL OR EXTRACT(YEAR FROM "DiscontinuedDate") >= "ReleaseYear"))),
        CONSTRAINT "CK_Devices_Name" CHECK (length(btrim("Name")) > 0),
        CONSTRAINT "CK_Devices_PhysicalMeasurements" CHECK (("HeightMm" IS NULL OR "HeightMm" > 0) AND ("WidthMm" IS NULL OR "WidthMm" > 0)
    AND ("DepthMm" IS NULL OR "DepthMm" > 0) AND ("WeightGrams" IS NULL OR "WeightGrams" > 0)),
        CONSTRAINT "CK_Devices_Publication" CHECK (("Status" <> 1 OR ("PublishedAt" IS NOT NULL
        AND length(btrim("ShortDescription")) > 0 AND length(btrim("Description")) > 0
        AND length(btrim("History")) > 0 AND length(btrim("SeoTitle")) > 0
        AND length(btrim("SeoDescription")) > 0))
    AND ("Status" = 1 OR "PublishedAt" IS NULL)),
        CONSTRAINT "CK_Devices_ReleaseDate" CHECK ("ReleaseDate" IS NULL OR
    ("ReleaseYear" IS NOT NULL AND EXTRACT(YEAR FROM "ReleaseDate") = "ReleaseYear")),
        CONSTRAINT "CK_Devices_ReleaseYear" CHECK ("ReleaseYear" IS NULL OR "ReleaseYear" BETWEEN 1 AND 9999),
        CONSTRAINT "CK_Devices_Slug" CHECK ("Slug" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
        CONSTRAINT "CK_Devices_Status" CHECK ("Status" IN (0, 1, 2)),
        CONSTRAINT "FK_Devices_Brands_BrandId" FOREIGN KEY ("BrandId") REFERENCES "Brands" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Devices_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE TABLE "SpecificationDefinitions" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Key" character varying(100) NOT NULL,
        "GroupId" uuid NOT NULL,
        "DataType" integer NOT NULL,
        "Unit" character varying(50),
        "DisplayOrder" integer NOT NULL,
        CONSTRAINT "PK_SpecificationDefinitions" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_SpecificationDefinitions_Id_DataType" UNIQUE ("Id", "DataType"),
        CONSTRAINT "CK_SpecificationDefinitions_DataType" CHECK ("DataType" IN (1, 2, 3, 4)),
        CONSTRAINT "CK_SpecificationDefinitions_DisplayOrder" CHECK ("DisplayOrder" >= 0),
        CONSTRAINT "CK_SpecificationDefinitions_Key" CHECK ("Key" ~ '^[a-z][a-z0-9]*(_[a-z0-9]+)*$'),
        CONSTRAINT "CK_SpecificationDefinitions_Name" CHECK (length(btrim("Name")) > 0),
        CONSTRAINT "FK_SpecificationDefinitions_SpecificationGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "SpecificationGroups" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE TABLE "DeviceSpecifications" (
        "DeviceId" uuid NOT NULL,
        "DefinitionId" uuid NOT NULL,
        "DataType" integer NOT NULL,
        "ValueText" character varying(10000),
        "ValueNumber" numeric,
        "ValueBoolean" boolean,
        "ValueDate" date,
        CONSTRAINT "PK_DeviceSpecifications" PRIMARY KEY ("DeviceId", "DefinitionId"),
        CONSTRAINT "CK_DeviceSpecifications_TypedValue" CHECK (("DataType" = 1 AND "ValueText" IS NOT NULL AND length(btrim("ValueText")) > 0
        AND "ValueNumber" IS NULL AND "ValueBoolean" IS NULL AND "ValueDate" IS NULL)
    OR ("DataType" = 2 AND "ValueNumber" IS NOT NULL
        AND "ValueText" IS NULL AND "ValueBoolean" IS NULL AND "ValueDate" IS NULL)
    OR ("DataType" = 3 AND "ValueBoolean" IS NOT NULL
        AND "ValueText" IS NULL AND "ValueNumber" IS NULL AND "ValueDate" IS NULL)
    OR ("DataType" = 4 AND "ValueDate" IS NOT NULL
        AND "ValueText" IS NULL AND "ValueNumber" IS NULL AND "ValueBoolean" IS NULL)),
        CONSTRAINT "FK_DeviceSpecifications_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_DeviceSpecifications_SpecificationDefinitions_DefinitionId_~" FOREIGN KEY ("DefinitionId", "DataType") REFERENCES "SpecificationDefinitions" ("Id", "DataType") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE UNIQUE INDEX "IX_Brands_Slug" ON "Brands" ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE INDEX "IX_Categories_ParentCategoryId" ON "Categories" ("ParentCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE UNIQUE INDEX "IX_Categories_Slug" ON "Categories" ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE INDEX "IX_Devices_BrandId" ON "Devices" ("BrandId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE INDEX "IX_Devices_CategoryId" ON "Devices" ("CategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE UNIQUE INDEX "IX_Devices_Slug" ON "Devices" ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE INDEX "IX_DeviceSpecifications_DefinitionId_DataType" ON "DeviceSpecifications" ("DefinitionId", "DataType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE INDEX "IX_SpecificationDefinitions_GroupId" ON "SpecificationDefinitions" ("GroupId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE UNIQUE INDEX "IX_SpecificationDefinitions_Key" ON "SpecificationDefinitions" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    CREATE UNIQUE INDEX "IX_SpecificationGroups_Key" ON "SpecificationGroups" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914062318_InitialCatalog') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260914062318_InitialCatalog', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917105840_AddCatalogDiscovery') THEN
    ALTER TABLE "Devices" ADD "Aliases" text[] NOT NULL DEFAULT ('{}'::text[]);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917105840_AddCatalogDiscovery') THEN
    ALTER TABLE "Devices" ADD "ModelNumber" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917105840_AddCatalogDiscovery') THEN
    ALTER TABLE "Devices" ADD "SearchVector" tsvector NOT NULL DEFAULT (''::tsvector);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917105840_AddCatalogDiscovery') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917105840_AddCatalogDiscovery') THEN
    CREATE INDEX "IX_Devices_SearchVector" ON "Devices" USING GIN ("SearchVector");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917105840_AddCatalogDiscovery') THEN
    CREATE INDEX "IX_Devices_Timeline" ON "Devices" ("ReleaseYear", "ReleaseDate", "Slug") WHERE "Status" = 1 AND "ReleaseYear" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917105840_AddCatalogDiscovery') THEN
    ALTER TABLE "Devices" ADD CONSTRAINT "CK_Devices_Aliases" CHECK (cardinality("Aliases") <= 20 AND array_position("Aliases", NULL) IS NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917105840_AddCatalogDiscovery') THEN
    ALTER TABLE "Devices" ADD CONSTRAINT "CK_Devices_ModelNumber" CHECK ("ModelNumber" IS NULL OR length(btrim("ModelNumber")) > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917105840_AddCatalogDiscovery') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260917105840_AddCatalogDiscovery', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918092718_AddCatalogComparisons') THEN
    ALTER TABLE "SpecificationDefinitions" ADD "IsComparable" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918092718_AddCatalogComparisons') THEN
    ALTER TABLE "Devices" ADD "ComparisonGroupId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918092718_AddCatalogComparisons') THEN
    CREATE TABLE "ComparisonGroups" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Key" character varying(100) NOT NULL,
        CONSTRAINT "PK_ComparisonGroups" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_ComparisonGroups_Key" CHECK ("Key" ~ '^[a-z][a-z0-9]*(_[a-z0-9]+)*$'),
        CONSTRAINT "CK_ComparisonGroups_Name" CHECK (length(btrim("Name")) > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918092718_AddCatalogComparisons') THEN
    CREATE INDEX "IX_Devices_ComparisonGroupId" ON "Devices" ("ComparisonGroupId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918092718_AddCatalogComparisons') THEN
    CREATE UNIQUE INDEX "IX_ComparisonGroups_Key" ON "ComparisonGroups" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918092718_AddCatalogComparisons') THEN
    ALTER TABLE "Devices" ADD CONSTRAINT "FK_Devices_ComparisonGroups_ComparisonGroupId" FOREIGN KEY ("ComparisonGroupId") REFERENCES "ComparisonGroups" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918092718_AddCatalogComparisons') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918092718_AddCatalogComparisons') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260918092718_AddCatalogComparisons', '10.0.11');
    END IF;
END $EF$;
COMMIT;

