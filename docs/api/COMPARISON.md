# Device comparison — Phase 7

`GET /api/v1/compare?devices=nokia-3310,nokia-3210&differencesOnly=false`

This is a public read-only capability. Apply `AddCatalogComparisons` explicitly before using the current API. Startup does not migrate or seed. Run commands from [the root README](../../README.md).

## Request and compatibility

- `devices`: required, exactly two distinct lowercase device slugs separated by one comma, with no spaces or empty entries. Each slug is at most 160 characters; the full value is at most 321. IDs, three devices, and duplicate slugs are rejected. Column order follows request order, not release year or database order.
- `differencesOnly`: optional boolean, default `false`. `true` removes equal rows and then empty specification groups. It never removes either compared device. An all-equal or no-comparable-values result is still 200 with an empty `specificationGroups` array.
- Both devices must be Published, have a non-null `ComparisonGroupId`, and reference the same group ID. Categories, brand names, or slugs do not determine compatibility at request time. Missing assignments fail closed, even when both are missing.

The sample phones use `phone`; Macintosh 128K and iMac G3 use `all_in_one`. A small persisted `ComparisonGroup` (ID/key/name) makes compatibility explicit without coupling it to browse taxonomy. Domain has no EF/Infrastructure dependencies. There is one Application handler, no per-group strategies, and no hard-coded sample exceptions in the query. Groups for unsupported catalog types are not pre-created.

## Response

HTTP 200 returns the usual `{ "data": { ... } }` envelope, not pagination:

| `data` field | Meaning |
| --- | --- |
| `comparisonGroup` | Shared `id`, `key`, and `name`. |
| `devices` | Exactly two existing device-card DTOs, in request order. |
| `differencesOnly` | The requested mode. |
| `specificationGroups` | Nonempty groups with `id`, `key`, `name`, `displayOrder`, and `specifications`. |

Each specification row contains `id`, `key`, `name`, lowercase `dataType`, nullable `unit`, `displayOrder`, `isDifferent`, and two `values` in the same order as `devices`. Each value contains `isMissing`, `valueText`, `valueNumber`, `valueBoolean`, and `valueDate`. A present value has exactly one populated typed field. A missing value has `isMissing: true` and all four typed fields null.

Rows are the **union of definitions assigned to either device**, filtered to `SpecificationDefinition.IsComparable == true`, and aligned by definition ID. Unassigned-to-both and opted-out definitions are omitted. Groups sort by display order then key; rows sort by definition display order then key. Row order is unchanged when device columns are reversed.

| Value type | Equality/difference rule |
| --- | --- |
| Text | Exact ordinal, case-sensitive equality; no parsing or semantic normalization. |
| Number | Exact decimal value equality (`1` equals `1.00`); no tolerance or unit conversion. |
| Boolean | Exact equality; `false` is present data. |
| Date | Exact calendar-date equality. |
| Missing on one side | Always different from a present value, including `0` and `false`. |

Units/types come from the shared definition, so values within a row have the same meaning. There is no score, winner, greater-is-better rule, or `ComparisonDirection`. Release/history/SEO/physical fields are not automatically converted into specification rows. On an unedited fresh seed the phone pair has 13 rows (10 different); the computer pair has 15 rows (all different). `announcement_date` remains available in normal detail/specification APIs but is deliberately opted out of comparison.

## Errors

The standard JSON `error` envelope and `X-Trace-Id` apply:

| Status/code | Condition |
| --- | --- |
| 400 `VALIDATION_ERROR` | Missing, duplicate, malformed, overlong, or wrong-count slugs; malformed boolean. |
| 404 `DEVICE_NOT_FOUND` | Either device does not exist or is Draft/Archived. No partial result identifies hidden content. |
| 400 `COMPARISON_UNAVAILABLE` | Both are public, but at least one has no comparison group. |
| 400 `INCOMPATIBLE_DEVICES` | Both are public and assigned, but to different groups. |
| 500 `UNEXPECTED_ERROR` | Unexpected failures, following the existing generic error contract. |

Visibility is checked before compatibility. For example a hidden phone paired with a computer returns the same generic 404 as a missing device, not its group or status. Phase 7 added no writes; [Phase 8 admin endpoints](ADMIN.md) now manage assignments, values, and comparable definition metadata.

## Persistence, migration, and seed safety

`AddCatalogComparisons` creates `ComparisonGroups` with unique snake-case keys, nullable `Devices.ComparisonGroupId` with a restrictive FK and index, and `SpecificationDefinitions.IsComparable` defaulting to false. New definitions are not comparable unless explicitly opted in; definitions can opt out without deleting their stored values.

The migration creates `phone` and `all_in_one`, then initializes the **new** metadata once:

- Only `nokia-3310`/`nokia-3210` under Nokia and `phones → feature-phones`, and `macintosh-128k`/`imac-g3` under Apple and `computers → all-in-one-computers`, receive assignments. Both roots must still be root categories. Other/reclassified records remain unassigned; publication state is not changed.
- The 28 known technical seed-definition keys become comparable. `announcement_date` and other keys remain false. Existing names, units, values, status, content, timestamps, IDs, and search vectors are untouched.
- Fresh sample inserts receive their group and new-definition flags. Existing sample slugs still skip the entire seed; existing reference labels and `IsComparable` choices are preserved. No reconciliation/upsert system, automatic repair, or seed-triggered backfill is added.

Existing correctly classified samples work after migration without reseeding. Existing unassigned records need an explicit reviewed metadata edit through the [Phase 8 admin API](ADMIN.md). Down removes only the Phase 7 schema but loses those new assignments/flags; rehearse on a disposable database, not valuable local/production data. Earlier migrations remain unchanged.

Verification: `dotnet build apps/api/TechVault.slnx` and `dotnet test apps/api/TechVault.slnx`; the focused filter is `FullyQualifiedName~Comparison`. Integration fixtures use real PostgreSQL containers, covering both pairs, typed/missing/equal values, visibility and metadata edits, non-overwriting reseeds, FK/unique constraints, and upgrade/down/up while preserving existing editorial rows. No packages, MediatR, Generic Repository, Unit of Work, scoring, three-device comparisons, or frontend changes are introduced.
