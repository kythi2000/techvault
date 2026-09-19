# TechVault Web

The public Next.js frontend for the TechVault digital technology archive. Its current public scope implements Frontend Phases 1–6: foundation, catalog browse, device detail/specifications, taxonomy pages, search/timeline, and comparison backed by Backend Phases 1–7. See [the frontend specification](../../docs/FRONTEND_SPEC.md).

## Run locally

Start the migrated and seeded API at `http://localhost:5078`, then run:

```powershell
cd apps/web
npm install
npm run dev
```

Open `http://localhost:3000`. Server Components use `TECHVAULT_API_URL`, which defaults to `http://localhost:5078`.

Copy `.env.example` in this directory to `.env.local` when local values need to be overridden:

```dotenv
TECHVAULT_API_URL=http://localhost:5078
NEXT_PUBLIC_SITE_URL=http://localhost:3000
```

`TECHVAULT_API_URL` is server-only. Never expose admin credentials with a `NEXT_PUBLIC_` prefix.

## Search and timeline

Apply the backend `AddCatalogDiscovery` migration and run the catalog seed as described in the [root README](../../README.md). With the API running, try:

- `/search?q=Nokia` — relevance-ordered results; search uses complete words, not partial names or typo correction.
- `/timeline` — known release years, earliest first.
- `/timeline?type=computers&brand=apple` — computer history for one maker.
- `/timeline?era=1990s` — a decade spanning both device types.
- `/timeline?fromYear=1980&toYear=2000&pageSize=2` — year range with URL-preserving pagination.

Search accepts `q`, `page`, and `pageSize`; timeline accepts brand/category/type/year/range/era filters and pagination. Forms reset the page when filters change. Timeline shows unknown exact dates explicitly and excludes records whose release year is unknown. Desktop supports native scrolling and Earlier/Later controls; mobile uses a vertical timeline.

## Comparison

Apply the backend `AddCatalogComparisons` migration before using comparison. Open `/compare`, or start from a device detail page. A completed comparison uses a shareable URL such as:

```text
/compare?devices=nokia-3310,nokia-3210&differencesOnly=true
```

The two slugs determine column order. Compatibility, comparable definitions, missing values, and differences come from the API; the frontend does not score devices or infer compatibility from categories.

## Quality checks

```powershell
npm run lint
npm run typecheck
npm test
npm run build
npm run test:smoke
```

The web build does not require a live API. Pages use request-time server rendering and show a deliberate unavailable state when the catalog cannot be reached. `test:smoke` starts a temporary contract-compatible API and the production Next.js server; run it after `build`.
