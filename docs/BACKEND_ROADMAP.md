# TechVault Backend Roadmap

Ten implementation milestones ending with the first production deployment. This replaces the previous 31-phase roadmap and is sized for a personal learning project. No application work is authorized by this document rewrite.

Primary milestone: a real TechVault backend running on a VPS behind Nginx with HTTPS and PostgreSQL, with a small useful catalog, public exploration APIs, and protected content management.

## Starting point and scope

The repository already has four .NET 10 projects with the intended references: `Api → Application + Infrastructure`, `Application → Domain`, and `Infrastructure → Application + Domain`. Preserve this structure. The solution file still contains no projects, the API serves the weather sample, the other projects contain placeholders, and Compose and `.env.example` are empty. There is no product implementation or persistence yet.

[SPEC.md](SPEC.md) remains the product vision. This revision intentionally narrows its first-release requirements: Redis, image uploads/object storage, collections, analytics, and the approximately 20-device catalog are deferred. The first production backend can launch with Nokia 3310, Nokia 3210, Macintosh 128K, and iMac G3. Frontend rendering and the full website MVP are separate work.

## How to use these phases

- Follow the numbered order. Dependencies identify the capabilities each phase needs; they are not separate handoffs.
- Each phase produces one reviewable capability and leaves the application buildable. Related entities, endpoints, migrations, tests, and documentation belong in that phase.
- A suggested commit is a convenient milestone label, not a requirement to create more commits or stop for approval after each internal step.
- Introduce test projects, `Result<T>`, pagination types, validators, and other supporting code when the first real use requires them.
- Use unit tests for meaningful domain rules and integration tests against real PostgreSQL for persistence, API behavior, and admin writes. Avoid extensive EF mocking and empty test scaffolding.

Keep one deployable modular monolith, Clean Architecture boundaries, vertical slices, and CQRS-lite. Thin HTTP endpoints call Application handlers directly. Infrastructure implements persistence; Domain stays independent of Infrastructure, EF Core, and ASP.NET Core. Use EF Core through the small `ITechVaultDbContext` surface needed by the slices. Do not add a Generic Repository, custom Unit of Work, or MediatR.

Use built-in DI, async database calls, propagated cancellation, and no-tracking reads where appropriate. Add a query builder, value object, factory, or comparison strategy only when concrete behavior benefits from it. No mandatory pattern checklist, generic handler framework, microservices, event sourcing, or message broker.

Verification commands below are for later implementation, run from the repository root. The common check is `dotnet build apps/api/TechVault.slnx`; run `dotnet test apps/api/TechVault.slnx` once tests exist, including the tests affected by that phase. Restore dependencies as needed during implementation. Review and apply migrations explicitly; production startup must never auto-migrate.

## Phase 1 — Foundation

**Depends on:** Existing scaffold.

### Goal

Make the existing API solution build and run reliably.

### Why now

The empty solution currently does not build the existing projects. A working host is the starting point for every capability.

### Scope

- Register the four existing projects in `TechVault.slnx` and preserve their references.
- Remove generated weather/placeholder code; establish the thin endpoint host, development OpenAPI, and `/health/live`.
- Update the HTTP request example and add brief local run/build instructions.

### Out of scope

Persistence, product entities, feature abstractions, authentication, and external services.

### Expected changes

`TechVault.slnx`, `Program.cs`, placeholder files, the API `.http` file, and a short README.

### Database impact

None.

### Acceptance criteria

- The solution lists and builds all four projects.
- The API starts, `/health/live` returns 200, and the weather sample is gone.

### Verification

Run the common build, `dotnet sln apps/api/TechVault.slnx list`, and `dotnet run --project apps/api/src/TechVault.Api`; request `/health/live`.

### Suggested commit

`chore(api): establish runnable backend foundation`

## Phase 2 — PostgreSQL + EF Core

**Depends on:** Phase 1.

### Goal

Run the API against a real local PostgreSQL instance.

### Why now

The catalog needs a working database connection, reproducible configuration, and a way to verify PostgreSQL behavior.

### Scope

