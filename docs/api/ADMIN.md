# Admin content API — Phase 8

Base path: `/api/v1/admin`. All reads and writes require the `CatalogAdmin` authorization policy and the single `AdminBearer` authentication scheme. These endpoints are for a trusted editor's CLI/HTTP client or private server-side integration, not an admin UI or public user accounts. The [public API](PUBLIC_CATALOG.md) remains anonymous and published-only.

## Credential configuration

Configure `Admin:ApiKey` outside source, normally with the API process environment variable `Admin__ApiKey`. There is no default key, login endpoint, cookie, JWT issuance, or user table. Generate at least 32 random bytes using a cryptographically secure generator and encode as base64 (see [local setup](../../README.md#phase-8-protected-content-management)). The accepted text length is 32–256 characters without whitespace; length checks alone do not guarantee a strong key.

Each request supplies `Authorization: Bearer <configured-key>`. Only that header is accepted: query parameters, cookies, Basic auth, and duplicate Authorization headers do not grant access. Comparison uses fixed-length hashes and constant-time equality. Invalid/missing key configuration fails closed for admin endpoints with 401, while public/liveness endpoints still work. Replace the configured key and restart to rotate it; existing keys then stop working.

Keep the key in a private secret store/process environment. Do not write it into tracked `.env.example`, appsettings, `.http` files, screenshots/logs, shell command history, or public frontend variables/bundles (`NEXT_PUBLIC_*`). Do not enable header/body logging that captures Authorization. A `.env` file alone does not configure `dotnet run`, and the current Compose service is PostgreSQL only. Use HTTPS for all non-loopback traffic; production TLS/network hardening belongs to Phases 9–10. Until then, keep development access local.

For a trusted PowerShell client that already has the same key in its private environment:

```powershell
$adminHeaders = @{ Authorization = "Bearer $env:Admin__ApiKey" }
Invoke-RestMethod -Uri 'http://localhost:5078/api/v1/admin/devices?status=draft' -Headers $adminHeaders
```

Neither this command nor the API needs to print the key. The API does not log credentials or include them in error responses.

## Endpoints

IDs below are GUIDs, not slugs. Mutations use JSON bodies where noted.

| Method/path | Behavior |
| --- | --- |
| `GET /devices` | Paginated list including draft/published/archived; optional `status=draft\|published\|archived`. Ordered by slug. |
| `GET /devices/{id}` | Full editable `content`, status, timestamps, and stored typed `specifications`, including private devices. |
| `POST /devices` | Create a draft from DeviceInput; 201 and `Location` header. |
| `PUT /devices/{id}` | Replace DeviceInput fields; does not replace specifications or change status. |
| `POST /devices/{id}/publish` | Publish a complete draft; already-published is a no-op. |
| `POST /devices/{id}/unpublish` | Return published content to draft; already-draft is a no-op. |
| `POST /devices/{id}/archive` | Archive draft/published content; already-archived is a no-op. |
| `DELETE /devices/{id}` | Same archive operation; **never a physical device deletion**. |
| `PUT /devices/{id}/specifications/{definitionId}` | Add or replace one typed value. |
| `DELETE /devices/{id}/specifications/{definitionId}` | Remove that value; the definition remains. Missing assignment is 404. |
| `GET /brands`, `/categories`, `/specification-groups`, `/specification-definitions` | Paginated reference lists. |
| `GET /brands/{id}`, `/categories/{id}`, `/specification-groups/{id}`, `/specification-definitions/{id}` | Reference detail. |
| `POST /brands`, `/categories`, `/specification-groups`, `/specification-definitions` | Create a reference; 201 and `Location` header. |
| `PUT /brands/{id}`, `/categories/{id}`, `/specification-groups/{id}`, `/specification-definitions/{id}` | Update permitted reference fields. |
| `DELETE /brands/{id}`, `/categories/{id}`, `/specification-groups/{id}`, `/specification-definitions/{id}` | Physical deletion **only when unreferenced**. |
| `GET /comparison-groups` | Read-only paginated picklist for existing Phase 7 compatibility groups. |

All lists accept `page` (1–10,000, default 1) and `pageSize` (1–100, default 24) and use the existing `{data, pagination}` envelope. Reference lists order by slug/key, with display order first for categories and specification metadata. Detail/mutation success uses `{ "data": ... }`. Reference responses include their IDs and editable fields. Device writes return `{id, slug, status, updatedAt, publishedAt}`; reference deletes return `{id}`. Device DELETE returns the archived device state, not a deletion receipt. Responses carry `X-Trace-Id`.

## Device content

For POST/PUT, the request body is the content object itself, not `{content: ...}`. Get an existing device and edit `data.content` before PUT. Example (replace reference IDs with values from admin picklists):

```json
{
  "name": "Editorial device",
  "slug": "editorial-device",
  "brandId": "11111111-1111-1111-1111-111111111111",
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "comparisonGroupId": null,
  "shortDescription": "Summary",
  "description": "Overview",
  "history": "Historical context",
  "seoTitle": "Editorial device | TechVault",
  "seoDescription": "Historical device overview",
  "modelNumber": null,
  "aliases": ["Alternative name"],
  "releaseYear": 2000,
  "releaseDate": null,
  "discontinuedDate": null,
  "heightMm": null,
  "widthMm": null,
  "depthMm": null,
  "weightGrams": null
}
```

Required identity fields: name (1–200 characters), lowercase hyphenated slug (1–160), and existing brand/category IDs. An optional comparisonGroupId must exist; null means comparison unavailable. Compatibility is an explicit editorial assignment, not inferred from category.

Drafts may have incomplete editorial content. Publishing requires nonblank shortDescription (max 500), description/history (each max 100,000), seoTitle (max 200), and seoDescription (max 500). Editing a published device must retain those required fields; unpublish first to save incomplete content. Unknown release/physical data stays null. Release years must be 1–9999, dates must agree with the year, discontinuation cannot precede release, and known measurements must be positive. Model numbers and up to 20 aliases are limited to 100 characters each; aliases are trimmed and deduplicated without case sensitivity. Text cannot contain NUL characters.

PUT is a full replacement of these content fields, not PATCH: omitted optional fields reset to defaults/null/empty arrays. It does not replace separately managed specifications. IDs, timestamps, and status are server-managed. Device slugs may be edited with uniqueness checks; old URLs are not redirected automatically. Read the current record before editing; concurrent successful content edits use last-write-wins (no versioning/approval workflow).

Unpublishing and archiving clear publishedAt, retain all content/specifications, and remove the device from public detail, browse, search, timeline, comparison, and published brand counts. Archived devices remain available to admin reads, but content/specification writes, publish, and unpublish return 409. There is no restore/hard-delete route. Repeating archive or deleting an already-archived device is safe. Reference lists are public metadata independently of device visibility, as before Phase 8.

## Typed specifications

Supply exactly one non-null typed field, matching the referenced definition's dataType:

```json
{ "valueText": "GSM" }
```

```json
{ "valueNumber": 0 }
```

```json
{ "valueBoolean": false }
```

```json
{ "valueDate": "2000-09-01" }
```

Text must be nonblank and at most 10,000 characters. Zero and false are real values, not missing. Empty/multiple typed values, malformed dates, wrong types, and missing definitions return 400. PUT replaces a value without creating duplicate assignments. DELETE removes a value to represent unknown; it does not delete other values or alter the definition. Successful edits appear immediately in public specifications/comparisons when published.

## Reference bodies and restrictions

| Resource | POST/PUT body fields | Immutable after creation |
| --- | --- | --- |
| Brand | `name`, `slug`, `description` (default empty) | `slug` |
| Category | `name`, `slug`, `displayOrder` (default 0), `parentCategoryId` (default null), `description` (default empty) | `slug`, `parentCategoryId` |
| Specification group | `name`, `key`, `displayOrder` (default 0) | `key` |
| Specification definition | `name`, `key`, `groupId`, `dataType`, `displayOrder` (default 0), `unit` (default null), `isComparable` (default false) | `key`, `dataType`, `unit` |

Names are nonblank, max 200; descriptions max 100,000. Keys are lowercase snake_case starting with a letter, max 100; slugs are lowercase hyphenated, max 160. Display order is nonnegative. dataType is exactly `text`, `number`, `boolean`, or `date`; an optional unit is nonblank, max 50.

Slug/key uniqueness is enforced in both handler checks and PostgreSQL constraints. Include the current immutable fields on PUT. Category parents can only be assigned at creation, which prevents reparenting cycles. Definition type/unit/key cannot change even when currently unassigned: create a new definition and explicitly re-enter values instead of silently reinterpreting existing data. A definition's name, group, display order, and comparison opt-in may change without rewriting values. Changes to isComparable/group/order affect subsequent comparisons. A brand rename refreshes searchable device data through the existing PostgreSQL trigger.

Deleting a brand used by **any** device (including drafts/archives), a category with devices/children, a group with definitions, or a definition with assigned values returns 409. There is no cascading catalog deletion. Unreferenced references may be deleted. Database constraints still protect against concurrent writes that pass the initial checks.

## Error contract

Errors use `{ "error": { "code": "...", "message": "...", "traceId": "..." } }`, matching `X-Trace-Id`.

| HTTP/code | Meaning |
| --- | --- |
| 400 `VALIDATION_ERROR` | Invalid fields/query/body, incomplete publication, wrong typed value, or missing input reference. |
| 401 `UNAUTHORIZED` | Missing/invalid credential or disabled admin configuration; `WWW-Authenticate: Bearer`. |
| 403 `FORBIDDEN` | Authenticated principal does not satisfy admin policy (the single built-in credential grants that policy). |
| 404 `ADMIN_RESOURCE_NOT_FOUND` | Missing target resource or missing specification assignment. |
| 409 `DUPLICATE_IDENTIFIER` | Slug/key already in use, including a racing insert. |
| 409 `REFERENCE_CONFLICT` | Reference still in use, or a concurrent reference change violated an FK. |
| 409 `CATALOG_CONFLICT` | Archived write, attempted immutable metadata change, or a row removed during an update/delete. |
| 500 `UNEXPECTED_ERROR` | Unexpected failure; no SQL/provider/credential details are returned. |

Malformed route IDs use the existing route-not-found envelope. Validation is manual FluentValidation in Application handlers plus domain rules, with no new mediator/repository/framework. PostgreSQL error classification stays in Infrastructure; the API translates it into the shared error envelope.

## Database and verification

No Phase 8 migration or seed change is required. Use the existing schema through `AddCatalogComparisons`; migrations remain explicit, never automatic on API startup. Existing editorial content is untouched until an authenticated mutation is requested. There is no media/image metadata in the current model, so no image feature is added.

Run `dotnet build apps/api/TechVault.slnx` and `dotnet test apps/api/TechVault.slnx`; the focused filter is `FullyQualifiedName~Admin`. Tests use generated keys and isolated PostgreSQL containers, never your local database. They cover authorization for all 31 routes, lifecycle and public-read/search visibility, typed values, safe reference editing/deletion, concurrent duplicate creation, and absence of pending model changes.

No frontend/admin UI, accounts, uploads, object storage, bulk import, scheduled publishing, collection/relationship management, production hardening, or deployment is implemented in this phase.
