# Frontend Admin CMS Design

**Date:** 2026-09-19  
**Status:** approved for implementation  
**Backend dependencies:** Phases 8–9 `/api/v1/admin/*` and operational error contracts

## Intent

Provide a private, single-editor CMS for the complete Phase 8 admin contract while respecting the backend's credential, lifecycle, typing, and immutability rules. Phase 9 affects transport safety and error handling; it does not justify a fictional operations dashboard.

The CMS covers devices, lifecycle operations, typed specifications, brands, categories, specification groups, specification definitions, and the read-only comparison-group picklist. Media, accounts, approval workflows, bulk import, collections, restore, and deployment controls remain out of scope.

## Security and session model

The browser never calls the ASP.NET admin API directly. It submits the API key once to a Next.js Server Action over the current origin. The server verifies the key with a minimal authenticated admin read, then stores an AES-256-GCM encrypted session payload in an `HttpOnly`, `SameSite=Strict` cookie. The payload contains the API key and an absolute expiry no later than eight hours after login.

`TECHVAULT_ADMIN_SESSION_SECRET` is a server-only base64 value decoding to exactly 32 bytes. A missing or invalid secret disables login with a configuration-safe message. The key and secret never appear in `NEXT_PUBLIC_*`, URLs, action state, HTML, logs, screenshots, or error messages. Production cookies are `Secure`; development loopback HTTP remains usable. Logout deletes the cookie. An expired or corrupt cookie is treated as unauthenticated.

Every protected read and mutation independently requires a valid session. Server Components perform reads; Server Actions perform writes. Server Actions use an explicit resource/action allowlist and construct backend paths themselves. Browser-supplied URLs or methods are never forwarded. Same-origin Server Actions plus `SameSite=Strict` provide the request boundary; destructive actions additionally require an explicit confirmation field.

## Information architecture

Routes:

```text
/admin/login
/admin
/admin/devices
/admin/devices/new
/admin/devices/{id}
/admin/references/brands
/admin/references/categories
/admin/references/specification-groups
/admin/references/specification-definitions
```

All admin routes are dynamic, `no-store`, and `noindex, nofollow`. They use a compact editorial shell separate from public navigation while retaining TechVault's readable archive typography. The dashboard links to devices and all reference managers; it is not an analytics surface.

## Device workflow

The device list supports the backend's `draft`, `published`, and `archived` status filter plus bounded pagination. New-device creation saves a draft. The editor form exposes every `DeviceInput` field and loads brand, category, and comparison-group picklists from authenticated endpoints. Aliases use one value per line and are submitted as an array. Unknown dates and measurements remain null.

Editing is a full content replacement, matching PUT semantics. The UI warns that slug changes do not redirect old URLs. Archived records render read-only. Lifecycle buttons reflect valid transitions:

- draft: publish or archive;
- published: unpublish or archive;
- archived: no content/specification/lifecycle writes.

Publishing errors stay on the editor with the backend validation message and trace ID. Archive requires a confirmation checkbox and states that it cannot be restored through the current API.

The specification editor joins all definitions with current assignments. Each definition renders one input matching `text`, `number`, `boolean`, or `date`; false and zero remain valid present values. Save sends exactly one typed property. Remove represents unknown and requires confirmation. Definition metadata, unit, and comparison opt-in are visible.

## Reference workflow

One shared reference manager is configured by a closed resource union. It renders resource-specific fields and uses the exact backend contracts:

- brand: name, immutable slug, description;
- category: name, immutable slug/parent, display order, description;
- specification group: name, immutable key, display order;
- specification definition: name, immutable key/data type/unit, group, display order, comparable flag.

Creation allows all creation-time fields. Editing includes immutable values in PUT bodies but disables changes in the UI. Deletion is physical only for unreferenced records, requires confirmation, and preserves `REFERENCE_CONFLICT` feedback. Comparison groups are read-only and appear only as device picklist data.

## Backend adapter and operational errors

Admin response schemas are separate from public schemas and validate every read/mutation. The adapter applies an eight-second timeout, `Accept: application/json`, JSON content type for bodies, `cache: no-store`, and the decrypted bearer credential. It never logs request headers or bodies.

Errors remain structured as `{status, code, message, traceId, retryAfterSeconds?}`. `401` returns the editor to login; `403` reports policy denial; `413` reports the configured body limit; `429` displays the retry delay; `409` keeps form data and explains the conflict; malformed success/error payloads become safe frontend gateway errors. Mutation success redirects to a stable admin route with a short non-sensitive status message.

## Testing

Pure tests cover session encryption/decryption/expiry/tamper handling, form-to-contract parsing, explicit action/resource allowlists, typed specification serialization, Zod contracts, `Retry-After`, and secret-free error states. HTTP smoke tests use a stateful contract-compatible admin fixture and cover login rejection/success, cookie flags, protected redirects, draft creation/editing, lifecycle transitions, typed false/zero values, reference CRUD/conflicts, logout, noindex metadata, and absence of the API key from HTML/log output.

The final verification is lint, generated route types plus TypeScript, unit tests, production build, production SSR/action smoke tests, and browser-level visual/keyboard checks when the in-app browser is available.

