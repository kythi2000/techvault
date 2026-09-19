# Frontend Admin CMS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a secure, server-mediated single-editor CMS covering every Backend Phase 8 content workflow and Phase 9 operational error contract.

**Architecture:** An authenticated Next.js server boundary encrypts the backend API key in an expiring HttpOnly cookie. Server Components read through a schema-validated admin adapter; closed-union Server Actions perform mutations. Focused device and generic reference editors share presentation primitives without accepting arbitrary paths or methods.

**Tech Stack:** Next.js 16 App Router and Server Actions, React 19 `useActionState`, TypeScript strict mode, Web Crypto AES-GCM, Zod 4, Node test runner, project CSS.

**Spec:** `docs/superpowers/specs/2026-09-19-frontend-admin-cms-design.md`

## Global Constraints

- Never expose the admin API key or session secret through public environment variables, URLs, HTML, logs, action state, or client storage.
- `TECHVAULT_ADMIN_SESSION_SECRET` must decode from base64 to exactly 32 bytes.
- Every admin read and write independently requires a valid unexpired session.
- Admin requests are `no-store`, schema-validated, eight-second bounded, and path/method allowlisted.
- Preserve Backend Phase 8 full-replacement, immutable-field, typed-value, lifecycle, conflict, and pagination semantics.
- Do not edit backend files or unrelated dirty changes.

## Review Focus

- Tampered, expired, or wrongly configured sessions must fail closed without revealing secrets.
- Browser form fields must not be able to select an arbitrary backend path or HTTP method.
- Numeric zero and boolean false specifications must serialize as present single typed values.
- A backend `401`, `413`, `429`, or `409` must preserve safe recovery information and trace ID without echoing request content.
- Archived devices and immutable reference fields must not expose actionable write controls.

---

### Task 1: Encrypted admin session primitive

**Files:**
- Create: `apps/web/src/lib/admin-session-crypto.ts`
- Create: `apps/web/src/lib/admin-session.ts`
- Create: `apps/web/tests/admin-session.test.mjs`
- Modify: `apps/web/.env.example`

**Interfaces:**
- Produces: `sealAdminSession(apiKey, secret, now?)`, `openAdminSession(token, secret, now?)`, `readAdminSession`, `writeAdminSession`, and `clearAdminSession`.
- Consumes: Web Crypto and Next `cookies()` only in the cookie wrapper.

- [ ] **Step 1: Write crypto behavior tests**

```js
test("admin session round-trips then expires and rejects tampering", async () => {
  const secret = Buffer.alloc(32, 7).toString("base64");
  const token = await sealAdminSession("editor-key", secret, new Date("2026-09-19T00:00:00Z"));
  assert.equal((await openAdminSession(token, secret, new Date("2026-09-19T01:00:00Z"))).apiKey, "editor-key");
  assert.equal(await openAdminSession(token + "x", secret, new Date("2026-09-19T01:00:00Z")), null);
  assert.equal(await openAdminSession(token, secret, new Date("2026-09-20T00:00:00Z")), null);
});
```

- [ ] **Step 2: Run focused test and verify RED**

Run: `cmd /c npm test -- --test-name-pattern="admin session"` in `apps/web`  
Expected: FAIL because session modules do not exist.

- [ ] **Step 3: Implement AES-256-GCM token and cookie wrapper**

Token format is version byte + 12-byte random IV + ciphertext/tag encoded base64url. The encrypted JSON contains `{apiKey, expiresAt}`; expiry is eight hours. Cookie options are `httpOnly`, `sameSite: "strict"`, root path, `secure` in production, and matching max age.

- [ ] **Step 4: Run session tests**

Run: `cmd /c npm test -- --test-name-pattern="admin session"` in `apps/web`  
Expected: PASS including invalid base64 secret, wrong length, expiry, and tampering.

- [ ] **Step 5: Commit the task**

```text
feat(web): add encrypted admin sessions
```

### Task 2: Admin contracts, adapter, and form parsers

**Files:**
- Create: `apps/web/src/lib/admin-contracts.ts`
- Create: `apps/web/src/lib/admin-api.ts`
- Create: `apps/web/src/lib/admin-form-data.ts`
- Create: `apps/web/tests/admin-contracts.test.mjs`

**Interfaces:**
- Produces: all Phase 8 response/input types; admin GET/POST/PUT/DELETE functions; `parseDeviceForm`, `parseSpecificationForm`, and `parseReferenceForm`.
- Consumes: Task 1 session reader, public `ApiError`, and pagination schema.

- [ ] **Step 1: Write failing contract and parsing tests**

```js
test("typed specification form preserves false and zero", () => {
  assert.deepEqual(parseSpecificationForm(form({ dataType: "boolean", valueBoolean: "false" })), { valueBoolean: false });
  assert.deepEqual(parseSpecificationForm(form({ dataType: "number", valueNumber: "0" })), { valueNumber: 0 });
});

test("admin adapter returns retry delay without leaking bearer", async () => {
  const result = await adminListDevices("secret-value", { page: "1" });
  assert.equal(result.status, 429);
  assert.equal(result.retryAfterSeconds, 60);
  assert.doesNotMatch(JSON.stringify(result), /secret-value/);
});
```

