# TechVault

A phone and computer catalog backend built with .NET 10. The current implementation covers Phases 1–3 in [the backend roadmap](docs/BACKEND_ROADMAP.md): the API foundation, PostgreSQL/EF Core, and storage for one complete Nokia 3310 record. Public catalog endpoints begin in Phase 4.

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

Domain tests do not require Docker:

```sh
dotnet test apps/api/tests/TechVault.UnitTests/TechVault.UnitTests.csproj
```

With Docker running, execute the complete suite:

```sh
dotnet test apps/api/TechVault.slnx
```

Integration tests use Testcontainers to create disposable PostgreSQL containers with random ports and credentials; they do not use the Compose database or your `DATABASE_URL`. They verify connectivity/outages, no automatic schema creation, migration up/down, catalog round-trips, constraints, and preservation of editorial changes across repeated seeds. The first run downloads the container images. If a Debug API process locks build output on Windows, stop it or add `-c Release` to the build/test commands.

To verify the running API manually, request both health endpoints, run `docker compose stop postgres`, and request them again: readiness should be 503 and liveness should stay 200. Run `docker compose up -d postgres --wait` to restore PostgreSQL and verify readiness returns to 200. `docker compose down` stops/removes the development container but preserves the named volume; avoid `down -v` unless you intend to delete the local database.

## EF tooling

The repository pins a local `dotnet-ef` tool. After setting `DATABASE_URL`, verify design-time context creation through the API's normal service registration:

```sh
dotnet tool restore
dotnet ef dbcontext info --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
```

The context and migrations belong to Infrastructure; the API is the startup project. No separate design-time factory is needed. Normal API startup does not call `Migrate`, `EnsureCreated`, or the seed.

## Phase 3: migrate and seed explicitly

First set `DATABASE_URL` as described above. Verify its host, port, and database before proceeding: the following commands modify that database. Use a dedicated TechVault database, not the default `postgres` maintenance database or an unrelated application database.

```sh
dotnet ef migrations script 0 InitialCatalog --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
dotnet ef database update --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
dotnet run --project apps/api/src/TechVault.Api --launch-profile http -- --seed-catalog
```

Review the SQL produced by the first command before applying it. The seed command exits when finished; it does not start the HTTP server or apply migrations. Run it after a successful database update. To start the API afterward, run the usual `dotnet run` command without `--seed-catalog`.

On a fresh database, the first migration may log a failed query against the not-yet-created `__EFMigrationsHistory` table. [Npgsql handles that missing-table case](https://github.com/npgsql/efcore.pg/blob/v10.0.2/src/EFCore.PG/Migrations/Internal/NpgsqlHistoryRepository.cs) and continues creating the schema. Confirm that the command finishes successfully with exit code 0; do not disregard other database errors.

The initial migration creates `Brands`, `Categories`, `Devices`, `SpecificationGroups`, `SpecificationDefinitions`, and `DeviceSpecifications`, plus EF migration history. It contains no seed content. Slugs/keys and device-definition pairs are unique; foreign keys and check constraints enforce references, publication state, dates, positive physical measurements, and typed specification values.

Seed behavior:

- If `nokia-3310` exists, do nothing: no content, status, timestamp, or specification changes, including for drafts or partially populated records.
- Otherwise, reuse reference data by slug/key, insert missing references, and insert the device with its specifications in one `SaveChangesAsync` transaction.
- Existing reference labels/order/content are preserved. An incompatible definition type/unit or category parent causes an explicit error instead of an overwrite.
- Run one seed process at a time. There is no synchronization/upsert engine or concurrent-seed retry mechanism.

The sample contains Nokia, Phones → Feature Phones, five specification groups, and nine values covering text, number, boolean, and date. Content is sourced from [Nokia's original 1 September 2000 announcement](https://www.globenewswire.com/js/news-release/2000/09/01/1845525/0/en/Nokia-introduces-mobile-chat-with-the-Nokia-3310.html). The known release year is stored separately from the announcement date; an exact retail release date, dimensions, and discontinuation date are left unset because this source does not establish them. No image assets are included.

Model conventions:

- Domain has no EF Core/ASP.NET dependency. Infrastructure implements Application's `ITechVaultDbContext` using the existing scoped EF context.
- `Common/BaseEntity` shares only the application-generated `Guid Id` for Brand, Category, Device, SpecificationGroup, and SpecificationDefinition. IDs have no public setter; existing audit fields remain on Brand/Device. DeviceSpecification retains its `(DeviceId, DefinitionId)` composite key and does not inherit BaseEntity.
- BaseEntity is a small, non-generic CLR base class, not an EF entity/table or a persistence inheritance hierarchy. This refactor does not change the schema or require a migration; do not add a `DbSet<BaseEntity>`, generic repository, automatic auditing, soft-delete flags, or domain events to it.
- Specification definitions are shared by key; only values assigned to a device are stored. No separate phone/computer tables or comparison groups exist.
- A missing specification means unknown; `false` and `0` are real values. Each stored value has exactly one typed column, and its type must match the definition.
- Read specifications in group display order, then definition display order, then key for stable ties. Load a device's specifications before calling `SetSpecification` to edit it.
- Category parents are assigned at creation. Reparenting and protected editorial endpoints are later-phase work.

Check that mappings and the migration snapshot remain aligned:

```sh
dotnet ef migrations has-pending-model-changes --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
```

There are no public catalog endpoints in Phase 3; `/api/v1/devices/nokia-3310` still returns 404. Inspect the seeded database through EF/tests or your PostgreSQL client until Phase 4.
