# TechVault

A digital museum and phone/computer catalog. The .NET backend currently covers Phases 1–9 in [the backend roadmap](docs/BACKEND_ROADMAP.md), including a four-device phone/computer seed, public catalog APIs, PostgreSQL full-text search, a chronological timeline, two-device comparisons, protected content management, and production-readiness controls. The Next.js app covers public Frontend Phases 1–6 in [the frontend specification](docs/FRONTEND_SPEC.md): the visual foundation, browse/detail/taxonomy routes, search, a responsive timeline, and structured comparison.

## Frontend

The public web app lives in `apps/web` and consumes the API from Server Components. Start the migrated/seeded API as described below, then in another terminal run:

```powershell
cd apps/web
npm install
npm run dev
```

Open `http://localhost:3000`. `TECHVAULT_API_URL` defaults to `http://localhost:5078`; see [the web README](apps/web/README.md) for configuration and quality checks. When the catalog API is unavailable, the public shell still renders and data surfaces show an explicit unavailable state.

With the current migrations applied and the API running, open `/search?q=Nokia`, `/timeline?era=1990s`, or `/compare?devices=nokia-3310,nokia-3210`. Discovery and comparison keep their state in shareable URLs.

## Local build and run

Install the .NET 10 SDK and start Docker Desktop with Linux containers (or a local Docker engine). Run commands from the repository root (`techvault/`).

Copy `.env.example` to an untracked `.env` file and set `POSTGRES_PASSWORD` to your own local password. The other Compose defaults are database/user `techvault` and host port `5432`. PostgreSQL is bound to loopback only and stores its data in the `postgres_data` named volume.

If you already run PostgreSQL locally, you can use a dedicated TechVault database on that server instead of Compose. Do not start a second PostgreSQL server on the same host port; choose another `POSTGRES_PORT` if using both. `POSTGRES_*` configures the Compose container, not an existing local server.

```sh
docker compose config --quiet
docker compose up -d postgres --wait
```

`--quiet` validates configuration without printing the resolved password. Set `DATABASE_URL` in the terminal that will run the API and EF commands. For example, in PowerShell, replace the placeholder with the same local password:

```powershell
$env:DATABASE_URL = 'Host=localhost;Port=5432;Database=techvault;Username=techvault;Password=<same-local-password>;Timeout=5;Command Timeout=5'
```

Despite its name, `DATABASE_URL` uses an Npgsql key/value connection string, not a `postgres://` URI. Match any database/user/port changes in `.env`; quote connection-string values containing semicolons. Docker Compose reads `.env`, but `dotnet run` and EF tools do not load it automatically. Do not commit credentials or put them in `appsettings.json`.

```sh
dotnet sln apps/api/TechVault.slnx list
dotnet build apps/api/TechVault.slnx
dotnet run --project apps/api/src/TechVault.Api --no-build --launch-profile http
```

The build restores dependencies automatically. The `http` launch profile starts the API in Development at `http://localhost:5078`.

- `GET http://localhost:5078/health/live` returns HTTP 200 with `{"status":"Healthy"}`. This endpoint checks process liveness only.
- `GET http://localhost:5078/health/ready` returns HTTP 200 with `Healthy` when PostgreSQL is reachable, or HTTP 503 with `Unhealthy` when it is unavailable. Database details and credentials are not included in the response.
- `GET http://localhost:5078/openapi/v1.json` serves the OpenAPI document in Development only.

Use [TechVault.Api.http](apps/api/src/TechVault.Api/TechVault.Api.http) to send these requests from an HTTP-file client. Stop the API with `Ctrl+C`.

The API does not connect to PostgreSQL until a database operation or readiness check is requested. Liveness works even if the database is stopped. Production HTTPS is scheduled in a later phase.

## Tests

Domain and Application contract/validation tests do not require Docker:

```sh
dotnet test apps/api/tests/TechVault.UnitTests/TechVault.UnitTests.csproj
```