- [ ] **Step 2: Run focused tests and verify RED**

Run: `cmd /c npm test -- --test-name-pattern="admin contract|typed specification|admin adapter"` in `apps/web`  
Expected: FAIL because contracts/parsers/adapter do not exist.

- [ ] **Step 3: Implement complete Zod schemas and closed adapter methods**

```ts
export type AdminReferenceKind = "brands" | "categories" | "specification-groups" | "specification-definitions";
export function adminListDevices(apiKey: string, params: AdminListParams): Promise<AdminResult<PaginatedData<AdminDeviceSummary>>>;
export function adminGetDevice(apiKey: string, id: string): Promise<AdminResult<AdminDeviceDetail>>;
export function adminSaveDevice(apiKey: string, id: string | null, input: AdminDeviceInput): Promise<AdminResult<AdminDeviceState>>;
```

Implement explicit methods for lifecycle/specification/reference operations; do not export a browser-controlled generic URL forwarder.

- [ ] **Step 4: Run all unit tests**

Run: `cmd /c npm test` in `apps/web`  
Expected: all tests PASS.

- [ ] **Step 5: Commit the task**

```text
feat(web): add admin API contracts
```

### Task 3: Admin login, shell, and dashboard

**Files:**
- Create: `apps/web/src/app/admin/actions.ts`
- Create: `apps/web/src/app/admin/layout.tsx`
- Create: `apps/web/src/app/admin/login/page.tsx`
- Create: `apps/web/src/app/admin/page.tsx`
- Create: `apps/web/src/components/admin/admin-login-form.tsx`
- Create: `apps/web/src/components/admin/admin-shell.tsx`
- Create: `apps/web/src/components/admin/admin-action-message.tsx`
- Create: `apps/web/src/app/admin/admin.css`
- Modify: `apps/web/src/app/globals.css`
- Modify: `apps/web/tests/smoke.mjs`

**Interfaces:**
- Consumes: Tasks 1–2 session and minimal list adapter.
- Produces: protected admin shell, login/logout actions, dashboard, and serializable `AdminActionState`.

- [ ] **Step 1: Add stateful smoke coverage for authentication**

```js
assert.match(await html("/admin"), /Unlock the editorial workspace/);
const rejected = await submitAdminLogin("wrong-key");
assert.match(rejected.body, /UNAUTHORIZED/);
const accepted = await submitAdminLogin(adminKey);
assert.match(accepted.headers.get("set-cookie"), /HttpOnly/);
assert.doesNotMatch(accepted.body + logs, new RegExp(adminKey));
```

- [ ] **Step 2: Build and run smoke to verify RED**

Run: `cmd /c npm run build` then `cmd /c npm run test:smoke` in `apps/web`  
Expected: FAIL because `/admin` and login action do not exist.

- [ ] **Step 3: Implement login/logout, protected shell, and dashboard**

Login verifies via `GET /admin/devices?page=1&pageSize=1`, seals the session, and redirects to `/admin`. Protected pages redirect to `/admin/login` without a valid session. All admin metadata is `noindex, nofollow` and dynamic/no-store.

- [ ] **Step 4: Rerun smoke**

Run: `cmd /c npm run test:smoke` in `apps/web`  
Expected: PASS for rejection, successful cookie session, protected navigation, logout, noindex, and secret non-disclosure.

- [ ] **Step 5: Commit the task**

```text
feat(web): add protected admin workspace
```

### Task 4: Device editor, lifecycle, and specifications

**Files:**
- Create: `apps/web/src/app/admin/devices/page.tsx`
- Create: `apps/web/src/app/admin/devices/new/page.tsx`
- Create: `apps/web/src/app/admin/devices/[id]/page.tsx`
- Create: `apps/web/src/app/admin/devices/actions.ts`
- Create: `apps/web/src/components/admin/admin-pagination.tsx`
- Create: `apps/web/src/components/admin/device-editor.tsx`
- Create: `apps/web/src/components/admin/device-lifecycle.tsx`
- Create: `apps/web/src/components/admin/specification-editor.tsx`
- Modify: `apps/web/tests/smoke.mjs`

**Interfaces:**
- Consumes: admin adapter/parsers/session and action state.
- Produces: complete device list/create/edit/lifecycle/specification UI.

- [ ] **Step 1: Add smoke cases for the editorial lifecycle**

```js
const created = await adminForm("/admin/devices/new", draftFields);
assert.equal(created.status, 303);
assert.equal(adminState.devices.at(-1).status, "draft");
await adminForm(`/admin/devices/${id}`, { intent: "set-specification", dataType: "boolean", valueBoolean: "false" });
assert.equal(adminState.devices.at(-1).specifications[0].valueBoolean, false);
await adminForm(`/admin/devices/${id}`, { intent: "publish" });
assert.equal(adminState.devices.at(-1).status, "published");
```

