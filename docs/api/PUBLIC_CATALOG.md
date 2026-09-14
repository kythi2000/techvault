# Public catalog API — Phase 4

Base path: `/api/v1`. These are read-only, unauthenticated endpoints. The API must point to a database with the Phase 3 migration applied. Migrations and Nokia 3310 seeding remain explicit commands; startup never performs them. See [local setup](../../README.md) and [HTTP examples](../../apps/api/src/TechVault.Api/TechVault.Api.http).

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

Cards contain `id`, `name`, `slug`, `shortDescription`, brand/category references, `releaseYear`, and `releaseDate`. A brand reference contains ID/name/slug. A category reference additionally contains `parentCategoryId` and `parentSlug`. Detail adds `description`, `history`, `seoTitle`, `seoDescription`, `discontinuedDate`, `physicalDetails` (nullable height/width/depth in mm and weight in grams), `createdAt`, `updatedAt`, `publishedAt`, and `specificationGroups`.

Only Published devices appear in cards, detail, specifications, or brand counts. Missing, Draft, and Archived device slugs all return the same `DEVICE_NOT_FOUND` 404, without exposing their status or content. Brands/categories are public reference metadata and remain visible even if no published devices use them. A valid but unknown filter slug gives an empty list, not 404.

The sample seed is unchanged: Nokia 3310 only. `/computers` returns an empty 200 response even when the `computers` root category does not yet exist. Additional real phone/computer sample content belongs to Phase 5.

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

Slugs are lowercase ASCII letters/digits separated by single hyphens, at most 160 characters; empty values, underscores, spaces, and uppercase letters are rejected. Type/sort values are also case-sensitive. Invalid numeric syntax/ranges, malformed slugs, unsupported type/sort values, and inverted ranges return JSON 400. Unrecognized query parameter names are ignored; there is no text-search parameter in this phase.

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

API endpoints bind requests and map Application results to HTTP; concrete feature-local handlers validate and execute no-tracking DTO projections through `ITechVaultDbContext`. Detail/specification reads share their actual ordering/projection logic, and all three browse endpoints share one handler. A small projected taxonomy traversal supports descendant filtering without per-node queries. Domain remains independent of EF/Infrastructure. No new persistence schema, indexes, migrations, packages, repositories, Unit of Work, or MediatR are required for this phase.

Run from the repository root with Docker running:

```sh
dotnet sln apps/api/TechVault.slnx list
dotnet build apps/api/TechVault.slnx
dotnet test apps/api/TechVault.slnx
dotnet ef migrations has-pending-model-changes --project apps/api/src/TechVault.Infrastructure --startup-project apps/api/src/TechVault.Api
```

The EF command requires the configured `DATABASE_URL` and restored local tool; it does not apply migrations. Add `-c Release` to build/test and `--configuration Release` to EF when a running Debug API locks output. PostgreSQL tests use disposable containers and synthetic fixtures, not your local database. They exercise all eight endpoints, published visibility, editorial read-through, combined filters, stable pagination, typed/ordered specifications, no-tracking reads, Development OpenAPI, Production error contracts, and database outages.

Search, timeline, comparison compatibility, admin mutations/authentication, product families, caching, and frontend work are intentionally absent.
