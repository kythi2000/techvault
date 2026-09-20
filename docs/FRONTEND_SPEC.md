# TechVault Frontend Specification

Status: working specification  
Scope: public web application and private single-editor CMS in `apps/web`
Last updated: 2026-09-20

## 1. Purpose

The frontend turns the public catalog API into two connected experiences:

1. **Archive utility** — find a device, inspect its technical record, and move between brands, categories, and eras.
2. **Digital museum** — understand why an object mattered through editorial context and deliberate visual presentation.

The public frontend consumes Backend Phases 1–7, including the four-device catalog, search/timeline, and structured two-device comparison. The private CMS consumes Backend Phase 8 and maps relevant Backend Phase 9 transport/error controls into editorial recovery states. Media and collections still depend on future backend contracts.

## 2. Product principles

- **History before hype.** The tone is informed, calm, and editorial.
- **Emotion plus utility.** Device pages lead with the object and its story, then provide exact structured data.
- **Unknown means unknown.** Missing dates or specifications are displayed as unknown, never inferred as `0`, `false`, or a fabricated date.
- **The URL is shareable state.** Browse filters, sort, and pagination live in query parameters.
- **Server-render the public record.** Titles, descriptions, histories, and specifications must exist in initial HTML.
- **Enhance progressively.** Core navigation and GET filter forms work without client-side state libraries.
- **Do not manufacture empty SEO pages.** Only backed, meaningful routes are indexable.

## 3. Users and core journeys

### Curious visitor

```text
Home → Phones → filter by Nokia / 2000s → Nokia 3310 → history → specifications
```

### Search visitor

```text
Search engine → device detail or specs → brand/category context → another device
```

### Research-oriented visitor

```text
Archive → exact technical record → verify unknown values → later compare/timeline
```

## 4. Information architecture

| Route | Purpose | Rendering | API dependency | Initial status |
| --- | --- | --- | --- | --- |
| `/` | Museum-style entry and featured records | Dynamic SSR | `GET /devices` | Implemented |
| `/devices` | Complete published-device index | Dynamic SSR | `GET /devices`, `/brands`, `/categories` | Implemented |
| `/phones` | Phone subtree index | Dynamic SSR | `GET /phones`, `/brands`, `/categories` | Implemented |
| `/computers` | Computer subtree index | Dynamic SSR | `GET /computers`, `/brands`, `/categories` | Implemented |
| `/devices/{slug}` | Story-led device detail | Dynamic SSR | `GET /devices/{slug}` | Implemented |
| `/devices/{slug}/specs` | Complete technical record | Dynamic SSR | `GET /devices/{slug}` | Implemented |
| `/brands` | Published-maker index | Dynamic SSR | `GET /brands` | Implemented |
| `/brands/{slug}` | Brand overview and devices | Dynamic SSR | `GET /brands/{slug}`, `/devices?brand=` | Implemented |
| `/categories` | Ordered classification index | Dynamic SSR | `GET /categories` | Implemented |
| `/categories/{slug}` | Category overview and devices | Dynamic SSR | `GET /categories`, `/devices?category=` | Implemented |
| `/search?q=...` | Full-text results in API relevance order | Dynamic SSR | `GET /search` | Implemented |
| `/timeline` | Global/phone/computer/brand chronology via URL filters | Dynamic SSR + client scroll controls | `GET /timeline`, `/brands`, `/categories` | Implemented |
| `/compare` | Ordered two-device comparison with URL state | Dynamic SSR + client selection | `GET /compare` | Implemented |
| `/museum`, `/collections/*` | Curated stories | ISR | Future content APIs | Phase 7 |
| `/admin/login`, `/admin` | Editor authentication and workspace index | Dynamic SSR + Server Actions | Backend Phase 8 | Implemented |
| `/admin/devices`, `/admin/devices/new`, `/admin/devices/{id}` | Device content, lifecycle, and typed specifications | Dynamic SSR + Server Actions | Backend Phases 8–9 | Implemented |
| `/admin/references/{kind}` | CRUD for four mutable reference resources | Dynamic SSR + Server Actions | Backend Phases 8–9 | Implemented |

