import assert from "node:assert/strict";
import { test } from "node:test";
import { discoveryPageHref, normalizeSearchQuery, normalizeTimelineQuery, toSearchQuery, toTimelineQuery } from "../src/lib/discovery-query.ts";
import { getTimeline, searchDevices } from "../src/lib/techvault-api.ts";
import { card, discoveryDevices, paged } from "./fixtures.mjs";

test("search query is encoded, paginated, and limited to the search contract", () => {
  const params = normalizeSearchQuery({ q: ["Nokia & 3310", "ignored"], page: "2", pageSize: "1", brand: "nokia", sort: "name-asc" });
  assert.deepEqual(params, { q: "Nokia & 3310", page: "2", pageSize: "1" });
  assert.equal(discoveryPageHref("/search", params, 3), "/search?q=Nokia+%26+3310&page=3&pageSize=1");
  assert.equal(toSearchQuery({ ...params, category: "phones" }), "?q=Nokia+%26+3310&page=2&pageSize=1");
  assert.deepEqual(normalizeSearchQuery({}), {});
  assert.equal(toSearchQuery(normalizeSearchQuery({ q: "" })), "?q=");
  assert.equal(toSearchQuery(normalizeSearchQuery({ q: "   ", page: "abc" })), "?q=+++&page=abc");
});

test("timeline pagination keeps all chronology filters and excludes browse-only keys", () => {
  const params = normalizeTimelineQuery({
    brand: ["nokia", "apple"], category: "feature-phones", type: "phones", year: "1999",
    fromYear: "1990", toYear: "2000", era: "1990s", page: "2", pageSize: "1",
    q: "nokia", sort: "release-desc", decade: "1990", ignored: "value",
  });
  assert.equal(discoveryPageHref("/timeline", params, 1), "/timeline?brand=nokia&category=feature-phones&type=phones&year=1999&fromYear=1990&toYear=2000&era=1990s&page=1&pageSize=1");
  assert.equal(toTimelineQuery({ era: "1990S", sort: "name-asc", decade: "1990" }), "?era=1990S");
  assert.equal(toTimelineQuery(normalizeTimelineQuery({ brand: "", era: "", page: "abc" })), "?page=abc");
});

test("discovery API adapters preserve server order, validate timeline years, and retain API failures", async (t) => {
  let response = () => Response.json(paged(discoveryDevices));
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url) => { calls.push(url); return response(); });
  const search = await searchDevices({ q: "Nokia & Apple", pageSize: "4", type: "phones" });
  assert.equal(search.ok, true);
  assert.deepEqual(search.data.data.map((device) => device.slug), discoveryDevices.map((device) => device.slug));
  assert.match(calls[0], /\/api\/v1\/search\?q=Nokia\+%26\+Apple&pageSize=4$/);
  response = () => Response.json(paged([card]));
  assert.equal((await getTimeline({ era: "2000s", decade: "2000" })).ok, true);
  assert.match(calls[1], /\/api\/v1\/timeline\?era=2000s$/);
  response = () => Response.json(paged([{ ...card, releaseYear: null }]));
  assert.equal((await searchDevices({ q: "Nokia" })).ok, true);
  assert.equal((await getTimeline()).status, 502);
  response = () => Response.json({ error: { code: "VALIDATION_ERROR", message: "Invalid era.", traceId: "discovery-trace" } }, { status: 400 });
  const invalid = await getTimeline({ era: "1990" });
  assert.equal(invalid.status, 400);
  assert.equal(invalid.error.traceId, "discovery-trace");
  response = () => { throw new Error("Private connection details"); };
  assert.equal((await searchDevices({ q: "Nokia" })).status, 503);
  assert.equal((await getTimeline()).status, 503);
});