- Add PostgreSQL with a persistent development volume to Compose and document connection configuration in `.env.example`.
- Add EF Core/Npgsql, `TechVaultDbContext`, Infrastructure registration, and design-time migration support.
- Add `/health/ready` for PostgreSQL and the first integration tests alongside the connection behavior they verify.

### Out of scope

Product tables, empty migrations, Redis, object storage, API containerization, and automatic startup migrations.

### Expected changes

Compose/environment configuration, Infrastructure persistence, API registration/health checks, and integration tests.

### Database impact

A local PostgreSQL database and volume; product schema arrives in Phase 3.

### Acceptance criteria

- The configured API connects to PostgreSQL and an integration test uses the real provider.
- Readiness becomes unhealthy when PostgreSQL is unavailable while liveness remains healthy.
- Configuration contains no committed credentials.

### Verification

Run `docker compose config`, `docker compose up -d postgres`, the common checks, and readiness checks with PostgreSQL available and unavailable.

### Suggested commit

`feat(persistence): connect api to postgresql with ef core`

## Phase 3 — Store one complete device

**Depends on:** Phase 2.

### Goal

Persist Nokia 3310 with the content needed for a useful device detail and specification response.

### Why now

Classification, device content, and typed specifications together form the first useful catalog record.

### Scope

- Implement Brand, hierarchical Category, Device, SpecificationGroup, SpecificationDefinition, and DeviceSpecification as one coherent content model.
- Share only `Guid Id` through a small, non-generic Domain `BaseEntity` for the five single-ID entities. Keep existing timestamps on Brand/Device and retain DeviceSpecification's composite key without inheritance. This is a code-only refactor of the established schema, not a separate milestone or migration; do not map BaseEntity as an EF entity.
- Include identity, publication state, descriptive/history content, release information, physical data where known, SEO title/description, and timestamps. Preserve unknown historical values rather than inventing precision.
- Store specifications dynamically with type validation and display order. Introduce `ITechVaultDbContext`, domain tests, EF mappings, and the initial schema with these real uses.
- Reference data such as Nokia, Phones, and Feature Phones may be seeded idempotently. Insert the Nokia 3310 sample and its specifications only when the device is missing; never overwrite existing editorial content. Use a simple existence check and insert, with no synchronization/upsert system.

### Out of scope

Public endpoints, admin writes, product families, relationships, a media module, view counters, and comparison compatibility modeling/algorithms. Image binaries and uploads are deferred; an optional existing image URL with attribution metadata is sufficient if needed for the sample.

### Expected changes

Domain catalog/specification modules, the Application persistence contract, Infrastructure mappings/migration/seed, and relevant tests.

### Database impact

The initial catalog schema with foreign keys, unique slugs/keys, device-definition uniqueness, typed-value constraints where practical, and basic lookup indexes.

### Acceptance criteria

- Seeded Nokia 3310 round-trips through PostgreSQL with ordered, typed specifications and publishable editorial content.
- Rerunning the seed creates no duplicate reference data and leaves an existing Nokia 3310 record and its specifications untouched.
- Invalid specification types, duplicate identifiers, and invalid classification/publication state are rejected.
- The model has no separate phone/computer entity tables.
- Shared entity identity does not introduce a base table, discriminator, new audit columns, or a surrogate key on DeviceSpecification; existing mappings and the migration snapshot remain aligned.

### Verification

Apply the initial migration to a disposable database, seed once, edit Nokia 3310 content/specifications, rerun the seed, and verify the edits are unchanged. Run the common checks with domain/persistence tests.

### Suggested commit

`feat(catalog): store complete device content and specifications`

## Phase 4 — Public catalog APIs

**Depends on:** Phase 3.

### Goal

Make the stored catalog usable through detail, browse, brand, and category endpoints.

### Why now

This turns the persisted model into the first working public vertical capabilities.

### Scope