`/devices/{slug}/specs` uses the detail endpoint for now because it already contains the same ordered specification groups. The dedicated specifications endpoint remains available if payload separation becomes useful later.

## 5. Visual direction

The identity is a contemporary archive rather than a conventional e-commerce catalog:

- warm paper background and near-black ink;
- signal red, archive green, and oxidized blue accents;
- readable system sans-serif typography, with monospace reserved for accession codes and object annotations;
- visible grids, accession numbers, rules, and object annotations;
- asymmetrical museum layouts with restrained motion;
- no cyberpunk glow, glass-card wall, fake pricing, or invented product photography.

Until Media APIs exist, device art is explicitly labeled as an archival placeholder. It must not be presented as a historically accurate product image. Real assets later require descriptive alt text, credit, license, dimensions, and responsive sources.

### Typography

- Use the local Segoe UI/system sans-serif stack for headings, navigation, and body text; no font download is required.
- Body and form controls start at 16px; introductory and long-form copy use 17–20px with generous line spacing.
- Navigation and actions use 15px text in natural case. Supporting labels use 13–14px, including on mobile.
- Headings use semibold weight, moderate letter spacing, and line heights of at least 1.08; emphasis uses color without italic styling.
- Scale the hero down to 40–56px on mobile and switch to compact navigation before menu labels become crowded.

### Responsive behavior

- Desktop: two-column hero, left filter rail, three-column catalog.
- Tablet: stacked hero, two-column catalog, inline filter grid.
- Mobile: compact navigation, single-column catalog and specification rows.
- Touch targets should be at least 40×40 CSS pixels; primary actions target 48 pixels.
- Meaning must not depend on hover or animation.

## 6. Frontend architecture

### Stack

- Next.js 16 App Router
- React 19
- TypeScript in strict mode
- Tailwind CSS 4 plus project-level CSS tokens
- Zod for runtime API-contract validation
- Server Components by default
- TanStack Query only when interactive client-owned data is introduced
- Zustand only for genuinely cross-route transient state such as a future compare tray
- Native forms and React 19 `useActionState` for current admin workflows

TanStack Query, Zustand, and React Hook Form remain intentionally uninstalled. Server Components own reads and Server Actions own writes; native form parsing and backend validation cover the current single-editor workflows without a second client cache or form abstraction.

### Folder ownership

```text
apps/web/src/
├── app/                 # routes, metadata, route loading/error/404 boundaries
├── components/          # shared presentational and feature composition
└── lib/
    ├── contracts.ts     # Zod schemas and inferred public types
    ├── techvault-api.ts # server-side HTTP adapter and Result contract
    ├── browse-query.ts  # URL-state whitelist and serialization
    ├── discovery-query.ts # separate search and timeline query contracts
    └── format.ts        # display-only formatting
```

Rules:

- Components do not assemble API base URLs.
- API envelopes are parsed once in `lib`; components receive a discriminated result.
- Expected backend failures are values, not render exceptions.
- A device `404` invokes the route not-found boundary; API outage and contract mismatch render recoverable status UI.
- Do not mirror backend domain entities or add client repositories. Types model only the public JSON contract.

## 7. Data and state strategy

### Server data

Public pages call ASP.NET from Server Components using `TECHVAULT_API_URL`. The first implementation uses uncached request-time reads because publishing invalidation does not exist yet. A later production phase may add time-based or tag-based caching after freshness rules are explicit.

### Browse state

Accepted URL state mirrors the API:

```text
brand, category, type, year, fromYear, toYear, decade, sort, page, pageSize
```

The UI exposes brand, category, decade, and sort, with type, year/range, and page size under additional filters. Unknown keys are not forwarded.

### Search and timeline state