With Docker running, execute the complete suite:

```sh
dotnet test apps/api/TechVault.slnx
```

Integration tests use Testcontainers to create disposable PostgreSQL containers with random ports and credentials; they do not use the Compose database or your `DATABASE_URL`. They verify connectivity/outages, no automatic schema creation, migration up/down, catalog round-trips, constraints, preservation of editorial changes across repeated seeds, and the public API contracts. API tests cover combined filters, deterministic pagination, typed specifications, unpublished visibility, and JSON errors in Production. Mixed-catalog tests exercise the real four-device seed, upgrades from the Nokia-only dataset, shared phone/computer reads, and non-overwriting reseeds. Additional synthetic records exist only in test fixtures, not in the sample seed. The first run downloads the container images. If a Debug API process locks build output on Windows, stop it or add `-c Release` to the build/test commands.

To verify the running API manually, request both health endpoints, run `docker compose stop postgres`, and request them again: readiness should be 503 and liveness should stay 200. Run `docker compose up -d postgres --wait` to restore PostgreSQL and verify readiness returns to 200. `docker compose down` stops/removes the development container but preserves the named volume; avoid `down -v` unless you intend to delete the local database.

## EF tooling

The repository pins a local `dotnet-ef` tool. After setting `DATABASE_URL`, verify design-time context creation through the API's normal service registration:

```sh
dotnet tool restore
dotnet ef dbcontext info --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
```

The context and migrations belong to Infrastructure; the API is the startup project. No separate design-time factory is needed. Normal API startup does not call `Migrate`, `EnsureCreated`, or the seed.

## Database migration and explicit catalog seed

First set `DATABASE_URL` as described above. Verify its host, port, and database before proceeding: the following commands modify that database. Use a dedicated TechVault database, not the default `postgres` maintenance database or an unrelated application database.

```sh
dotnet ef migrations script 0 --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
dotnet ef database update --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
dotnet run --project apps/api/src/TechVault.Api --launch-profile http -- --seed-catalog
```

Review the SQL produced by the first command before applying it. The seed command exits when finished; it does not start the HTTP server or apply migrations. Run it after a successful database update. To start the API afterward, run the usual `dotnet run` command without `--seed-catalog`.