- [ ] **Step 2: Build/run smoke and verify RED**

Run: `cmd /c npm run build` then `cmd /c npm run test:smoke` in `apps/web`  
Expected: FAIL because device admin routes/actions are missing.

- [ ] **Step 3: Implement list, editor, lifecycle, and typed specification actions**

Use `useActionState` client forms for pending/error feedback. Redirect after successful creation/content/lifecycle writes. Keep specification mutation feedback local. Archived pages render values without write forms. Every destructive action validates a submitted confirmation.

- [ ] **Step 4: Rerun unit and smoke tests**

Run: `cmd /c npm test` then `cmd /c npm run test:smoke` in `apps/web`  
Expected: PASS for draft/full replacement/publish/unpublish/archive, typed values, validation, 409, 413, and 429.

- [ ] **Step 5: Commit the task**

```text
feat(web): add device editorial workflow
```

### Task 5: Reference managers

**Files:**
- Create: `apps/web/src/app/admin/references/[kind]/page.tsx`
- Create: `apps/web/src/app/admin/references/actions.ts`
- Create: `apps/web/src/components/admin/reference-manager.tsx`
- Create: `apps/web/src/lib/admin-reference-config.ts`
- Modify: `apps/web/tests/smoke.mjs`

**Interfaces:**
- Consumes: closed `AdminReferenceKind`, reference adapters/parsers, and admin session.
- Produces: CRUD UI for all four mutable reference resources.

- [ ] **Step 1: Add smoke cases for create/update/delete restrictions**

```js
await adminForm("/admin/references/brands", { intent: "create", name: "Test", slug: "test", description: "" });
assert.ok(adminState.brands.some(x => x.slug === "test"));
const conflict = await adminForm("/admin/references/brands", { intent: "delete", id: usedBrandId, confirm: "yes" });
assert.match(conflict.body, /REFERENCE_CONFLICT/);
```

- [ ] **Step 2: Build/run smoke and verify RED**

Run: `cmd /c npm run build` then `cmd /c npm run test:smoke` in `apps/web`  
Expected: FAIL because reference routes/actions are missing.

- [ ] **Step 3: Implement closed configuration and forms**

The configuration names fields, immutable-on-edit fields, option dependencies, and labels for each allowed kind. Unknown kinds call `notFound()`. Actions switch on the closed union and never concatenate untrusted resource paths.

- [ ] **Step 4: Run unit and smoke tests**

Run: `cmd /c npm test` then `cmd /c npm run test:smoke` in `apps/web`  
Expected: PASS for all resource contracts, immutable fields, comparison/group picklists, pagination, and conflict feedback.

- [ ] **Step 5: Commit the task**

```text
feat(web): manage admin reference data
```

### Task 6: Operational UX, documentation, and full verification

**Files:**
- Modify: `apps/web/src/lib/techvault-api.ts`
- Modify: public error components where applicable
- Modify: `apps/web/src/app/admin/admin.css`
- Modify: `apps/web/next.config.ts`
- Modify: `docs/FRONTEND_SPEC.md`
- Modify: `apps/web/README.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: all earlier tasks.
- Produces: documented Frontend Phases 6 and 8 plus consistent `Retry-After`/trace handling and final responsive styles.

- [ ] **Step 1: Add failing unit/smoke assertions for 413/429 and safe headers**

```js
assert.equal((await fetch(`${base}/admin`)).headers.get("x-content-type-options"), "nosniff");
assert.match(await rateLimitedAdminAction(), /Try again in 60 seconds/);
assert.match(await oversizedAdminAction(), /PAYLOAD_TOO_LARGE/);
```

- [ ] **Step 2: Run focused checks and verify RED**

Run: `cmd /c npm test` and `cmd /c npm run test:smoke` in `apps/web`  
Expected: FAIL on missing retry copy and frontend response headers.

- [ ] **Step 3: Implement operational presentation, headers, and docs**

Add `X-Content-Type-Options`, `Referrer-Policy`, and frame protection through Next config without a CSP that would break Next runtime scripts. Document session secret generation, admin run steps, route coverage, Phase 9 mapping, and remaining production limitations.

- [ ] **Step 4: Run fresh full verification**

Run in `apps/web`: `cmd /c npm run lint`, `cmd /c npm run typecheck`, `cmd /c npm test`, `cmd /c npm run build`, `cmd /c npm run test:smoke`. Run `git -c safe.directory=* diff --check -- apps/web docs/FRONTEND_SPEC.md README.md docs/superpowers`.  
Expected: every command exits 0 with no test failures or whitespace errors.

- [ ] **Step 5: Commit the task**

```text
docs(web): complete admin CMS phase
```