- Search accepts only `q`, `page`, and `pageSize`. An initial `/search` visit prompts for a query without calling the API. An explicitly submitted empty/invalid query displays the backend validation error.
- Search uses complete words, matches all supplied words, and retains API relevance order. It does not offer unsupported sort, taxonomy filters, autocomplete, fuzzy matching, or client-side ranking.
- Timeline accepts only `brand`, `category`, `type`, `year`, `fromYear`, `toYear`, `era`, `page`, and `pageSize`. A single `/timeline` route covers global and scoped views; e.g. `/timeline?type=phones`, `/timeline?brand=nokia`, `/timeline?era=1990s`.
- `era` is forwarded as the backend decade label (`0010s` through `9990s`). Browse-only `decade` and `sort` are never forwarded to timeline.
- Both views default to 12 records per page, retain filters in pagination links, reset to the first page on form submission, and distinguish no matches from a page beyond the last result.
- Timeline preserves API order: release year, exact release date (unknown last), then slug. A year-only record displays an unknown exact date; FE never invents January 1. The timeline schema rejects a missing release year.
- Timeline cards are server-rendered in a horizontally scrollable region on desktop and a vertical sequence on mobile. A small client component adds Earlier/Later buttons, tracks scroll limits, and honors reduced motion; native scrolling, keyboard access, filters, and pagination work without client state libraries.
- On taxonomy API failure, current selected values remain in filter controls and a notice is shown. Discovery data can still render independently.

### Admin session and mutations

- `/admin/login` sends the API key to a same-origin Server Action. The browser never calls the ASP.NET admin API directly.
- Next.js verifies the key with a minimal authenticated read, encrypts it with AES-256-GCM, and stores only the encrypted token in an eight-hour `HttpOnly`, `SameSite=Strict` cookie. `TECHVAULT_ADMIN_SESSION_SECRET` is server-only base64 for exactly 32 bytes.
- Every protected Server Component and Server Action independently opens the session. Missing, expired, corrupt, or unauthorized sessions fail closed and return to login.
- A backend `401` from a cryptographically valid session enters an explicit re-authentication route, so API-key rotation cannot trap the editor in a login redirect loop.
- Admin adapters use explicit methods and a four-value reference-resource union. Browser fields cannot select an arbitrary backend URL or HTTP method.
- Device PUT is a full replacement; lifecycle and typed specification operations remain separate. `false` and `0` serialize as present values. Archived records expose no write controls.
- Reference editing includes immutable values in full PUT bodies while disabling those controls. Deletion requires confirmation and surfaces reference conflicts without cascade behavior.
- Validation and conflict action states retain only allowlisted submitted fields and remount hydrated forms with those values; the admin workspace requires JavaScript for this inline recovery path.
- `401`, `409`, `413`, and `429` remain structured. Rate limits preserve safe `Retry-After` seconds, payload limits use concise recovery copy, and trace IDs remain visible without echoing request content.

### Runtime validation

TypeScript protects code written in this repository; it cannot prove the response sent by another process. Zod therefore validates success envelopes, error envelopes, dates, UUIDs, pagination metadata, nullable values, and discriminated specification values at the boundary.

Contract mismatch behavior:

```text
HTTP 2xx + invalid JSON shape → frontend 502-style result → safe error UI
network failure              → frontend 503-style result → safe error UI
API error envelope           → preserve status, code, message, traceId
device 404                   → Next.js not-found boundary + noindex
```

## 8. API-to-UI mapping

### Card

| API field | UI use |
| --- | --- |
| `id` | Stable React key and short accession number |
| `name`, `slug` | Heading and canonical detail link |
| `shortDescription` | Three-line card summary |
| `brand` | Maker label and brand archive link |
| `category` | Classification and visual variant |
| `releaseYear` | Era label; unknown remains unknown |

### Detail

- `seoTitle` and `seoDescription` feed dynamic metadata with safe content fallbacks.
- `description` is the overview; `history` is the editorial story.
- `physicalDetails` becomes key facts only when values exist.
- `specificationGroups` are rendered in API order; FE does not reclassify definitions.
- `publishedAt` and `updatedAt` may support structured data and record-review labels.

### Typed specifications

```text
text    → valueText
number  → localized valueNumber + unit
boolean → Yes / No (null remains Unknown)
date    → stable UTC calendar formatting
```

## 9. UX states

Browse data surfaces need:

- a layout-matched loading skeleton;
- a successful state;
- a genuine empty state that suggests widening filters;
- a validation/API error state with public message and trace reference;
- an unpublished/missing `404` that does not leak status;
- keyboard-visible focus and reduced-motion support.

