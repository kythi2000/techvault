# TechVault Web

The Next.js frontend for the TechVault digital technology archive. Its public scope implements Frontend Phases 1–6, and its private editorial workspace implements the Backend Phase 8 content workflows plus Backend Phase 9 operational error handling. See [the frontend specification](../../docs/FRONTEND_SPEC.md).

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
TECHVAULT_ADMIN_SESSION_SECRET=<base64-for-exactly-32-random-bytes>
```

`TECHVAULT_API_URL` and `TECHVAULT_ADMIN_SESSION_SECRET` are server-only. Never expose either secret or the admin API key with a `NEXT_PUBLIC_` prefix.

## Editorial workspace

The CMS is available at `/admin`. It covers device drafts and full content replacement, publish/unpublish/archive, typed specification values, and CRUD for brands, categories, specification groups, and specification definitions. Comparison groups are authenticated read-only picklist data.

Configure the API process with `Admin__ApiKey` as documented in the [root README](../../README.md). Configure a separate Next.js session-encryption secret before starting the web app:

```powershell
$sessionBytes = New-Object byte[] 32
$sessionRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$sessionRng.GetBytes($sessionBytes)
$sessionRng.Dispose()
$env:TECHVAULT_ADMIN_SESSION_SECRET = [Convert]::ToBase64String($sessionBytes)
npm run dev
```

Enter the API's existing admin key on `/admin/login`; do not generate a second API key there. The browser sends it only to a same-origin Server Action. Next.js verifies it server-side and keeps it in an AES-256-GCM encrypted, `HttpOnly`, `SameSite=Strict` cookie that expires after eight hours. The API key is not stored in `localStorage`, a public environment variable, a URL, or client JavaScript.

If the API key is rotated or revoked, the next protected read opens a re-authentication form instead of redirecting in a loop. JavaScript should remain enabled in the editorial workspace so validation and conflict responses can restore only the submitted allowlisted fields inline.

This remains a trusted single-editor workflow, not an account or approval system. Use HTTPS outside loopback, keep both secrets in a private secret store, rotate them deliberately, and do not enable request logging that captures form bodies or authorization headers.

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

The web build does not require a live API. Pages use request-time server rendering and show a deliberate unavailable state when the catalog cannot be reached. `test:smoke` starts a stateful contract-compatible API and the production Next.js server; it covers public SSR plus admin authentication and key-rotation recovery, session flags, device lifecycle/specifications and archived controls, CRUD for all four reference resources, conflicts, read/write/delete rate limits, 413 recovery, security headers, and secret non-disclosure. Run it after `build`.
