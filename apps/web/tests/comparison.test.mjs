import assert from "node:assert/strict";
import { test } from "node:test";
import {
  normalizeComparisonQuery,
  toComparisonQuery,
} from "../src/lib/comparison-query.ts";
import { compareDevices, getAllDevices } from "../src/lib/techvault-api.ts";
import { discoveryDevices, paged } from "./fixtures.mjs";

const comparison = {
  comparisonGroup: {
    id: "10101010-1010-4010-8010-101010101010",
    key: "phone",
    name: "Phones",
  },
  devices: [discoveryDevices[0], discoveryDevices[1]],
  differencesOnly: false,
  specificationGroups: [{
    id: "20202020-2020-4020-8020-202020202020",
    key: "display",
    name: "Display",
    displayOrder: 10,
    specifications: [
      {
        id: "30303030-3030-4030-8030-303030303030",
        key: "backlight",
        name: "Backlight",
        dataType: "boolean",
        unit: null,
        displayOrder: 10,
        isDifferent: true,
        values: [
          { isMissing: false, valueText: null, valueNumber: null, valueBoolean: false, valueDate: null },
          { isMissing: false, valueText: null, valueNumber: null, valueBoolean: true, valueDate: null },
        ],
      },
      {
        id: "40404040-4040-4040-8040-404040404040",
        key: "colors",
        name: "Colors",
        dataType: "number",
        unit: null,
        displayOrder: 20,
        isDifferent: true,
        values: [
          { isMissing: false, valueText: null, valueNumber: 0, valueBoolean: null, valueDate: null },
          { isMissing: true, valueText: null, valueNumber: null, valueBoolean: null, valueDate: null },
        ],
      },
    ],
  }],
};

test("comparison keeps ordered slugs and only its supported query", () => {
  const params = normalizeComparisonQuery({
    devices: ["nokia-3310,nokia-3210", "ignored"],
    differencesOnly: "true",
    page: "2",
    left: "ignored",
  });

  assert.deepEqual(params, {
    devices: "nokia-3310,nokia-3210",
    differencesOnly: "true",
  });
  assert.equal(
    toComparisonQuery({ devices: "imac-g3,macintosh-128k", differencesOnly: "false" }),
    "?devices=imac-g3%2Cmacintosh-128k&differencesOnly=false",
  );
  assert.deepEqual(normalizeComparisonQuery({}), {});
});

test("comparison adapter preserves response order, false, zero, and missing values", async (t) => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url) => {
    calls.push(String(url));
    return Response.json({ data: comparison });
  });

  const result = await compareDevices({
    devices: "nokia-3310,nokia-3210",
    differencesOnly: "false",
    page: "2",
  });

  assert.equal(result.ok, true);
  assert.deepEqual(result.data.devices.map((device) => device.slug), ["nokia-3310", "nokia-3210"]);
  assert.equal(result.data.specificationGroups[0].specifications[0].values[0].valueBoolean, false);
  assert.equal(result.data.specificationGroups[0].specifications[1].values[0].valueNumber, 0);
  assert.equal(result.data.specificationGroups[0].specifications[1].values[1].isMissing, true);
  assert.match(calls[0], /\/api\/v1\/compare\?devices=nokia-3310%2Cnokia-3210&differencesOnly=false$/);
  assert.doesNotMatch(calls[0], /page=/);
});

test("comparison adapter rejects malformed value alignment", async (t) => {
  t.mock.method(globalThis, "fetch", async () => Response.json({
    data: {
      ...comparison,
      specificationGroups: [{
        ...comparison.specificationGroups[0],
        specifications: [{
          ...comparison.specificationGroups[0].specifications[0],
          values: [comparison.specificationGroups[0].specifications[0].values[0]],
        }],
      }],
    },
  }));

  const result = await compareDevices({ devices: "nokia-3310,nokia-3210" });
  assert.equal(result.ok, false);
  assert.equal(result.status, 502);
});

test("comparison adapter retains rate limit retry metadata", async (t) => {
  t.mock.method(globalThis, "fetch", async () => Response.json(
    { error: { code: "RATE_LIMITED", message: "Too many requests; retry later.", traceId: "rate-trace" } },
    { status: 429, headers: { "Retry-After": "60", "X-Trace-Id": "rate-trace" } },
  ));

  const result = await compareDevices({ devices: "nokia-3310,nokia-3210" });
  assert.equal(result.ok, false);
  assert.equal(result.status, 429);
  assert.equal(result.retryAfterSeconds, 60);
  assert.equal(result.error.traceId, "rate-trace");
});

test("public adapter drops unsafe Retry-After values", async (t) => {
  t.mock.method(globalThis, "fetch", async () => Response.json(
    { error: { code: "RATE_LIMITED", message: "Too many requests; retry later.", traceId: "rate-trace" } },
    { status: 429, headers: { "Retry-After": "999999999999999999999999", "X-Trace-Id": "rate-trace" } },
  ));

  const result = await compareDevices({ devices: "nokia-3310,nokia-3210" });
  assert.equal(result.ok, false);
  assert.equal(result.status, 429);
  assert.equal(result.retryAfterSeconds, undefined);
});

test("public adapter rejects a normalized but invalid Retry-After date", async (t) => {
  t.mock.method(globalThis, "fetch", async () => Response.json(
    { error: { code: "RATE_LIMITED", message: "Too many requests; retry later.", traceId: "rate-trace" } },
    { status: 429, headers: { "Retry-After": "Sun, 31 Feb 2026 12:00:00 GMT" } },
  ));

  const result = await compareDevices({ devices: "nokia-3310,nokia-3210" });
  assert.equal(result.status, 429);
  assert.equal(result.retryAfterSeconds, undefined);
});

test("all-device picker follows public pagination in API order", async (t) => {
  let call = 0;
  t.mock.method(globalThis, "fetch", async () => {
    call += 1;
    return Response.json(paged(
      call === 1 ? discoveryDevices.slice(0, 2) : discoveryDevices.slice(2),
      call,
      2,
      4,
    ));
  });

  const result = await getAllDevices(2);
  assert.equal(result.ok, true);
  assert.deepEqual(result.data.map((device) => device.slug), discoveryDevices.map((device) => device.slug));
  assert.equal(call, 2);
});