Device detail deliberately resolves existence before streaming its body so a missing object keeps a real HTTP 404 instead of becoming a soft 404. The homepage remains useful when the API is offline: it keeps its positioning and collection entry points, while clearly stating that live records require the local catalog service.

## 10. SEO contract

Initial requirements:

- semantic server-rendered headings, descriptions, history, and specification lists;
- unique page title and description;
- canonical URL without browse query parameters;
- `noindex` for not-found pages;
- `noindex, follow` for search results and filtered/paginated timeline views; canonical URLs remain `/search` and `/timeline` respectively;
- meaningful internal links between browse, detail, and specs;
- metadata never blocks public content for normal visitors.

Deferred to the dedicated SEO phase:

- generated Open Graph images;
- JSON-LD policy and validation;
- production sitemap/robots fed by all published slugs;
- alternate languages;
- revalidation webhook on publish;
- Core Web Vitals budgets enforced in CI.

## 11. Accessibility requirements

- WCAG 2.2 AA is the target for public flows.
- One primary `h1` per page and sequential content headings.
- A skip link precedes repeated navigation.
- Navigation, breadcrumbs, pagination, status messages, and loading state have programmatic labels.
- Form labels remain visible; placeholders never replace labels.
- Color contrast is checked for text and focus indicators.
- Decorative object studies and diagrams are hidden from assistive technology.
- Real media must use editorial alt text; credit text is separate from alt text.
- Reduced-motion preference disables nonessential transitions and shimmer motion.

## 12. Performance and security budgets

Initial budgets:

