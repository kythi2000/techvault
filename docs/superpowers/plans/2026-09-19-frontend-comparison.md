# Frontend Comparison Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a server-rendered, shareable, accessible two-device comparison matching Backend Phase 7.

**Architecture:** A whitelist query module owns URL state, the server-only TechVault adapter parses the comparison envelope, and a small client selector navigates to the shareable URL. The result table remains server-rendered and preserves backend ordering and typing.

**Tech Stack:** Next.js 16 App Router, React 19, TypeScript strict mode, Zod 4, Node test runner, project CSS.

**Spec:** `docs/superpowers/specs/2026-09-19-frontend-comparison-design.md`

## Global Constraints

- Use the existing readable archive visual system and system fonts.
- Never infer compatibility, winners, scores, missing values, or converted units.
- Keep the ordered device selection in `devices=a,b`; allow only `differencesOnly=true`.
- Server-render result content and validate every backend payload with Zod.
- Do not alter backend files or unrelated dirty worktree changes.

## Review Focus

- Reversed device slugs must reverse columns without changing specification row order.
- Present `false` and numeric `0` must not render as missing.
- An absent `devices` query must not call `/api/v1/compare`.
- Malformed two-column/value responses must fail closed with a safe contract error.
- `429` must keep the comparison state and expose a valid integer `Retry-After` delay.

---

### Task 1: Comparison contracts, URL state, and API adapter

**Files:**
- Create: `apps/web/src/lib/comparison-query.ts`
- Modify: `apps/web/src/lib/contracts.ts`
- Modify: `apps/web/src/lib/techvault-api.ts`
- Create: `apps/web/tests/comparison.test.mjs`

**Interfaces:**
- Produces: `normalizeComparisonQuery`, `toComparisonQuery`, `compareDevices`, `getAllDevices`, `ComparisonResponse`, and typed comparison schemas.
- Consumes: existing `ApiResult`, `DeviceCard`, `apiResponseSchema`, and public browse endpoint.

- [ ] **Step 1: Write failing query and adapter tests**

```js
test("comparison keeps ordered slugs and one boolean option", () => {
  assert.deepEqual(normalizeComparisonQuery({ devices: ["nokia-3310,nokia-3210", "ignored"], differencesOnly: "true", page: "2" }),
    { devices: "nokia-3310,nokia-3210", differencesOnly: "true" });
  assert.equal(toComparisonQuery({ devices: "imac-g3,macintosh-128k", differencesOnly: "false" }),
    "?devices=imac-g3%2Cmacintosh-128k&differencesOnly=false");
});

test("comparison adapter preserves false zero missing and response order", async () => {
  const result = await compareDevices({ devices: "nokia-3310,nokia-3210" });
  assert.equal(result.ok, true);
  assert.deepEqual(result.data.devices.map(x => x.slug), ["nokia-3310", "nokia-3210"]);
  assert.equal(result.data.specificationGroups[0].specifications[0].values[0].valueBoolean, false);
  assert.equal(result.data.specificationGroups[0].specifications[1].values[0].valueNumber, 0);
  assert.equal(result.data.specificationGroups[0].specifications[1].values[1].isMissing, true);
});
```

- [ ] **Step 2: Run the focused test and verify RED**

Run: `cmd /c npm test -- --test-name-pattern=comparison` in `apps/web`  
Expected: FAIL because the comparison module and exports do not exist.

- [ ] **Step 3: Implement the minimal query, schemas, and adapters**

```ts
export interface ComparisonParams { devices?: string; differencesOnly?: string }
export function toComparisonQuery(params: ComparisonParams): string;
export function compareDevices(params: ComparisonParams): Promise<ApiResult<ComparisonResponse>>;
export function getAllDevices(): Promise<ApiResult<DeviceCard[]>>;
```

The value schema refines `isMissing` against exactly one or zero populated typed fields. The response schema requires exactly two devices and two values per row. `getAllDevices` follows pagination up to the backend maximum.

- [ ] **Step 4: Run focused and complete unit tests**

Run: `cmd /c npm test` in `apps/web`  
Expected: all tests PASS with the new comparison contract cases.

- [ ] **Step 5: Commit the task**

```text
feat(web): add comparison data contract
```

### Task 2: Comparison selector and server-rendered result

**Files:**
- Create: `apps/web/src/components/comparison-selector.tsx`
- Create: `apps/web/src/components/comparison-table.tsx`
- Create: `apps/web/src/components/comparison-error.tsx`
- Create: `apps/web/src/app/compare/page.tsx`
- Modify: `apps/web/src/app/discovery.css`
- Modify: `apps/web/tests/smoke.mjs`

**Interfaces:**
- Consumes: Task 1 query helpers, `compareDevices`, `getAllDevices`, and comparison model types.
- Produces: `/compare` public route and accessible comparison presentation.

- [ ] **Step 1: Add smoke assertions before page implementation**

```js
const initialCompare = await html("/compare");
assert.match(initialCompare, /Choose two objects/);
assert.equal(comparisonRequests.length, 0);
const comparison = await html("/compare?devices=nokia-3310,nokia-3210");
assert.ok(comparison.indexOf("Nokia 3310") < comparison.indexOf("Nokia 3210"));
assert.match(comparison, />No</);
assert.match(comparison, />0 /);
assert.match(comparison, />Unknown</);
```

- [ ] **Step 2: Build and run smoke to verify RED**

Run: `cmd /c npm run build` then `cmd /c npm run test:smoke` in `apps/web`  
Expected: smoke FAILS because `/compare` is missing.

- [ ] **Step 3: Implement selector, metadata, error states, and table**

`ComparisonSelector` uses two labelled selects, one checkbox, and `router.push` with `toComparisonQuery`. `ComparisonTable` uses `<table>`, row/device `<th>` cells, API ordering, typed formatting, and explicit difference/missing labels. The page calls neither adapter until `devices` is present.

- [ ] **Step 4: Add responsive table styles and rerun smoke**

Run: `cmd /c npm run test:smoke` in `apps/web`  
Expected: PASS for initial, valid, reversed, differences-only, invalid, and rate-limited comparison states.

- [ ] **Step 5: Commit the task**

```text
feat(web): render device comparisons
```

### Task 3: Discovery entry points and documentation

**Files:**
- Modify: `apps/web/src/components/site-header.tsx`
- Modify: `apps/web/src/components/site-footer.tsx`
- Modify: `apps/web/src/app/devices/[slug]/page.tsx`
- Modify: `apps/web/src/app/globals.css`
- Modify: `docs/FRONTEND_SPEC.md`
- Modify: `apps/web/README.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: `/compare` route from Task 2.
- Produces: discoverable navigation and an updated frontend delivery record.

- [ ] **Step 1: Extend smoke assertions for entry points**

```js
assert.match(await html("/devices/nokia-3310"), /href="\/compare\?devices=nokia-3310/);
assert.match(await html("/"), /href="\/compare"/);
```

- [ ] **Step 2: Run smoke and verify RED for missing links**

Run: `cmd /c npm run test:smoke` in `apps/web`  
Expected: FAIL on the missing compare links.

- [ ] **Step 3: Add navigation links and update docs**

Document Frontend Phase 6 as implemented from Backend Phase 7. Keep museum/collections deferred and describe URL/query/error semantics.

- [ ] **Step 4: Run comparison verification**

Run: `cmd /c npm run lint`, `cmd /c npm run typecheck`, `cmd /c npm test`, `cmd /c npm run build`, `cmd /c npm run test:smoke` in `apps/web`  
Expected: all commands exit 0.

- [ ] **Step 5: Commit the task**

```text
docs(web): record comparison phase
```

