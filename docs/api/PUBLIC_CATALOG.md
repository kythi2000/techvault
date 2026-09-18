# Public catalog API — Phases 4–6

Base path: `/api/v1`. These are read-only, unauthenticated endpoints. The API must point to a database with all migrations applied, including Phase 6's `AddCatalogDiscovery`. Migrations and catalog seeding remain explicit commands; startup never performs them. See [local setup](../../README.md) and [HTTP examples](../../apps/api/src/TechVault.Api/TechVault.Api.http).

## Endpoints

| GET path | Data returned |
| --- | --- |
| `/devices` | Paginated published-device cards. |
| `/devices/{slug}` | Published device detail, including history, SEO fields, classification, physical details, timestamps, and ordered specifications. |
| `/devices/{slug}/specifications` | Device identity (`deviceId`, `name`, `slug`) and `specificationGroups`, with the same group/value contract as detail. |
| `/phones` | The device browse query constrained to the `phones` category subtree. |
| `/computers` | The device browse query constrained to the `computers` category subtree. |
| `/brands` | Paginated brand metadata and published-device counts, ordered by name then slug. |
| `/brands/{slug}` | One brand's metadata and published-device count. Use `/devices?brand={slug}` for its devices. |
| `/categories` | Paginated flat taxonomy, ordered by display order then slug; each item includes parent ID/slug. Parents may be on another page. |
| `/search?q=...` | Paginated published-device cards ordered by full-text relevance, then slug. |
| `/timeline` | Paginated published-device cards with known release years, oldest first. |

Cards contain `id`, `name`, `slug`, `shortDescription`, brand/category references, `releaseYear`, and `releaseDate`. A brand reference contains ID/name/slug. A category reference additionally contains `parentCategoryId` and `parentSlug`. Detail adds `description`, `history`, `seoTitle`, `seoDescription`, `discontinuedDate`, `physicalDetails` (nullable height/width/depth in mm and weight in grams), `createdAt`, `updatedAt`, `publishedAt`, and `specificationGroups`.

Only Published devices appear in cards, detail, specifications, or brand counts. Missing, Draft, and Archived device slugs all return the same `DEVICE_NOT_FOUND` 404, without exposing their status or content. Brands/categories are public reference metadata and remain visible even if no published devices use them. A valid but unknown filter slug gives an empty list, not 404.

Phase 5 adds Nokia 3210, Macintosh 128K, and the original 1998 iMac G3 to the sample seed, without changing these contracts. On a fresh seeded database, `/phones` returns two Nokia records and `/computers` returns two Apple records under `computers → all-in-one-computers`. Root-category filters include those descendants. `/devices?decade=1990` spans both types, returning Nokia 3210 and iMac G3 in the default descending-year order. See [seed scope and sources](../catalog/SEED_DATA.md). An empty/unseeded category still returns an empty 200, including databases that have not yet rerun the seed.

## Browse parameters

`/devices`, `/phones`, and `/computers` share these optional query parameters:

| Parameter | Accepted values and behavior |
| --- | --- |
| `brand` | Exact brand slug. |
| `category` | Exact category slug; includes that category and all descendants. |
| `type` | `phones` or `computers`, mapped to the corresponding category subtree. A conflicting type on `/phones` or `/computers` is 400. |
| `year` | Exact release year, 1–9999. |
| `fromYear`, `toYear` | Inclusive release-year boundaries, each 1–9999; either may be omitted. When both are set, `fromYear` must not exceed `toYear`. |
| `decade` | First year of a decade, a multiple of 10 from 10–9990, e.g. `1990` means 1990–1999. |
| `sort` | `release-desc` (default), `release-asc`, `name-asc`, or `name-desc`. |
| `page`, `pageSize` | Pagination described below. |

Filters combine with **AND**, including type/category and exact/range/decade constraints. Well-formed but non-overlapping constraints produce an empty list. Devices with unknown release years are excluded whenever a year filter is applied. Without year filters, unknown years sort last in both release sort directions. All sorts use ascending unique slug as the final tie-breaker; name sorting uses the database's collation.

