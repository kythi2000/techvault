import assert from "node:assert/strict";
import { test } from "node:test";
import {
  adminGetDevice,
  adminListDevices,
  adminListReferences,
} from "../src/lib/admin-api.ts";
import {
  parseDeviceForm,
  parseReferenceForm,
  parseSpecificationForm,
  retainDeviceFormValues,
  retainReferenceFormValues,
} from "../src/lib/admin-form-data.ts";
import { paged } from "./fixtures.mjs";

const ids = {
  device: "11111111-1111-4111-8111-111111111111",
  brand: "22222222-2222-4222-8222-222222222222",
  category: "33333333-3333-4333-8333-333333333333",
  comparison: "44444444-4444-4444-8444-444444444444",
  definition: "55555555-5555-4555-8555-555555555555",
};

const deviceInput = {
  name: "Editorial device",
  slug: "editorial-device",
  brandId: ids.brand,
  categoryId: ids.category,
  comparisonGroupId: ids.comparison,
  shortDescription: "Summary",
  description: "Overview",
  history: "History",
  seoTitle: "Editorial device | TechVault",
  seoDescription: "Editorial description",
  modelNumber: "MODEL-0",
  aliases: ["First name", "Second name"],
  releaseYear: 2000,
  releaseDate: "2000-09-01",
  discontinuedDate: null,
  heightMm: 0.125,
  widthMm: null,
  depthMm: null,
  weightGrams: 1.5,
};

const detail = {
  id: ids.device,
  status: "draft",
  content: deviceInput,
  createdAt: "2026-09-19T00:00:00+00:00",
  updatedAt: "2026-09-19T00:00:00+00:00",
  publishedAt: null,
  specifications: [{
    definitionId: ids.definition,
    key: "has_feature",
    dataType: "boolean",
    unit: null,
    valueText: null,
    valueNumber: null,
    valueBoolean: false,
    valueDate: null,
  }],
};

function form(values) {
  const result = new FormData();
  for (const [key, value] of Object.entries(values)) result.set(key, String(value));
  return result;
}

test("typed specification form preserves false and zero", () => {
  assert.deepEqual(
    parseSpecificationForm(form({ dataType: "boolean", valueBoolean: "false" })),
    { valueBoolean: false },
  );
  assert.deepEqual(
    parseSpecificationForm(form({ dataType: "number", valueNumber: "0" })),
    { valueNumber: 0 },
  );
  assert.throws(
    () => parseSpecificationForm(form({ dataType: "number", valueNumber: "not-a-number" })),
    /valid number/i,
  );
});

test("device form creates a full replacement contract and keeps unknown values null", () => {
  const submitted = {
    name: " Editorial device ", slug: "editorial-device", brandId: ids.brand, categoryId: ids.category,
    comparisonGroupId: "", shortDescription: "Summary", description: "Overview", history: "History",
    seoTitle: "Title", seoDescription: "SEO", modelNumber: "", aliases: " First name\n\nSecond name ",
    releaseYear: "2000", releaseDate: "", discontinuedDate: "", heightMm: "0.125", widthMm: "",
    depthMm: "", weightGrams: "1.5",
  };
  const parsed = parseDeviceForm(form(submitted));

  assert.deepEqual(parsed, {
    name: "Editorial device", slug: "editorial-device", brandId: ids.brand, categoryId: ids.category,
    comparisonGroupId: null, shortDescription: "Summary", description: "Overview", history: "History",
    seoTitle: "Title", seoDescription: "SEO", modelNumber: null, aliases: ["First name", "Second name"],
    releaseYear: 2000, releaseDate: null, discontinuedDate: null, heightMm: 0.125, widthMm: null,
    depthMm: null, weightGrams: 1.5,
  });
  assert.throws(() => parseDeviceForm(form({ ...submitted, heightMm: "0" })), /greater than zero/i);
});