On a fresh database, the first migration may log a failed query against the not-yet-created `__EFMigrationsHistory` table. [Npgsql handles that missing-table case](https://github.com/npgsql/efcore.pg/blob/v10.0.2/src/EFCore.PG/Migrations/Internal/NpgsqlHistoryRepository.cs) and continues creating the schema. Confirm that the command finishes successfully with exit code 0; do not disregard other database errors.

The initial migration creates `Brands`, `Categories`, `Devices`, `SpecificationGroups`, `SpecificationDefinitions`, and `DeviceSpecifications`, plus EF migration history. It contains no seed content. Slugs/keys and device-definition pairs are unique; foreign keys and check constraints enforce references, publication state, dates, positive physical measurements, and typed specification values. Phase 6's `AddCatalogDiscovery` migration adds alias/model storage, a maintained search vector and GIN index, and a timeline index. Apply all migrations before running the current API or seed.

Seed behavior:

- The command now checks `nokia-3310`, `nokia-3210`, `macintosh-128k`, and `imac-g3` independently. If a device exists, skip it entirely: no content, classification, status, timestamp, or specification changes, including for drafts or partially populated records.
- For each missing device, reuse reference data by slug/key, insert missing references, and insert the device with its specifications in one `SaveChangesAsync` transaction.
- Existing reference labels/order/content are preserved. An incompatible definition type/unit or category parent causes an explicit error instead of an overwrite.
- Devices are committed one at a time, not in one catalog-wide transaction. If a later device fails, earlier inserts remain; correct the conflicting reference deliberately and rerun the command in a fresh process to add only the remaining devices.
- Run one seed process at a time. There is no synchronization/upsert engine or concurrent-seed retry mechanism.

On a fresh database, the sample contains two Nokia feature phones and two Apple all-in-one computers. Existing Nokia 3310 seed content is unchanged. See [seed data and historical sources](docs/catalog/SEED_DATA.md) for model variants, exact/unknown dates, shared specification units, and source caveats. No image assets are included.

Model conventions:

- Domain has no EF Core/ASP.NET dependency. Infrastructure implements Application's `ITechVaultDbContext` using the existing scoped EF context.
- `Common/BaseEntity` shares only the application-generated `Guid Id` for Brand, Category, Device, SpecificationGroup, SpecificationDefinition, and ComparisonGroup. IDs have no public setter; existing audit fields remain on Brand/Device. DeviceSpecification retains its `(DeviceId, DefinitionId)` composite key and does not inherit BaseEntity.
- BaseEntity is a small, non-generic CLR base class, not an EF entity/table or a persistence inheritance hierarchy. This refactor does not change the schema or require a migration; do not add a `DbSet<BaseEntity>`, generic repository, automatic auditing, soft-delete flags, or domain events to it.
- Specification definitions are shared by key; only values assigned to a device are stored. No separate phone/computer tables exist. Phase 7 adds explicit comparison groups and opt-in `IsComparable` definition metadata.
- A missing specification means unknown; `false` and `0` are real values. Each stored value has exactly one typed column, and its type must match the definition.
- Read specifications in group display order, then definition display order, then key for stable ties. Load a device's specifications before calling `SetSpecification` to edit it.
- Category parents are assigned at creation and cannot be changed through the admin API. Protected editorial endpoints are described in Phase 8 below.

Check that mappings and the migration snapshot remain aligned:

```sh
dotnet ef migrations has-pending-model-changes --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
```

## Phase 4: public catalog APIs

After explicitly migrating/seeding and starting the API, try:

```sh
curl -i http://localhost:5078/api/v1/devices/nokia-3310
curl -i http://localhost:5078/api/v1/devices/nokia-3310/specifications
curl -i "http://localhost:5078/api/v1/phones?brand=nokia&decade=2000&page=1&pageSize=24"
curl -i http://localhost:5078/api/v1/computers
curl -i http://localhost:5078/api/v1/brands/nokia
curl -i http://localhost:5078/api/v1/categories
```

On Windows PowerShell, use `curl.exe` to invoke curl rather than the `Invoke-WebRequest` alias. All eight endpoint examples are also in [TechVault.Api.http](apps/api/src/TechVault.Api/TechVault.Api.http); see [the public catalog contract](docs/api/PUBLIC_CATALOG.md) for responses, filters, sorting, and pagination limits.

Only Published devices are public. Unknown and unpublished device slugs return the same JSON 404. Lists return `{ "data": [], "pagination": { ... } }`; detail returns `{ "data": { ... } }`. API responses carry `X-Trace-Id`, and JSON errors include the same `error.traceId`.

The API delegates to concrete Application query handlers; EF queries project DTOs without tracking entities. Small `Result<T>`/pagination contracts support these actual use cases. Phase 4 introduced no MediatR, repository, Unit of Work, extra package, schema change, or migration. Search/timeline, comparison, and admin writes are implemented in Phases 6–8 below; caching remains deferred.

## Phase 5: two phones + two computers

`--seed-catalog` inserts Nokia 3210, Macintosh 128K, and the original 1998 iMac G3 alongside Nokia 3310. Phase 5 introduced no migration or API contract. With all current migrations applied, verify `DATABASE_URL` targets the intended TechVault database and run:

```sh
dotnet run --project apps/api/src/TechVault.Api --launch-profile http -- --seed-catalog
dotnet run --project apps/api/src/TechVault.Api --launch-profile http
```

On an unedited four-device dataset, `/phones` returns two devices and `/computers` returns two. Both computers belong to `Computers → All-in-One Computers`; Phase 5 introduced no laptop, comparison-group, or relationship data. Try:

```sh
curl -i http://localhost:5078/api/v1/devices/nokia-3210
curl -i http://localhost:5078/api/v1/devices/macintosh-128k
curl -i http://localhost:5078/api/v1/devices/imac-g3/specifications
curl -i "http://localhost:5078/api/v1/computers?brand=apple&category=all-in-one-computers&sort=release-asc"
curl -i "http://localhost:5078/api/v1/devices?decade=1990&sort=release-asc"
```

The final query spans categories: iMac G3 (1998) and Nokia 3210 (1999). A database that has not rerun the seed can still return empty computer results; that is a valid 200 response, not an API failure. Seeding will not republish existing hidden records to force these expected counts.

## Phase 6: search + timeline

Review and explicitly apply `AddCatalogDiscovery` to your intended database before starting this version. For an existing Phase 3–5 database, first stop the old local API, verify `DATABASE_URL`, then run:

```sh
dotnet ef migrations script InitialCatalog AddCatalogDiscovery --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
dotnet ef database update --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
dotnet run --project apps/api/src/TechVault.Api --launch-profile http
```

If relying on local `appsettings.Development.json` instead of an environment variable, append `-- --environment Development` to each EF command. Never log or commit credentials. API startup still does not migrate or seed. The migration backfills searchable text without changing existing editorial fields, timestamps, publication states, or specifications. No seed rerun is needed for existing records to become searchable. New aliases default to empty and model numbers to unknown; existing seed content is not overwritten to populate them.

```sh
curl -i "http://localhost:5078/api/v1/search?q=Nokia%203310"
curl -i "http://localhost:5078/api/v1/search?q=Apple&page=1&pageSize=2"
curl -i "http://localhost:5078/api/v1/timeline"
curl -i "http://localhost:5078/api/v1/timeline?era=1990s"
curl -i "http://localhost:5078/api/v1/timeline?type=computers&brand=apple&fromYear=1980&toYear=1999"
```

Both endpoints return the existing paginated device cards and only published devices. Search matches name, aliases, model number, brand name, short description, and description. It uses plain whole-word matching, not substring/fuzzy search; timeline sorts oldest first and omits devices without a known release year. See [the API contract](docs/api/PUBLIC_CATALOG.md) for ranking, era/date semantics, and bounds.

Phase 6 added no new package, separate service, comparison model, or admin endpoint. A feature-specific `IDeviceSearch` lets Infrastructure own PostgreSQL full-text details; Domain remains persistence-independent. Integration tests verify edits, visibility, migration backfill/down/up, and query plans on 8,000 disposable test records. Run the plan check with its output visible:

```sh
dotnet test apps/api/tests/TechVault.IntegrationTests/TechVault.IntegrationTests.csproj --filter FullyQualifiedName~Representative_search_and_chronology_queries_use_their_indexes --logger "console;verbosity=detailed"
```

The plan fixture runs `VACUUM (ANALYZE)` after bulk loading to flush the [GIN pending list](https://www.postgresql.org/docs/17/gin.html#GIN-FAST-UPDATE) and refresh statistics; it never disables sequential scans. A four-device catalog can legitimately use sequential scans. There is no additional application-managed vacuum job or catalog expansion in this phase. Reverting the discovery migration drops alias/model metadata, so use rollback only on disposable databases or after a reviewed backup; the implementation tests never migrate your local database.

## Phase 7: compare compatible devices

Apply all migrations, including `AddCatalogComparisons`, before running the current API or seed. For an existing Phase 6 database, stop the API, verify `DATABASE_URL`, review the upgrade SQL, then explicitly apply it:

```sh
dotnet ef migrations script AddCatalogDiscovery AddCatalogComparisons --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
dotnet ef database update --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
dotnet run --project apps/api/src/TechVault.Api --launch-profile http
```

Append `-- --environment Development` to EF commands if using `appsettings.Development.json` instead of a terminal environment variable. No startup migration or seed is added. The migration initializes new comparison metadata for the four known samples when their brand/category still matches the seed classification, without changing existing editorial fields, status, timestamps, or specification values. It opts the 28 technical sample definitions into comparison; `announcement_date` and unknown definitions stay opted out. Existing matching samples need no seed rerun. Reclassified/other records remain unassigned and fail closed until explicitly reviewed; reseeding does not repair them.

```sh
curl -i "http://localhost:5078/api/v1/compare?devices=nokia-3310,nokia-3210"
curl -i "http://localhost:5078/api/v1/compare?devices=macintosh-128k,imac-g3&differencesOnly=true"
```

`devices` requires exactly two distinct lowercase slugs, in the desired column order. Both must be Published and assigned to the same explicit comparison group. Missing values remain missing, not zero/false; no winner, scoring, unit conversion, or device-specific strategy is introduced. See [comparison contract and migration notes](docs/api/COMPARISON.md) for errors, equality rules, and the response.

Run the normal build/test commands or focus on this capability with `dotnet test apps/api/TechVault.slnx --filter FullyQualifiedName~Comparison`. Tests use isolated PostgreSQL containers and cover both real pairs, typed values, visibility/metadata changes, non-overwriting reseeds, foreign keys, and migration upgrade/down/up. A schema rollback drops the new group assignments and comparability flags; do not use it on a valuable database without a reviewed backup.

## Phase 8: protected content management

The `/api/v1/admin` endpoints manage devices, typed specifications, brands, categories, specification groups/definitions, and publication state. `DELETE /api/v1/admin/devices/{id}` archives the device; it never deletes editorial content or specifications. All admin reads and writes require one securely configured API key in `Authorization: Bearer <key>`. Public catalog and health endpoints remain unauthenticated.

Set `Admin__ApiKey` in the API process environment (equivalent configuration key: `Admin:ApiKey`). Missing/invalid configuration disables admin access with 401; there is no default credential. `.env` is not automatically loaded by `dotnet run`. Generate a random local key without printing or committing it, then launch the API from the same PowerShell session with `DATABASE_URL` already configured:

```powershell
$adminKeyBytes = New-Object byte[] 32
$adminRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$adminRng.GetBytes($adminKeyBytes)
$adminRng.Dispose()
$env:Admin__ApiKey = [Convert]::ToBase64String($adminKeyBytes)
dotnet run --project apps/api/src/TechVault.Api --launch-profile http
```

Keep the key in a private secret store if it must survive this session. A trusted client must use the same key; generating another key in a second terminal will not authenticate against the running process. Use HTTPS outside loopback development, never query-string credentials or a public frontend bundle. Rotate by replacing the configured key and restarting the API. See [the admin API contract](docs/api/ADMIN.md) for setup, request bodies, lifecycle rules, and errors; [TechVault.Admin.http](apps/api/src/TechVault.Api/TechVault.Admin.http) contains examples using a private client variable.

Phase 8 adds FluentValidation with manual validation in concrete Application handlers and domain methods for editing/lifecycle rules. It reuses the current schema: no new migration, account table, JWT server, repository, Unit of Work, MediatR, media pipeline, or admin UI. Apply existing migrations through `AddCatalogComparisons` explicitly if your database is behind; startup still never migrates or seeds. PUT replaces the documented content fields; use GET to load the current content before editing. Writes are immediate and intended for one trusted editor, not a multi-user approval/versioning workflow.

```sh
dotnet build apps/api/TechVault.slnx
dotnet test apps/api/TechVault.slnx --filter FullyQualifiedName~Admin
dotnet test apps/api/TechVault.slnx
```

Admin tests use disposable PostgreSQL containers and generated test-only keys. They cover every route's authorization, editorial lifecycle/public visibility, all four specification types, safe reference CRUD, semantic restrictions, duplicate/racing writes, search updates, and unchanged schema. Production hardening and deployment remain Phases 9–10; this phase does not expose a production service.