- Add `/api/v1/devices`, `/devices/{slug}`, `/devices/{slug}/specifications`, `/phones`, `/computers`, `/brands`, `/brands/{slug}`, and `/categories`.
- Implement feature-local queries, handlers, responses, and validation. Add `Result<T>`, success/error envelopes, trace IDs, and bounded pagination as these endpoints require them.
- Support brand/category/type and year/range/decade filters, stable sorting, and published-only visibility. Reuse filter logic when the endpoints actually share it.
- Return history, SEO fields, classification, and ordered specifications; expose enough card data for a future frontend.

### Out of scope

Full-text search, timelines, comparisons, admin endpoints, product families, caching, and frontend SSR/SEO rendering.

### Expected changes

Application catalog slices and small shared contracts, thin API endpoints/result mapping, request examples, and endpoint tests.

### Database impact

No new product tables expected. Add query indexes only where the actual queries justify them.

### Acceptance criteria

- Nokia 3310 detail/specifications return database content; missing and unpublished slugs return the standard 404.
- Browse filters combine correctly, pagination is bounded/deterministic, and drafts never appear in public results.
- Brand/category navigation works; empty computer results are valid until Phase 5.

### Verification

Run the common checks and PostgreSQL endpoint tests for valid, missing, unpublished, filtered, paginated, and empty results.

### Suggested commit

`feat(catalog): expose public catalog APIs`

## Phase 5 — Prove phones + computers

**Depends on:** Phases 3–4.

### Goal

Validate the same schema and public APIs with two phones and two computers.

### Why now

A small mixed catalog exposes category-specific assumptions before search, comparison, and editing expand the implementation.

### Scope

- Add Nokia 3210, Macintosh 128K, and iMac G3 with meaningful specifications, historical content, and SEO metadata.
- Add only the brands, computer categories, and specification definitions these records need.
- Use consistent form-factor classification for the two computer samples; defer comparison compatibility design to Phase 7.
- Exercise all four devices through the existing public APIs and correct demonstrated shared-model issues.

### Out of scope

The 20-device catalog, laptop-specific behavior, relationships, product families, comparison algorithms, and media infrastructure.

### Expected changes

Seed data, representative integration fixtures, and small model/query corrections only if required.

### Database impact

Primarily data inserts. Add a schema migration only if this proof reveals a real modeling gap; content expansion does not require a migration per device.

### Acceptance criteria

- Four useful published records work through the same detail, specification, and browse contracts.
- Phone and computer category filters both return correct results.
- No parallel phone/computer persistence model or hard-coded device exceptions are introduced.

### Verification

Seed a fresh database, request all four records, and run the common checks with cross-category regression tests.

### Suggested commit

`feat(catalog): validate phone and computer content`

## Phase 6 — Search + timeline

**Depends on:** Phases 4–5.

### Goal

Let visitors find devices by text and explore them chronologically.

### Why now

The mixed catalog provides real content for both discovery queries.

### Scope

- Implement PostgreSQL full-text search at `GET /api/v1/search?q=` across name, brand, description, aliases, and model number; add the latter fields where needed.
- Keep search ranking and pagination deterministic and ensure indexed search data stays correct when device or brand content changes.
- Implement `GET /api/v1/timeline` with category, brand, year/range/era filters, bounded results, and stable chronological ordering.
- Reuse published-device projections and filtering where useful; derive timelines directly from catalog data.

### Out of scope

Elasticsearch, fuzzy recommendation systems, cached suggestions, stored timeline copies, frontend timeline interaction, and analytics.

### Expected changes

Application Search/Timeline slices, PostgreSQL search implementation, API endpoints, schema/index changes, and integration tests.

### Database impact

Alias/model-number storage and PostgreSQL full-text indexes; no separate search service or timeline database.

### Acceptance criteria

- Searches find records through all supported fields and never return drafts.
- Global, phone, computer, and brand timelines work from the same query contract.
- Invalid/unbounded requests are rejected; search remains correct after content updates.

### Verification

Run the common checks, PostgreSQL search/timeline tests including updates, and inspect representative query plans with appropriate fixture data.

### Suggested commit

`feat(discovery): add search and timeline APIs`

## Phase 7 — Comparison

**Depends on:** Phases 4–5; scheduled after Phase 6.

### Goal

Compare two compatible devices using their structured specifications.