test("reference parser returns only fields allowed for its closed resource kind", () => {
  assert.deepEqual(
    parseReferenceForm("specification-definitions", form({
      name: "Weight", key: "weight", groupId: ids.definition, dataType: "number",
      displayOrder: "20", unit: "g", isComparable: "true", injectedPath: "/devices",
    })),
    { name: "Weight", key: "weight", groupId: ids.definition, dataType: "number", displayOrder: 20, unit: "g", isComparable: true },
  );
  assert.throws(() => parseReferenceForm("../../devices", form({})), /Unsupported admin resource/);
});

test("failed editor submissions retain only allowlisted form values", () => {
  const device = form({ name: " Untrimmed editor value ", slug: "draft", apiKey: "must-not-survive" });
  assert.deepEqual(retainDeviceFormValues(device), {
    name: " Untrimmed editor value ", slug: "draft", brandId: "", categoryId: "", comparisonGroupId: "",
    modelNumber: "", shortDescription: "", description: "", history: "", seoTitle: "", seoDescription: "",
    aliases: "", releaseYear: "", releaseDate: "", discontinuedDate: "", heightMm: "", widthMm: "",
    depthMm: "", weightGrams: "",
  });

  const reference = form({ name: "Comparable field", key: "field", isComparable: "true", injectedPath: "/devices" });
  assert.deepEqual(retainReferenceFormValues("specification-definitions", reference), {
    name: "Comparable field", key: "field", groupId: "", dataType: "", displayOrder: "", unit: "", isComparable: "true",
  });
});

test("admin adapter sends bearer server-side and returns retry metadata without leaking it", async (t) => {
  let authorization = null;
  t.mock.method(globalThis, "fetch", async (_url, init) => {
    authorization = new Headers(init.headers).get("authorization");
    return Response.json(
      { error: { code: "RATE_LIMITED", message: "Too many requests; retry later.", traceId: "admin-rate" } },
      { status: 429, headers: { "Retry-After": "60", "X-Trace-Id": "admin-rate" } },
    );
  });

  const result = await adminListDevices("secret-value", { page: "1", status: "draft", ignored: "value" });
  assert.equal(authorization, "Bearer secret-value");
  assert.equal(result.status, 429);
  assert.equal(result.retryAfterSeconds, 60);
  assert.doesNotMatch(JSON.stringify(result), /secret-value/);
});

test("admin adapter rejects a normalized but invalid Retry-After date", async (t) => {
  t.mock.method(globalThis, "fetch", async () => Response.json(
    { error: { code: "RATE_LIMITED", message: "Too many requests; retry later.", traceId: "admin-rate" } },
    { status: 429, headers: { "Retry-After": "Sun, 31 Feb 2026 12:00:00 GMT" } },
  ));

  const result = await adminListDevices("secret-value", { page: "1" });
  assert.equal(result.status, 429);
  assert.equal(result.retryAfterSeconds, undefined);
});

test("admin adapter validates complete device detail responses", async (t) => {
  let response = () => Response.json({ data: detail });
  t.mock.method(globalThis, "fetch", async () => response());

  const valid = await adminGetDevice("secret", ids.device);
  assert.equal(valid.ok, true);
  assert.equal(valid.data.specifications[0].valueBoolean, false);
  assert.equal(valid.data.content.weightGrams, 1.5);

  response = () => Response.json({ data: { ...detail, specifications: [{ ...detail.specifications[0], valueBoolean: null }] } });
  const malformed = await adminGetDevice("secret", ids.device);
  assert.equal(malformed.ok, false);
  assert.equal(malformed.status, 502);
});

test("admin reference adapter rejects unknown resources before fetch", async (t) => {
  let calls = 0;
  t.mock.method(globalThis, "fetch", async () => { calls += 1; return Response.json(paged([])); });

  const result = await adminListReferences("secret", "../../devices", {});
  assert.equal(result.ok, false);
  assert.equal(result.status, 400);
  assert.equal(calls, 0);
});