- No page-wide Client Component boundary.
- No global client state or query provider until required.
- No remote font request in the critical path.
- Public record content is present in HTML.
- Avoid layout shift by reserving object/media aspect ratios.
- Forward no browser credentials to the public API.
- Never place admin secrets in `NEXT_PUBLIC_*` variables.
- Never place the admin API key in a URL, public bundle, `localStorage`, logs, or tracked environment file.
- Admin and public responses set `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and `Referrer-Policy: strict-origin-when-cross-origin`.
- Render editorial text as text; do not use unsanitized `dangerouslySetInnerHTML`.

Target production checks: LCP ≤ 2.5 s, CLS ≤ 0.1, INP ≤ 200 ms at the 75th percentile on representative mobile traffic.

## 13. Testing strategy

### Required before release

- `npm run lint`
- `npm run typecheck`
- `npm run build`
- production HTTP smoke checks for home, browse, taxonomy, search, timeline, URL filters, mixed phone/computer data, empty results, detail, specs, and missing routes;
- desktop and mobile visual inspection;
- no unexpected browser console errors.

### Implemented with stable synthetic fixtures

- Node tests for browse/discovery query whitelists and serialization, typed specification formatting, API errors, timeline year validation, and paged taxonomy references;
- production-server smoke tests for SSR content, typed values, search relevance order, timeline chronology/date precision, filter preservation, pagination, metadata, errors, and hard 404 responses.

### Deferred test automation

- component tests for interactive states;
- Playwright journeys against a seeded disposable API;
- automated accessibility checks and mobile/desktop screenshot regression.

### Verification snapshot — 2026-09-20

- Lint, generated route types, strict TypeScript, unit/contract tests, production build, and HTTP smoke suite pass.
- Browser-driven visual/console inspection is still required before release; the current Codex browser runtime could not initialize because its sandbox policy metadata was unavailable.
- The HTTP smoke suite uses a temporary stateful contract-compatible API with synthetic mixed-catalog and admin records. It covers login/logout and rotated-key re-authentication, cookie flags, device create/update/lifecycle/specifications and archived controls, CRUD for all four reference resources, conflicts, read/write/delete `429`, `413` recovery, safe headers, and absence of the API key from returned HTML/logs. Unit coverage verifies allowlisted failed-form retention. A live API was not listening on the default local port during verification; end-to-end checks against the migrated/seeded database remain a local integration check.

## 14. Delivery phases

### Phase 1 — Foundation — implemented

Deliver the Next.js/React/TypeScript/Tailwind scaffold; global shell, navigation, footer, design tokens, responsive system; loading, error, and not-found boundaries; and local quality scripts.

Acceptance: the app runs without remote fonts, works at mobile/desktop widths, and builds without the API being available at build time.

### Phase 2 — Catalog data layer and browse — implemented

Deliver the runtime-validated API adapter; `/devices`, `/phones`, `/computers`; GET filters, sort, pagination, counts, empty/outage states; and the museum-style homepage connected to published devices.

Acceptance: all list data is server rendered, URL filters survive sharing/reload, and `/computers` treats no records as a valid empty state.

### Phase 3 — Device vertical slice — implemented

Deliver `/devices/{slug}`, `/devices/{slug}/specs`, dynamic metadata, canonical links, breadcrumbs, typed values, and safe 404 behavior.

Acceptance: Nokia 3310 renders from the seeded database with ordered typed specifications; unpublished and unknown slugs share the same public 404.

### Phase 4 — Taxonomy landing pages — implemented

Deliver brand and category indexes/detail pages, meaningful internal linking, pagination, hard 404 handling, and route-level metadata. Empty taxonomy detail pages remain usable but are marked `noindex`; empty brands are omitted from the maker index.

Dependency satisfied by the current public brand/category/device APIs.

### Phase 5 — Search and timeline — implemented

Deliver `/search`, `/timeline`, dedicated query whitelists and API adapters, URL-driven GET forms, pagination, recoverable errors, empty states, metadata, and discovery links from home/navigation/catalog/taxonomy pages. The horizontal desktop timeline becomes a vertical sequence on mobile.

Dependency satisfied by Backend Phase 6 search/timeline endpoints and Backend Phase 5 mixed sample data. Comparison follows as a separate implemented slice below.

### Phase 6 — Comparison — implemented

Deliver `/compare`, URL-ordered two-object selection, compatibility feedback, a server-rendered result table, aligned specification groups, differences-only mode, and explicit missing values. Device detail, primary navigation, and the footer provide entry points. A single valid `devices` slug preselects the first object without calling the comparison endpoint; a complete pair is passed to the backend unchanged.

Dependency satisfied by Backend Phase 7 comparison contract and `AddCatalogComparisons`. The frontend preserves backend row/column order, validates two values per row, and never adds scoring, winners, unit conversion, or category-based compatibility inference.

### Phase 7 — Museum and collections

Deliver `/museum`, curated collections, era stories, and progressive media treatment. 3D stays optional and lazy-loaded behind a static fallback.

Dependency: content/media/collection contracts and licensed assets.

### Phase 8 — Admin CMS — implemented

Deliver a protected single-editor workspace with an encrypted eight-hour session, contract-validated server-side reads, and allowlisted Server Actions. It covers device list/create/full replacement, publish/unpublish/archive, typed specification save/remove, and CRUD for brands, categories, specification groups, and specification definitions. Comparison groups remain read-only picklist data. Archived devices are read-only; immutable reference metadata is locked after creation; destructive operations require confirmation.

Dependency satisfied by Backend Phase 8 authentication and mutation APIs. Accounts, concurrent approval/version history, media, bulk import, restore, and preview infrastructure remain outside the backend contract and are not simulated by the frontend.

### Phase 9 — SEO, performance, and observability — operational baseline implemented

The Backend Phase 9-facing baseline is implemented: safe `413`/`429` recovery with trace/retry metadata, strict parsing of `Retry-After`, noindex admin metadata, and global nosniff/frame/referrer headers. Remaining work is structured data, sitemap, robots, generated social images, caching/invalidation, Web Vitals reporting, analytics consent decision, and automated accessibility/performance budgets.

Dependency: production URL, publish invalidation policy, and complete metadata/media contracts.

### Phase 10 — Production delivery

Deliver frontend container, Nginx routing, CI quality gates, environment validation, health-aware deployment, smoke tests, and rollback instructions.

Dependency: backend deployment topology and real domain.

## 15. Definition of done

A frontend phase is done only when:

- its route works from real API data and direct navigation;
- desktop and mobile layouts have been inspected;
- loading, empty, expected error, and missing states are intentional;
- semantic HTML and keyboard navigation are usable;
- metadata/canonical behavior matches the route's indexability;
- lint, type-check, and production build pass;
- documentation and environment examples match the code;
- deferred functionality is labeled, not faked.