### Why now

The two phones and two computers provide enough real variation to validate comparison rules.

### Scope

- Implement `GET /api/v1/compare?devices=...` and its query/handler/response for exactly two distinct published devices.
- Design and enforce comparison compatibility in this phase, introducing `ComparisonGroup` and related assignments only if needed. Align comparable definitions, handle missing values explicitly, and expose differences.
- Compare both sample pairs. Keep shared comparison logic simple; introduce separate strategies only if actual group behavior differs.

### Out of scope

Three-device comparisons, cross-group comparison, automatic scoring, assuming larger numbers are better, and a strategy/resolver per category with identical behavior.

### Expected changes

Application Comparisons slices, focused domain rules if needed, API endpoint, any required persistence mappings/migration, and comparison tests.

### Database impact

If the chosen compatibility model needs persisted groups or assignments, add their schema/migration and sample reference data here. Reuse the existing specification metadata.

### Acceptance criteria

- Both the phone pair and computer pair compare correctly using only comparable definitions.
- Duplicate, missing, unpublished, and incompatible requests return explicit errors.
- Missing values and show-differences behavior have tested, consistent semantics.

### Verification

Run the common checks, focused comparison tests, and endpoint tests for both sample pairs and invalid requests.

### Suggested commit

`feat(comparisons): compare compatible devices`

## Phase 8 — Admin/content management

**Depends on:** Phases 3–7.

### Goal

Maintain and publish the launch catalog through protected API operations.

### Why now

The public capabilities define what editors need to manage; authentication and writes can now ship as one useful capability.

### Scope

- Add one admin authentication scheme and authorization policy using securely configured credentials. Document trusted admin-client usage; keep credentials out of source, logs, and public frontend bundles.
- Add admin list/detail, create/update device, typed-specification editing, and publish/unpublish/archive operations. Treat device `DELETE` as archive and document that behavior.
- Manage the brands, categories, specification groups, and definitions required by these editors, including reference/uniqueness checks and restrictions on unsafe definition changes.
- Edit history, SEO, aliases, classification, and any existing image-reference metadata. Apply FluentValidation and domain rules within the relevant slices.

### Out of scope

Admin UI, public accounts, a full identity platform, collections, families/relationships, uploads, bulk import, scheduled publishing, and destructive deletion of referenced data.

### Expected changes

API authentication/admin endpoints, Application command/query slices, validators, domain rules, configuration examples, and integration tests.

### Database impact

Reuse the catalog schema; add only demonstrated write-workflow constraints/fields. A simple single-admin setup does not inherently need user/account tables.

### Acceptance criteria

- Unauthorized clients cannot read private admin content or mutate data.
- An admin can create a draft, edit its content/specifications, publish it, and unpublish/archive it; every public API respects visibility changes.
- Duplicate identifiers, invalid references/types, and unsafe definition changes fail consistently; search reflects successful edits.

### Verification

Run the common checks and real PostgreSQL tests covering authorization and the full editorial lifecycle, including public-read/search regression checks.

### Suggested commit

`feat(admin): manage and publish catalog content`

## Phase 9 — Production readiness

**Depends on:** Phase 8.

### Goal

Make the API safe to expose and its PostgreSQL data recoverable.

### Why now

The complete launch API surface can now be checked against real operational requirements.

### Scope

- Add structured request/error logs with trace IDs, secret redaction, consistent unexpected-error responses, and useful startup configuration validation.
- Finalize liveness and PostgreSQL readiness; apply CORS allowlists, rate/body limits, HTTPS behavior, secure headers, and trusted-proxy configuration appropriate to Nginx.
- Document secret handling and explicit migration execution. Add a simple PostgreSQL backup/restore procedure, retention settings, and a restore test against a separate database.
- Fix issues found in launch-path integration checks and document how to run and diagnose the API.

### Out of scope

Redis/object-storage health checks, upload security, analytics, OpenTelemetry collectors, monitoring dashboards, distributed tracing, and elaborate performance infrastructure.

### Expected changes

API middleware/options/logging, health checks, deployment notes, backup scripts, and focused tests.

