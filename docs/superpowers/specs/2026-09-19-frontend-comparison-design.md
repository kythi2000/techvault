# Frontend Comparison Design

**Date:** 2026-09-19  
**Status:** approved for implementation  
**Backend dependency:** Phase 7 `GET /api/v1/compare`

## Intent

Add a public, shareable two-device comparison that presents the backend's structured comparison faithfully. The frontend must preserve device column order, distinguish missing values from false/zero values, and never invent winners, scores, unit conversions, or compatibility rules.

## Route and state

The canonical route is `/compare`. Its shareable state is:

- `devices=<left-slug>,<right-slug>` for exactly two ordered selections;
- `differencesOnly=true` only when enabled.

Unsupported query parameters are discarded before calling the API. The frontend does not silently repair duplicate, malformed, or incompatible selections; submitted invalid state is forwarded to the backend so its documented error is shown. Opening `/compare` without `devices` renders the selector and guidance without calling the comparison endpoint.

The selector loads all published device cards through the paginated public catalog. A small client component combines two selects into the backend-compatible `devices` value and navigates to the shareable URL. The comparison result itself remains server-rendered.

## Rendering

The page shows the comparison group, two device summaries in request order, and specification groups/rows in API order. Typed values render as follows:

- text: unchanged;
- number: localized number plus the definition unit when present;
- boolean: `Yes` or `No`, including a present `false`;
- date: the existing human-readable date formatter;
- missing: `Unknown`.

Different rows receive a text label and visual accent; color is never the only signal. `differencesOnly=true` is represented by a checked control and does not remove the two selected device headers. An empty filtered result explains that no differing comparable values remain.

On narrow screens, the table retains its semantic table structure inside a horizontally scrollable region with sticky row labels. The public header, relevant device pages, and footer provide entry points to comparison.

## Contracts and errors

Zod validates the complete comparison envelope, including exactly two devices, exactly two values per row, and exactly one typed value for present values. API order is preserved.

The page gives tailored recovery for:

- `VALIDATION_ERROR`: correct the selection;
- `DEVICE_NOT_FOUND`: one selected object is no longer public;
- `COMPARISON_UNAVAILABLE`: comparison metadata is missing;
- `INCOMPATIBLE_DEVICES`: select devices from the same comparison group;
- `RATE_LIMITED`: retain selection and show `Retry-After` when supplied;
- transport, malformed-contract, and unexpected errors: retry and show trace ID.

## SEO and accessibility

The base `/compare` page has a canonical URL and descriptive metadata. Any query-driven comparison is `noindex, follow` to avoid generating a combinatorial index. Both selects have persistent labels, errors are announced, table headers identify row and device columns, and the interface works by keyboard.

## Testing

Unit/contract tests cover query whitelisting and encoding, response order, false/zero/missing values, malformed response rejection, error propagation, and rate-limit metadata. The production SSR smoke suite covers initial state, a valid phone comparison, reversed columns, differences-only mode, incompatible/missing cases, metadata, and navigation entry points.