Slugs are lowercase ASCII letters/digits separated by single hyphens, at most 160 characters; empty values, underscores, spaces, and uppercase letters are rejected. Type/sort values are also case-sensitive. Invalid numeric syntax/ranges, malformed slugs, unsupported type/sort values, and inverted ranges return JSON 400. Unrecognized query parameter names are ignored; browse endpoints do not accept text-search filters. Use `/search` for full-text search.

## Search parameters and ranking

`GET /search?q=Nokia%203310&page=1&pageSize=24` accepts required `q` and the normal pagination parameters. `q` must contain 1–200 characters, including at least one letter or digit. Whitespace-only, punctuation-only, missing, overlong, and invalid-pagination requests return the existing `VALIDATION_ERROR` 400. Surrounding whitespace is trimmed after validation.

Search uses PostgreSQL `plainto_tsquery('simple', q)`: case-insensitive tokens combined with AND, including terms spread across different indexed fields. Punctuation follows PostgreSQL tokenization, not user-provided query operators. There is no stemming, substring/prefix matching, typo correction, accent folding, or suggestion service. Brand matches use the current brand **name**, not slug/description. History, SEO fields, categories, and structured specifications are not searched. See [Npgsql full-text mapping](https://www.npgsql.org/efcore/mapping/full-text-search.html) for the provider operations.

The weighted document uses name/aliases/model number at weight A, brand name at B, and short description/description at C. Results sort by `ts_rank` descending, then unique slug ascending for equal ranks; the rank is internal and the response stays a device card. Repeated occurrences also influence relevance, so this is not a strict field-priority bucket sort. Empty matches return a paginated 200. Pagination is deterministic for unchanged content; offset pages are not a snapshot across concurrent edits.

`Device.SetSearchMetadata(modelNumber, aliases)` validates optional model numbers (1–100 trimmed characters when provided) and up to 20 aliases of 1–100 trimmed characters, removing case-insensitive duplicates. Pass `null`/`[]` to clear metadata. This is domain behavior, not a new editing API; aliases/model numbers are not added to existing public DTOs in this phase. Unknown seed metadata stays unknown.

Infrastructure maps aliases to a PostgreSQL array and maintains a shadow `tsvector` with a GIN index. Device inserts/changes and brand renames refresh it in the same database transaction, including direct SQL and `ExecuteUpdate` writes. The device trigger locks its brand row while reading the name to serialize with renames. As with other conflicting PostgreSQL writes, deadlocks can abort a transaction; never treat an aborted write as successful. Do not write `SearchVector` directly or bypass triggers. The migration backfills existing devices without modifying editorial content; rollback removes the added metadata/indexes/functions/triggers. No startup synchronization or background worker exists.

## Timeline parameters and ordering

`GET /timeline` accepts `brand`, `category`, `type`, `year`, `fromYear`, `toYear`, `era`, `page`, and `pageSize`. Brand/category/type/year filters have the same validation and AND semantics as browse, including descendant-category traversal. Omit type for a global timeline; `phones`, `computers`, or a brand filter use the same handler. Unknown slugs and non-overlapping valid filters return an empty 200.

`era` is a decade label, not a stored entity: four ASCII digits ending in zero followed by lowercase `s`, from `0010s` to `9990s`. For example `era=1990s` selects 1990–1999 inclusive. Other formats return 400. Use year ranges for broader historical periods. `decade` and `sort` are browse parameters, not timeline parameters; chronology has one fixed order.

Timeline excludes records with unknown `releaseYear`, even with no date filters; these records remain discoverable by browse/search. It orders by release year ascending, exact release date ascending (unknown dates last within that year), then unique slug ascending. Year-only records retain `releaseDate: null`; announcement specifications are never substituted for release dates. There are no timeline copies, event tables, or frontend changes.

## Pagination and response envelopes

Every list endpoint accepts `page` (default 1, range 1–10,000) and `pageSize` (default 24, range 1–100). Invalid bounds are rejected, not clamped. Database queries apply filters and ordering before offset/limit; they do not materialize the device catalog to paginate it.

Successful detail responses wrap their DTO in `data`. List responses use:

```json
{
  "data": [],
  "pagination": {
    "page": 1,
    "pageSize": 24,
    "total": 0,
    "totalPages": 0
  }
}
```

`total` counts matching records before pagination. A page beyond the last result is still 200 with empty `data`; it retains the requested page and actual total. Ordering is deterministic for an unchanged catalog, not a snapshot across requests while editors modify content.

All `/api/v1` responses include `X-Trace-Id`. Errors use:

```json
{
  "error": {
    "code": "DEVICE_NOT_FOUND",
    "message": "Device was not found.",
    "traceId": "request-trace-id"
  }
}
```

The trace ID matches the response header. Expected codes are `VALIDATION_ERROR` (400), `DEVICE_NOT_FOUND`/`BRAND_NOT_FOUND` (404), `NOT_FOUND` for an unknown API route (404), and `METHOD_NOT_ALLOWED` (405). Unexpected failures, including database errors, return `UNEXPECTED_ERROR` (500) with a generic message; server logs contain the exception and trace ID. Health endpoints and the Development-only `/openapi/v1.json` retain their existing, non-enveloped contracts. Full operational hardening remains Phase 9.

## Structured specifications

`specificationGroups` are ordered by group `displayOrder`, then group key. Each group contains ID/key/name/display order and `specifications` ordered by definition `displayOrder`, then definition key. Only assigned definitions/groups are returned; a published device with no assigned specifications has an empty array.

Each specification contains definition `id`, `key`, `name`, `dataType`, nullable `unit`, `displayOrder`, and four typed value fields: `valueText`, `valueNumber`, `valueBoolean`, `valueDate`. `dataType` is `text`, `number`, `boolean`, or `date`; exactly its matching field is populated. Missing values are unknown, not inferred defaults; `false` and `0` remain real values. Dates serialize as `YYYY-MM-DD`. Nokia's announcement date is a specification, not a fabricated exact retail release date.

## Implementation boundaries and verification

API endpoints bind requests and map Application results to HTTP; concrete feature-local handlers validate and execute no-tracking DTO projections. Browse/timeline reuse published filtering and taxonomy traversal; all discovery/browse results reuse the same card projection. Search uses a feature-specific `IDeviceSearch` implemented by Infrastructure with Npgsql; other reads use `ITechVaultDbContext`. Domain remains independent of EF/Infrastructure. Phase 6 adds one discovery migration, not new entity tables, packages, repositories, Unit of Work, or MediatR.

Run from the repository root with Docker running:

```sh
dotnet sln apps/api/TechVault.slnx list
dotnet build apps/api/TechVault.slnx
dotnet test apps/api/TechVault.slnx
dotnet ef migrations has-pending-model-changes --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
```

The EF command requires the configured `DATABASE_URL` and restored local tool; it does not apply migrations. Add `-c Release` to build/test and `--configuration Release` to EF when a running Debug API locks output. PostgreSQL tests use disposable containers with both the real four-device seed and dedicated synthetic fixtures, not your local database. They exercise all ten endpoints, cross-category reads, published visibility, editorial read-through, combined filters, stable pagination, typed/ordered specifications, no-tracking reads, Development OpenAPI, Production error contracts, and database outages. Discovery tests additionally cover every indexed field, relevance ties, brand/device edits, visibility changes, migration backfill/down/up, and real `EXPLAIN (ANALYZE, BUFFERS)` plans on 8,000 synthetic records after routine vacuum/analyze; the planner is not forced to avoid sequential scans.

Comparison compatibility, admin mutations/authentication, product families, caching, and frontend discovery work are intentionally absent.