### Database impact

No feature schema expected. Backups and restore drills use isolated destinations; normal startup never migrates production.

### Acceptance criteria

- Production errors do not expose internals or credentials; logs provide a traceable failure.
- Only configured origins receive CORS permission, limits work, and PostgreSQL failure affects readiness rather than liveness.
- A backup restores catalog data and migration history to a separate database successfully.

### Verification

Run the common checks, security/error/health tests, a disposable-database migration rehearsal, and the documented backup/restore drill.

### Suggested commit

`feat(ops): prepare backend for production`

## Phase 10 — Docker, Nginx, CI/CD, and VPS deployment

**Depends on:** Phase 9.

### Goal

Run the real TechVault backend on a VPS behind Nginx with HTTPS and persistent PostgreSQL.

### Why now

The launch capabilities and recovery procedure are ready; deployment is the next deliverable.

### Scope

- Add a multi-stage API Dockerfile and production Compose configuration for API, PostgreSQL, and Nginx, with persistent storage and private database networking.
- Configure the user-provided VPS/domain, DNS, Nginx `/api` routing, TLS certificates, HTTP-to-HTTPS redirection, renewal, and trusted forwarded headers. Keep database/API ports private; expose web traffic through Nginx.
- Add GitHub Actions for backend build/tests and image production, plus a straightforward deployment path with explicit migrations, restart, and health verification. Reuse the same commands for manual recovery.
- Deploy the four-device catalog, schedule backups with seven daily/four weekly retention and a protected copy outside the VPS, and verify recovery instructions against the deployed topology.

### Out of scope

Frontend deployment, Redis, MinIO, Kubernetes, multiple application services, zero-downtime orchestration, and a large catalog. Provisioning access is a practical Phase 10 prerequisite, not another planning milestone.

### Expected changes

Dockerfile, `.dockerignore`, production Compose/Nginx configuration, GitHub Actions, deployment/backup scripts, and a concise runbook.

### Database impact

Apply the existing migrations explicitly to the production PostgreSQL volume and load seed data deliberately. Back up before later migrations; do not automatically roll back schemas or overwrite edited content.

### Acceptance criteria

- The actual domain serves `/api/v1/devices/nokia-3310`, browse/search/timeline, and both comparison pairs over valid HTTPS; authenticated editing works.
- Nginx is the public web entry point, HTTP redirects to HTTPS, and certificate renewal is configured and tested.
- CI passes; deployment applies migrations explicitly, aborts on migration failure, and reports unsuccessful health checks.
- Restarts preserve data; scheduled backups run and a backup from the deployed database has been restored separately.

### Verification

Run `docker compose config`, build/start the production stack, verify CI and the real VPS deployment, and smoke-test public/admin endpoints over HTTPS. Verify renewal, restart persistence, private database access, and restore from a production backup.

### Suggested commit

`build(deploy): ship backend to vps with https`

## Post-MVP

These are optional follow-up capabilities after the first production deployment. They are not additional launch gates or a mandatory sequence.

| Capability | When to take it on | Scope |
| --- | --- | --- |
| Redis | Measured repeated reads justify caching. | Cache-aside for selected public queries, write invalidation, database fallback, and Redis health checks when installed. |
| MinIO/S3-compatible storage and image uploads | Editing/uploading owned media becomes necessary. | Media metadata/associations, storage adapter, validated uploads, object cleanup, and backup/lifecycle handling; replace simple image references when useful. |
| Curated collections | Museum curation becomes a priority. | Ordered device membership, protected editing/publishing, public list/detail, and collection SEO data. |
| Analytics | There are concrete product questions to answer. | Minimal privacy-conscious events/aggregates; avoid tracking infrastructure without a use. |
| Larger catalog | The four-device experience is useful and maintainable. | Grow toward the specification's 20 devices, then further, with verified content and licensed media. Add laptop coverage and group-specific comparison behavior only when needed. |
| Product families and relationships | Related-device navigation has real content to display. | Family membership and explicit predecessor/successor/related links, including admin editing and public projections. |
| Advanced observability | Basic logs and health checks no longer answer operational questions. | Additional metrics, OpenTelemetry, alerts, and dashboards selected for actual diagnostic needs. |

