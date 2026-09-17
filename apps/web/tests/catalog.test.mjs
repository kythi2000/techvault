import assert from "node:assert/strict";
import { test } from "node:test";
import { deviceDetailSchema, specificationSchema } from "../src/lib/contracts.ts";
import { formatDate, formatSpecification } from "../src/lib/format.ts";
import { normalizeSearchParams, pageHref, toQuery } from "../src/lib/browse-query.ts";
import { browseDevices, getCategories } from "../src/lib/techvault-api.ts";
import { card, category, detail, paged, specification } from "./fixtures.mjs";

test("typed specifications preserve false, zero, and decimal precision", () => {
  const [fraction, boolean, zero] = deviceDetailSchema.parse(detail).specificationGroups[0].specifications;
  assert.equal(formatSpecification(fraction), "0.125 mm");
  assert.equal(formatSpecification(boolean), "No");
  assert.equal(formatSpecification(zero), "0 mm");
  assert.equal(formatDate(null), "Unknown");
  assert.equal(formatDate("2000-09-01"), "Sep 1, 2000");
});

test("contract rejects mismatched values, multiple values, missing values, and impossible dates", () => {
  for (const invalid of [
    { ...specification, dataType: "boolean" },
    { ...specification, valueBoolean: false },
    { ...specification, valueNumber: null },
    { ...specification, dataType: "date", valueNumber: null, valueDate: "2000-02-30" },
  ]) assert.equal(specificationSchema.safeParse(invalid).success, false);
  assert.equal(deviceDetailSchema.safeParse({ ...detail, slug: "../admin" }).success, false);
});

test("URL state preserves all supported filters and page size while dropping unknown keys", () => {
  const params = normalizeSearchParams({
    brand: ["nokia", "ignored"], category: "feature-phones", type: "phones",
    year: "2000", fromYear: "1990", toYear: "2009", decade: "2000",
    sort: "name-desc", page: "2", pageSize: "1", q: "not-supported", extra: "ignore",
  });
  const url = new URL(pageHref("/devices", params, 3), "http://localhost");
  assert.equal(url.searchParams.get("page"), "3");
  assert.equal(url.searchParams.get("pageSize"), "1");
  assert.equal(url.searchParams.get("type"), "phones");
  assert.equal(url.searchParams.get("fromYear"), "1990");
  assert.equal(url.searchParams.get("q"), null);
  assert.equal(toQuery(normalizeSearchParams({ brand: "", page: "abc" })), "?page=abc");
});

test("API client validates responses, retains trace IDs, and handles outages", async (t) => {
  let response = Response.json(paged([card]), { headers: { "X-Trace-Id": "test-trace" } });
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url, options) => {
    calls.push({ url, options });
    if (response instanceof Error) throw response;
    return response;
  });
  const success = await browseDevices("/phones", { brand: "nokia", pageSize: "1" });
  assert.equal(success.ok, true);
  assert.equal(success.traceId, "test-trace");
  assert.match(calls[0].url, /\/phones\?brand=nokia&pageSize=1$/);
  assert.equal(calls[0].options.cache, "no-store");
  assert.ok(calls[0].options.signal instanceof AbortSignal);

  response = Response.json({ unexpected: true }, { headers: { "X-Trace-Id": "bad-body" } });
  const malformed = await browseDevices("/devices");
  assert.equal(malformed.status, 502);
  assert.equal(malformed.error.traceId, "bad-body");

  response = Response.json({ error: { code: "VALIDATION_ERROR", message: "Invalid page.", traceId: "validation" } }, { status: 400 });
  const invalid = await browseDevices("/devices");
  assert.equal(invalid.status, 400);
  assert.equal(invalid.error.code, "VALIDATION_ERROR");

  response = new Error("Internal network details must not leak");
  const outage = await browseDevices("/devices");
  assert.equal(outage.status, 503);
  assert.doesNotMatch(outage.error.message, /Internal/);
});

test("taxonomy loads references beyond page one and rejects partial results on failure", async (t) => {
  let failSecond = false;
  t.mock.method(globalThis, "fetch", async (url) => {
    const page = Number(new URL(url).searchParams.get("page"));
    if (page === 2 && failSecond) return new Response("unavailable", { status: 503 });
    const row = page === 1 ? category : { ...category, id: category.parentCategoryId, name: "Phones", slug: "phones", parentCategoryId: null, parentSlug: null };
    return Response.json(paged([row], page, 100, 101));
  });
  const result = await getCategories();
  assert.equal(result.ok, true);
  assert.deepEqual(result.data.map((row) => row.slug), ["feature-phones", "phones"]);
  failSecond = true;
  assert.equal((await getCategories()).ok, false);
});