3D and background jobs remain optional future work. Add a job runner only for an actual workload such as image processing or scheduled publishing. Public accounts, new device categories, and community features remain outside this roadmap.

Frontend SSR, canonical links, structured data, OpenGraph rendering, sitemap/robots output, and museum presentation belong to the web roadmap. This backend supplies publication state, stable slugs, timestamps, editorial/SEO fields, and public data; frontend completion is not a dependency of the first backend deployment.

## Next phase

Start with Phase 1 — Foundation when implementation is explicitly requested. The existing projects and references should be retained; registering them in the solution and running the API establishes the baseline needed for PostgreSQL.

## Old phases → new phase

Every old phase is accounted for below. Removed items refer to unnecessary phase boundaries or preselected implementations, not silently dropped launch capabilities.

| Old phase(s) | New destination | What changed |
| --- | --- | --- |
| 1 — Truthful solution baseline | 1 — Foundation | Retained and focused on a runnable API. |
| 2 — Result contracts and test foundation | 2–4, within the first uses | PostgreSQL integration tests join Phase 2; domain tests join Phase 3; result/pagination contracts join Phase 4. The standalone framework phase is removed. |
| 3 — PostgreSQL and EF Core foundation | 2 — PostgreSQL + EF Core | Persistence and its tests stay together. |
| 4–6 — Classification, device, and specifications | 3 — Store one complete device; 7 — Comparison | Catalog content, mappings, migration, seed, and tests stay together; comparison compatibility moves to Phase 7. |
| 7, 12 — Device detail and public browsing | 4 — Public catalog APIs | Related public read capabilities ship together. |
| 8 — Relationships and Nokia 3210 | 5 — Prove phones + computers; Post-MVP | The second phone stays in the proof dataset; relationship modeling moves after deployment. |
| 9, 11 — Phone and desktop comparison | 7 — Comparison | One comparison capability for both sample pairs; separate named strategies are conditional on real differences. |
| 10 — Computer model proof data | 5 — Prove phones + computers | The four-device dataset validates shared models/APIs before comparison. |
| 13 — Brand/category/product-family reads | 4 — Public catalog APIs; Post-MVP | Brand/category reads join public catalog work; families move after deployment. |
| 14–15 — Search and timeline | 6 — Search + timeline | Two related discovery queries form one milestone. |
| 16–19 — Admin auth, taxonomy, device editing, publishing | 8 — Admin/content management | Authentication and protected editorial workflows ship together; family editing follows the deferred family model. |
| 20 — Image storage/upload | Post-MVP — Media | Remove object storage and uploads from launch; optional image references are sufficient initially. |
| 21 — Collections | Post-MVP — Collections | Removed from the launch dependency chain. |
| 22–23 — Phone/computer catalog expansion | Post-MVP — Larger catalog | Launch with four complete devices; expand toward 20 later. |
| 24 — Laptop comparison strategy | Post-MVP — Larger catalog/comparison coverage | Add laptop fixtures and any distinct behavior when actual data needs it; no mandatory strategy class. |
| 25 — Redis cache-aside | Post-MVP — Redis | Removed from deployment/readiness prerequisites. |
| 26 — Logging, readiness, basic metrics | 2 and 9; Post-MVP | PostgreSQL readiness joins Phase 2; launch logging/health joins Phase 9; extra metrics/exporters and deferred-service checks follow actual needs. |
| 27 — API security hardening | 9 — Production readiness | Keep launch protections; upload-specific protections move with uploads. |
| 28–29 — Production containers and CI/CD | 10 — Docker, Nginx, CI/CD, and VPS deployment | One milestone ending in an actual HTTPS deployment, including DNS/TLS work previously excluded. |
| 30 — Backup/restore | 9–10 — Readiness and deployment | Rehearse restore in Phase 9; schedule and verify backups on the VPS in Phase 10. |
| 31 — Analytics | Post-MVP — Analytics | Remains optional after deployment. |
