// HTTP/SSR checks, not browser tests. Run after npm run build.
import assert from "node:assert/strict";
import http from "node:http";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { appleBrand, brand, card, category, computerCategory, detail, discoveryDevices, paged } from "./fixtures.mjs";

let available = true;
let referencesAvailable = true;
const discoveryRequests = [];
const comparisonRequests = [];
const adminKey = "smoke-admin-key-01234567890123456789";
let acceptedAdminKey = adminKey;
let rateLimitAdminReads = false;
let rateLimitReferenceDeletes = false;
const adminRequests = [];
const adminSessionSecret = Buffer.alloc(32, 9).toString("base64");
const comparisonGroup = { id: "10101010-1010-4010-8010-101010101010", key: "phone", name: "Phones" };
const adminNow = "2026-09-19T00:00:00+00:00";
const adminGroups = [{ id: "55555555-5555-4555-8555-555555555555", name: "Core specifications", key: "core", displayOrder: 10 }];
const adminDefinitions = [
  { id: "77777777-7777-4777-8777-777777777777", name: "Feature enabled", key: "feature_enabled", groupId: adminGroups[0].id, dataType: "boolean", displayOrder: 10, unit: null, isComparable: true },
  { id: "88888888-8888-4888-8888-888888888888", name: "Test zero", key: "test_zero", groupId: adminGroups[0].id, dataType: "number", displayOrder: 20, unit: "mm", isComparable: true },
];
const adminBrands = [{ id: brand.id, name: brand.name, slug: brand.slug, description: brand.description }];
const adminCategories = [
  { id: "33333333-3333-4333-8333-333333333333", name: "Phones", slug: "phones", description: "Phone root", displayOrder: 0, parentCategoryId: null },
  { id: category.id, name: category.name, slug: category.slug, description: category.description, displayOrder: category.displayOrder, parentCategoryId: category.parentCategoryId },
];
const adminComparisonGroups = [comparisonGroup];
const adminDevices = [{
  id: card.id,
  status: "draft",
  content: {
    name: card.name, slug: card.slug, brandId: brand.id, categoryId: category.id, comparisonGroupId: comparisonGroup.id,
    shortDescription: card.shortDescription, description: detail.description, history: detail.history,
    seoTitle: detail.seoTitle, seoDescription: detail.seoDescription, modelNumber: null, aliases: [],
    releaseYear: 2000, releaseDate: null, discontinuedDate: null, heightMm: null, widthMm: null, depthMm: null, weightGrams: 133,
  },
  createdAt: adminNow,
  updatedAt: adminNow,
  publishedAt: null,
  specifications: [],
}];
const comparisonRows = [{
  id: "20202020-2020-4020-8020-202020202020",
  key: "display",
  name: "Display",
  displayOrder: 10,
  specifications: [
    {
      id: "30303030-3030-4030-8030-303030303030", key: "backlight", name: "Backlight",
      dataType: "boolean", unit: null, displayOrder: 10, isDifferent: true,
      values: [
        { isMissing: false, valueText: null, valueNumber: null, valueBoolean: false, valueDate: null },
        { isMissing: false, valueText: null, valueNumber: null, valueBoolean: true, valueDate: null },
      ],
    },
    {
      id: "40404040-4040-4040-8040-404040404040", key: "colors", name: "Colors",
      dataType: "number", unit: "colors", displayOrder: 20, isDifferent: true,
      values: [
        { isMissing: false, valueText: null, valueNumber: 0, valueBoolean: null, valueDate: null },
        { isMissing: true, valueText: null, valueNumber: null, valueBoolean: null, valueDate: null },
      ],
    },
    {
      id: "50505050-5050-4050-8050-505050505050", key: "network", name: "Network",
      dataType: "text", unit: null, displayOrder: 30, isDifferent: false,
      values: [
        { isMissing: false, valueText: "GSM", valueNumber: null, valueBoolean: null, valueDate: null },
        { isMissing: false, valueText: "GSM", valueNumber: null, valueBoolean: null, valueDate: null },
      ],
    },
  ],
}];
const api = http.createServer(async (request, response) => {
  const url = new URL(request.url, "http://localhost");
  response.setHeader("Content-Type", "application/json");
  response.setHeader("X-Trace-Id", "smoke-trace");
  const error = (status, code) => {
    response.statusCode = status;
    response.end(JSON.stringify({ error: { code, message: "Synthetic API error.", traceId: "smoke-trace" } }));
  };
  const readJson = async () => {
    const chunks = [];
    for await (const chunk of request) chunks.push(chunk);
    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  };
  const state = (device) => ({ id: device.id, slug: device.content.slug, status: device.status, updatedAt: device.updatedAt, publishedAt: device.publishedAt });
  const summary = (device) => ({
    id: device.id, name: device.content.name, slug: device.content.slug, status: device.status,
    brandId: device.content.brandId, categoryId: device.content.categoryId,
    comparisonGroupId: device.content.comparisonGroupId, updatedAt: device.updatedAt,
  });
  if (!available) return error(500, "UNEXPECTED_ERROR");
  if (url.pathname.startsWith("/api/v1/admin/")) {
    adminRequests.push(`${request.method} ${url.pathname}${url.search}`);
    if (request.headers.authorization !== `Bearer ${acceptedAdminKey}`) return error(401, "UNAUTHORIZED");
    if (url.pathname === "/api/v1/admin/devices" && request.method === "GET") {
      if (rateLimitAdminReads) {
        response.setHeader("Retry-After", "60");
        return error(429, "RATE_LIMITED");
      }
      const page = Number(url.searchParams.get("page") ?? 1);
      const pageSize = Number(url.searchParams.get("pageSize") ?? 24);
      const status = url.searchParams.get("status");
      const devices = status ? adminDevices.filter((device) => device.status === status) : adminDevices;
      return response.end(JSON.stringify(paged(devices.map(summary).slice((page - 1) * pageSize, page * pageSize), page, pageSize, devices.length)));
    }
    if (url.pathname === "/api/v1/admin/devices" && request.method === "POST") {
      const content = await readJson();
      if (content.name === "Rate Limited Device") {
        response.setHeader("Retry-After", "60");
        return error(429, "RATE_LIMITED");
      }
      if (content.name === "Oversized Device") return error(413, "PAYLOAD_TOO_LARGE");
      if (content.name === "Conflict Device") return error(409, "CATALOG_CONFLICT");
      const device = { id: "90909090-9090-4090-8090-909090909090", status: "draft", content, createdAt: adminNow, updatedAt: adminNow, publishedAt: null, specifications: [] };
      adminDevices.push(device);
      response.statusCode = 201;
      return response.end(JSON.stringify({ data: state(device) }));
    }
    const deviceMatch = url.pathname.match(/^\/api\/v1\/admin\/devices\/([0-9a-f-]+)$/);
    if (deviceMatch) {
      const device = adminDevices.find((item) => item.id === deviceMatch[1]);
      if (!device) return error(404, "ADMIN_RESOURCE_NOT_FOUND");
      if (request.method === "GET") return response.end(JSON.stringify({ data: device }));
      if (request.method === "PUT") {
        if (device.status === "archived") return error(409, "CATALOG_CONFLICT");
        device.content = await readJson();
        return response.end(JSON.stringify({ data: state(device) }));
      }
    }
    const lifecycleMatch = url.pathname.match(/^\/api\/v1\/admin\/devices\/([0-9a-f-]+)\/(publish|unpublish|archive)$/);
    if (lifecycleMatch && request.method === "POST") {
      const device = adminDevices.find((item) => item.id === lifecycleMatch[1]);
      if (!device) return error(404, "ADMIN_RESOURCE_NOT_FOUND");
      const action = lifecycleMatch[2];
      if (device.status === "archived" && action !== "archive") return error(409, "CATALOG_CONFLICT");
      device.status = action === "publish" ? "published" : action === "unpublish" ? "draft" : "archived";
      device.publishedAt = device.status === "published" ? adminNow : null;
      return response.end(JSON.stringify({ data: state(device) }));
    }
    const specificationMatch = url.pathname.match(/^\/api\/v1\/admin\/devices\/([0-9a-f-]+)\/specifications\/([0-9a-f-]+)$/);
    if (specificationMatch) {
      const device = adminDevices.find((item) => item.id === specificationMatch[1]);
      const definition = adminDefinitions.find((item) => item.id === specificationMatch[2]);
      if (!device || !definition) return error(404, "ADMIN_RESOURCE_NOT_FOUND");
      if (device.status === "archived") return error(409, "CATALOG_CONFLICT");
      if (request.method === "PUT") {
        const value = await readJson();
        device.specifications = device.specifications.filter((item) => item.definitionId !== definition.id);
        device.specifications.push({
          definitionId: definition.id, key: definition.key, dataType: definition.dataType, unit: definition.unit,
          valueText: null, valueNumber: null, valueBoolean: null, valueDate: null, ...value,
        });
      } else if (request.method === "DELETE") {
        device.specifications = device.specifications.filter((item) => item.definitionId !== definition.id);
      }
      return response.end(JSON.stringify({ data: state(device) }));
    }
    const adminLists = {
      "/api/v1/admin/brands": adminBrands,
      "/api/v1/admin/categories": adminCategories,
      "/api/v1/admin/specification-groups": adminGroups,
      "/api/v1/admin/specification-definitions": adminDefinitions,
      "/api/v1/admin/comparison-groups": adminComparisonGroups,
    };
    if (request.method === "GET" && adminLists[url.pathname]) {
      const values = adminLists[url.pathname];
      const page = Number(url.searchParams.get("page") ?? 1);
      const pageSize = Number(url.searchParams.get("pageSize") ?? 24);
      return response.end(JSON.stringify(paged(values.slice((page - 1) * pageSize, page * pageSize), page, pageSize, values.length)));
    }
    if (request.method === "POST" && adminLists[url.pathname]) {
      const values = adminLists[url.pathname];
      const input = await readJson();
      const item = { id: "12121212-1212-4212-8212-121212121212", ...input };
      values.push(item);
      response.statusCode = 201;
      return response.end(JSON.stringify({ data: item }));
    }
    const referenceMatch = url.pathname.match(/^\/api\/v1\/admin\/(brands|categories|specification-groups|specification-definitions)\/([0-9a-f-]+)$/);
    if (referenceMatch) {
      const values = adminLists[`/api/v1/admin/${referenceMatch[1]}`];
      const index = values.findIndex((item) => item.id === referenceMatch[2]);
      if (index < 0) return error(404, "ADMIN_RESOURCE_NOT_FOUND");
      if (request.method === "GET") return response.end(JSON.stringify({ data: values[index] }));
      if (request.method === "PUT") {
        const input = await readJson();
        const immutable = referenceMatch[1] === "brands" || referenceMatch[1] === "categories" ? "slug" : "key";
        if (input[immutable] !== values[index][immutable]) return error(400, "IMMUTABLE_FIELD");
        values[index] = { id: values[index].id, ...input };
        return response.end(JSON.stringify({ data: values[index] }));
      }
      if (request.method === "DELETE") {
        if (rateLimitReferenceDeletes) {
          response.setHeader("Retry-After", "60");
          return error(429, "RATE_LIMITED");
        }
        if (referenceMatch[1] === "brands" && referenceMatch[2] === brand.id) return error(409, "REFERENCE_CONFLICT");
        const [deleted] = values.splice(index, 1);
        return response.end(JSON.stringify({ data: { id: deleted.id } }));
      }
    }
    return error(404, "ADMIN_RESOURCE_NOT_FOUND");
  }
  if (url.pathname === "/api/v1/compare") {
    comparisonRequests.push(url);
    if ([...url.searchParams.keys()].some((key) => !["devices", "differencesOnly"].includes(key))) return error(400, "VALIDATION_ERROR");
    const raw = url.searchParams.get("devices") ?? "";
    const slugs = raw.split(",");
    if (slugs.length !== 2 || slugs[0] === slugs[1] || slugs.some((slug) => !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug))) return error(400, "VALIDATION_ERROR");
    if (slugs.includes("rate-limited")) {
      response.setHeader("Retry-After", "60");
      return error(429, "RATE_LIMITED");
    }
    const devices = slugs.map((slug) => discoveryDevices.find((device) => device.slug === slug));
    if (devices.some((device) => !device)) return error(404, "DEVICE_NOT_FOUND");
    if (devices[0].category.parentSlug !== devices[1].category.parentSlug) return error(400, "INCOMPATIBLE_DEVICES");
    const differencesOnly = url.searchParams.get("differencesOnly") === "true";
    const groups = differencesOnly && devices[0].category.parentSlug === "computers"
      ? []
      : differencesOnly
        ? comparisonRows.map((group) => ({ ...group, specifications: group.specifications.filter((item) => item.isDifferent) }))
      : comparisonRows;
    return response.end(JSON.stringify({ data: { comparisonGroup, devices, differencesOnly, specificationGroups: groups } }));
  }
  if (url.pathname === "/api/v1/devices/nokia-3310") return response.end(JSON.stringify({ data: detail }));
  if (url.pathname.startsWith("/api/v1/devices/")) return error(404, "DEVICE_NOT_FOUND");
  if (url.pathname === "/api/v1/brands") return referencesAvailable ? response.end(JSON.stringify(paged([brand, appleBrand], 1, 100))) : error(503, "REFERENCES_UNAVAILABLE");
  if (url.pathname === "/api/v1/brands/nokia") return response.end(JSON.stringify({ data: brand }));
  if (url.pathname.startsWith("/api/v1/brands/")) return error(404, "BRAND_NOT_FOUND");
  if (url.pathname === "/api/v1/categories") return referencesAvailable ? response.end(JSON.stringify(paged([category, computerCategory], 1, 100))) : error(503, "REFERENCES_UNAVAILABLE");
  if (["/api/v1/search", "/api/v1/timeline"].includes(url.pathname)) {
    discoveryRequests.push(url);
    const isSearch = url.pathname.endsWith("/search");
    const allowed = isSearch ? ["q", "page", "pageSize"] : ["brand", "category", "type", "year", "fromYear", "toYear", "era", "page", "pageSize"];
    if ([...url.searchParams.keys()].some((key) => !allowed.includes(key))) return error(400, "UNSUPPORTED_PARAMETER");
    const page = Number(url.searchParams.get("page") ?? 1);
    const pageSize = Number(url.searchParams.get("pageSize") ?? 24);
    if (!Number.isInteger(page) || page < 1 || page > 10000 || !Number.isInteger(pageSize) || pageSize < 1 || pageSize > 100) return error(400, "VALIDATION_ERROR");
    let cards = discoveryDevices;
    if (isSearch) {
      const q = url.searchParams.get("q") ?? "";
      if (q.length > 200 || !/[\p{L}\p{Nd}]/u.test(q)) return error(400, "VALIDATION_ERROR");
      const words = q.toLowerCase().trim().split(/\s+/);
      cards = cards.filter((device) => words.every((word) => `${device.name} ${device.brand.name}`.toLowerCase().includes(word)));
    } else {
      const value = (key) => url.searchParams.get(key);
      const era = value("era");
      if (era !== null && (!/^\d{3}0s$/.test(era) || Number(era.slice(0, 4)) < 10)) return error(400, "VALIDATION_ERROR");
      if (value("type") !== null && !["phones", "computers"].includes(value("type"))) return error(400, "VALIDATION_ERROR");
      for (const key of ["year", "fromYear", "toYear"]) {
        if (value(key) !== null && (!Number.isInteger(Number(value(key))) || Number(value(key)) < 1 || Number(value(key)) > 9999)) return error(400, "VALIDATION_ERROR");
      }
      if (value("fromYear") && value("toYear") && Number(value("fromYear")) > Number(value("toYear"))) return error(400, "VALIDATION_ERROR");
      cards = cards.filter((device) => device.releaseYear !== null
        && (!value("brand") || device.brand.slug === value("brand"))
        && (!value("category") || [device.category.slug, device.category.parentSlug].includes(value("category")))
        && (!value("type") || device.category.parentSlug === value("type"))
        && (!value("year") || device.releaseYear === Number(value("year")))
        && (!value("fromYear") || device.releaseYear >= Number(value("fromYear")))
        && (!value("toYear") || device.releaseYear <= Number(value("toYear")))
        && (!era || Math.floor(device.releaseYear / 10) * 10 === Number(era.slice(0, 4))))
        .sort((a, b) => a.releaseYear - b.releaseYear || (a.releaseDate ?? "9999-12-31").localeCompare(b.releaseDate ?? "9999-12-31") || a.slug.localeCompare(b.slug));
    }
    return response.end(JSON.stringify(paged(cards.slice((page - 1) * pageSize, page * pageSize), page, pageSize, cards.length)));
  }
  if (url.searchParams.get("page") === "abc") return error(400, "VALIDATION_ERROR");
  const page = Number(url.searchParams.get("page") ?? 1);
  const pageSize = Number(url.searchParams.get("pageSize") ?? 12);
  let cards = url.pathname === "/api/v1/devices" && pageSize === 100
    ? discoveryDevices
    : [card, { ...card, id: "99999999-9999-4999-8999-999999999999", name: "Synthetic second object", slug: "test-second-object" }];
  if (url.pathname === "/api/v1/computers") cards = discoveryDevices.filter((device) => device.brand.slug === "apple");
  if (url.searchParams.get("brand") === "unknown-brand") cards = [];
  response.end(JSON.stringify(paged(cards.slice((page - 1) * pageSize, page * pageSize), page, pageSize, cards.length)));
});
api.listen(0, "127.0.0.1");
await once(api, "listening");
const apiPort = api.address().port;

const portProbe = http.createServer();
portProbe.listen(0, "127.0.0.1");
await once(portProbe, "listening");
const webPort = portProbe.address().port;
await new Promise((resolve) => portProbe.close(resolve));

const web = spawn(process.execPath, ["node_modules/next/dist/bin/next", "start", "--hostname", "127.0.0.1", "--port", String(webPort)], {
  cwd: new URL("..", import.meta.url),
  env: {
    ...process.env,
    TECHVAULT_API_URL: `http://127.0.0.1:${apiPort}`,
    NEXT_PUBLIC_SITE_URL: "http://localhost:3000",
    TECHVAULT_ADMIN_SESSION_SECRET: adminSessionSecret,
  },
  windowsHide: true,
  stdio: ["ignore", "pipe", "pipe"],
});
let logs = "";
web.stdout.on("data", (chunk) => { logs += chunk; });
web.stderr.on("data", (chunk) => { logs += chunk; });
const base = `http://127.0.0.1:${webPort}`;

async function html(path, status = 200) {
  const response = await fetch(`${base}${path}`, { signal: AbortSignal.timeout(15000) });
  assert.equal(response.status, status, path);
  return (await response.text()).replace(/<script\b[^>]*>[\s\S]*?<\/script>/g, "");
}

function actionFields(page, marker) {
  const forms = [...page.matchAll(/<form\b[^>]*>[\s\S]*?<\/form>/g)].map((match) => match[0]);
  const form = marker ? forms.find((candidate) => candidate.includes(marker)) : forms[0];
  assert.ok(form, "Server Action form must be rendered");
  const fields = {};
  for (const match of form.matchAll(/<input type="hidden" name="([^"]+)"(?: value="([^"]*)")?\/>/g)) {
    fields[match[1]] = (match[2] ?? "")
      .replaceAll("&quot;", '"')
      .replaceAll("&amp;", "&")
      .replaceAll("&lt;", "<")
      .replaceAll("&gt;", ">");
  }
  assert.ok(Object.keys(fields).some((name) => name.startsWith("$ACTION_")), "Server Action fields must be rendered");
  return fields;
}

async function submit(path, fields, cookie, marker) {
  const pageResponse = await fetch(`${base}${path}`, {
    headers: cookie ? { Cookie: cookie } : {},
    signal: AbortSignal.timeout(15000),
  });
  assert.equal(pageResponse.status, 200, path);
  const page = await pageResponse.text();
  const body = new FormData();
  for (const [name, value] of Object.entries({ ...actionFields(page, marker), ...fields })) body.set(name, value);
  try {
    return await fetch(`${base}${path}`, {
      method: "POST",
      headers: {
        Origin: base,
        ...(cookie ? { Cookie: cookie } : {}),
      },
      body,
      redirect: "manual",
      signal: AbortSignal.timeout(15000),
    });
  } catch (error) {
    throw new Error(`Admin form request timed out at ${path}. Recent API requests: ${adminRequests.slice(-12).join(" | ")}. Server logs: ${logs.slice(-1000)}`, { cause: error });
  }
}

try {
  const deadline = Date.now() + 20000;
  while (!logs.includes("Ready")) {
    if (web.exitCode !== null || Date.now() > deadline) throw new Error(`Web server did not start. ${logs}`);
    await new Promise((resolve) => setTimeout(resolve, 100));
  }

  const home = await html("/");
  assert.match(home, /Technology has a/);
  assert.match(home, /Nokia 3310/);
  assert.match(home, /href="\/compare"/);
  assert.doesNotMatch(home, /Frontend Phase|local catalog API/);

  const filtered = await html("/devices?brand=nokia&type=phones&year=2000&fromYear=1990&toYear=2009&sort=name-asc&pageSize=1");
  assert.match(filtered, /Nokia 3310/);
  assert.match(filtered, /href="\/devices\?brand=nokia&amp;type=phones&amp;year=2000&amp;fromYear=1990&amp;toYear=2009&amp;sort=name-asc&amp;page=2&amp;pageSize=1"/);
  assert.match(filtered, /name="fromYear"[^>]*value="1990"/);
  assert.match(filtered, /name="pageSize"[^>]*value="1"/);
  assert.match(await html("/phones"), /Phone archive/);
  const computers = await html("/computers");
  assert.match(computers, /Macintosh 128K/);
  assert.match(computers, /iMac G3/);
  assert.match(await html("/computers?brand=unknown-brand"), /This cabinet is still empty/);
  assert.match(await html("/brands"), /The companies behind the objects/);
  const brandPage = await html("/brands/nokia");
  assert.match(brandPage, /Objects by [\s\S]*Nokia/);
  assert.match(brandPage, /<title>Nokia devices \| TechVault<\/title>/);
  assert.match(brandPage, /rel="canonical" href="http:\/\/localhost:3000\/brands\/nokia"/);
  assert.match(await html("/brands/nokia?page=abc"), /VALIDATION_ERROR/);
  assert.match(await html("/categories"), /device families used to organize objects/);
  const categoryPage = await html("/categories/feature-phones");
  assert.match(categoryPage, /Filed in this category/);
  assert.match(categoryPage, /<title>Feature Phones archive \| TechVault<\/title>/);
  assert.match(categoryPage, /rel="canonical" href="http:\/\/localhost:3000\/categories\/feature-phones"/);
  const unknownFilter = await html("/devices?brand=unknown-brand");
  assert.match(unknownFilter, /value="unknown-brand" selected=""/);
  assert.match(await html("/devices?page=4"), /You’re past the last page/);
  assert.match(await html("/devices?page=abc"), /VALIDATION_ERROR/);

  const device = await html("/devices/nokia-3310");
  assert.match(device, /Test history, rendered on the server/);
  assert.match(device, /href="\/compare\?devices=nokia-3310"/);
  assert.match(device, /href="\/brands\/nokia"/);
  assert.match(device, /href="\/categories\/feature-phones"/);
  assert.match(device, /<title>Nokia 3310 specifications and history \| TechVault<\/title>/);
  assert.match(device, /rel="canonical" href="http:\/\/localhost:3000\/devices\/nokia-3310"/);
  const specs = await html("/devices/nokia-3310/specs");
  assert.match(specs, /<dd>0.125 mm<\/dd>/);
  assert.match(specs, /<dd>No<\/dd>/);
  assert.match(specs, /<dd>0 mm<\/dd>/);
  const missing = await html("/devices/missing-device", 404);
  assert.match(missing, /name="robots" content="noindex/);
  await html("/devices/missing-device/specs", 404);
  await html("/brands/missing-brand", 404);
  await html("/categories/missing-category", 404);
  await html("/missing-route", 404);

  const initialSearch = await html("/search");
  assert.match(initialSearch, /What are you looking for/);
  assert.match(initialSearch, /name="robots" content="noindex, follow"/);
  assert.equal(discoveryRequests.length, 0, "Opening search without q must not call search API");
  const search = await html("/search?q=Nokia&pageSize=1&sort=name-asc&brand=apple");
  assert.match(search, /Nokia 3310/);
  assert.doesNotMatch(search, /Nokia 3210/);
  assert.match(search, /href="\/search\?q=Nokia&amp;page=2&amp;pageSize=1"/);
  assert.match(search, /value="Nokia"/);
  assert.match(search, /rel="canonical" href="http:\/\/localhost:3000\/search"/);
  assert.equal(discoveryRequests.at(-1).searchParams.has("brand"), false);
  const secondSearch = await html("/search?q=Nokia&page=2&pageSize=1");
  assert.match(secondSearch, /Nokia 3210/);
  assert.match(secondSearch, /href="\/search\?q=Nokia&amp;page=1&amp;pageSize=1"/);
  assert.match(await html("/search?q=no-such-object"), /No objects match those words/);
  assert.match(await html("/search?q=Nokia&page=99"), /You’re past the last page/);
  for (const path of ["/search?q=", "/search?q=%20%20", "/search?q=!!!", `/search?q=${"a".repeat(201)}`, "/search?q=Nokia&page=abc", "/search?q=Nokia&pageSize=101"]) {
    assert.match(await html(path), /VALIDATION_ERROR/, path);
  }

  const timeline = await html("/timeline");
  assert.match(timeline, /Devices in chronological order/);
  assert.match(timeline, /Exact release date unknown/);
  assert.match(timeline, /dateTime="1984-01-24"/i);
  const positions = ["Macintosh 128K", "iMac G3", "Nokia 3210", "Nokia 3310"].map((name) => timeline.indexOf(`aria-label="Explore ${name}"`));
  assert.ok(positions.every((position, i) => position >= 0 && (i === 0 || position > positions[i - 1])), "Timeline must preserve API chronology");
  assert.doesNotMatch(timeline, /dateTime="2000-01-01"/i);
  const eraTimeline = await html("/timeline?era=1990s&fromYear=1990&toYear=1999&pageSize=1&sort=name-asc&decade=2000");
  assert.match(eraTimeline, /iMac G3/);
  assert.doesNotMatch(eraTimeline, /Nokia 3310/);
  assert.match(eraTimeline, /href="\/timeline\?fromYear=1990&amp;toYear=1999&amp;era=1990s&amp;page=2&amp;pageSize=1"/);
  assert.match(eraTimeline, /name="era"[^>]*value="1990s"/);
  assert.match(eraTimeline, /name="robots" content="noindex, follow"/);
  assert.equal(discoveryRequests.at(-1).searchParams.has("decade"), false);
  assert.match(await html("/timeline?era=1990s&page=2&pageSize=1"), /Nokia 3210/);
  const appleTimeline = await html("/timeline?brand=apple&type=computers&category=computers");
  assert.match(appleTimeline, /Macintosh 128K/);
  assert.doesNotMatch(appleTimeline, /Nokia 3310/);
  assert.match(await html("/timeline?year=2000"), /Nokia 3310/);
  assert.match(await html("/timeline?brand=unknown-brand"), /No objects in this period/);
  assert.match(await html("/timeline?page=99"), /You’re past the last page/);
  for (const path of ["/timeline?era=1990", "/timeline?era=1990S", "/timeline?fromYear=2000&toYear=1980", "/timeline?page=0"]) {
    assert.match(await html(path), /VALIDATION_ERROR/, path);
  }
  referencesAvailable = false;
  const partialTimeline = await html("/timeline?brand=nokia&category=feature-phones");
  assert.match(partialTimeline, /Some filter choices could not be loaded/);
  assert.match(partialTimeline, /value="nokia" selected=""/);
  assert.match(partialTimeline, /Nokia 3310/);
  referencesAvailable = true;

  const initialCompare = await html("/compare");
  assert.match(initialCompare, /Choose two objects/);
  assert.match(initialCompare, /name="robots" content="index, follow"/);
  assert.equal(comparisonRequests.length, 0, "Opening compare without devices must not call comparison API");
  const comparisonRequestsBeforePartial = comparisonRequests.length;
  const partialCompare = await html("/compare?devices=nokia-3310");
  assert.match(partialCompare, /Choose two objects/);
  assert.equal(comparisonRequests.length, comparisonRequestsBeforePartial, "A valid one-device entry point must wait for the second selection");
  const comparison = await html("/compare?devices=nokia-3310,nokia-3210");
  assert.ok(comparison.indexOf('href="/devices/nokia-3310">Nokia 3310') < comparison.indexOf('href="/devices/nokia-3210">Nokia 3210'), "Comparison columns must follow the requested order");
  assert.match(comparison, />No</);
  assert.match(comparison, />0 colors</);
  assert.match(comparison, />Unknown</);
  assert.match(comparison, /name="robots" content="noindex, follow"/);
  const reversedComparison = await html("/compare?devices=nokia-3210,nokia-3310");
  assert.ok(reversedComparison.indexOf('href="/devices/nokia-3210">Nokia 3210') < reversedComparison.indexOf('href="/devices/nokia-3310">Nokia 3310'), "Reversed request must reverse columns");
  const differences = await html("/compare?devices=nokia-3310,nokia-3210&differencesOnly=true");
  assert.doesNotMatch(differences, />Network</);
  const emptyDifferences = await html("/compare?devices=macintosh-128k,imac-g3&differencesOnly=true");
  assert.match(emptyDifferences, /href="\/devices\/macintosh-128k">Macintosh 128K/);
  assert.match(emptyDifferences, /href="\/devices\/imac-g3">iMac G3/);
  assert.match(emptyDifferences, /No differing comparable values remain/);
  assert.match(await html("/compare?devices=nokia-3310,macintosh-128k"), /INCOMPATIBLE_DEVICES/);
  assert.match(await html("/compare?devices=nokia-3310,missing-device"), /DEVICE_NOT_FOUND/);
  assert.match(await html("/compare?devices=nokia-3310,nokia-3310"), /VALIDATION_ERROR/);
  const rateLimited = await html("/compare?devices=rate-limited,nokia-3310");
  assert.match(rateLimited, /RATE_LIMITED/);
  assert.match(rateLimited, /60[\s\S]*seconds/);

  const login = await html("/admin");
  assert.match(login, /Unlock the editorial workspace/);
  assert.match(login, /name="robots" content="noindex, nofollow"/);
  const rejectedLogin = await submit("/admin/login", { apiKey: "wrong-key" });
  assert.equal(rejectedLogin.status, 200);
  assert.match(await rejectedLogin.text(), /UNAUTHORIZED/);
  const acceptedLogin = await submit("/admin/login", { apiKey: adminKey });
  assert.equal(acceptedLogin.status, 303);
  assert.equal(acceptedLogin.headers.get("location"), "/admin");
  const adminCookie = acceptedLogin.headers.get("set-cookie");
  assert.match(adminCookie, /HttpOnly/);
  assert.match(adminCookie, /SameSite=Strict/i);
  assert.doesNotMatch((await acceptedLogin.text()) + logs, new RegExp(adminKey));
  acceptedAdminKey = "rotated-admin-key-012345678901234567";
  const rejectedSession = await fetch(`${base}/admin`, { headers: { Cookie: adminCookie }, redirect: "manual" });
  assert.equal(rejectedSession.status, 307);
  assert.equal(rejectedSession.headers.get("location"), "/admin/login?reauth=1");
  const reauthentication = await fetch(new URL(rejectedSession.headers.get("location"), base), { headers: { Cookie: adminCookie } });
  const reauthenticationHtml = await reauthentication.text();
  assert.equal(reauthentication.status, 200);
  assert.match(reauthenticationHtml, /SESSION_REJECTED/);
  assert.match(reauthenticationHtml, /Unlock the editorial workspace/);
  acceptedAdminKey = adminKey;
  const dashboardResponse = await fetch(`${base}/admin`, { headers: { Cookie: adminCookie }, redirect: "manual" });
  assert.equal(dashboardResponse.status, 200);
  const dashboard = await dashboardResponse.text();
  assert.match(dashboard, /Editorial dashboard/);
  assert.match(dashboard, /Devices/);
  rateLimitAdminReads = true;
  const rateLimitedRead = await fetch(`${base}/admin`, { headers: { Cookie: adminCookie } });
  const rateLimitedReadHtml = await rateLimitedRead.text();
  assert.match(rateLimitedReadHtml, /RATE_LIMITED/);
  assert.match(rateLimitedReadHtml, /Try again[\s\S]*60[\s\S]*seconds/);
  assert.match(rateLimitedReadHtml, /Trace:[\s\S]*smoke-trace/);
  rateLimitAdminReads = false;

  const deviceFields = {
    name: "Editorial test device", slug: "editorial-test-device", brandId: brand.id, categoryId: category.id,
    comparisonGroupId: comparisonGroup.id, shortDescription: "A complete editorial summary.",
    description: "A complete editorial description.", history: "A complete editorial history.",
    seoTitle: "Editorial test device | TechVault", seoDescription: "Editorial SEO description.",
    modelNumber: "MODEL-0", aliases: "First alias\nSecond alias", releaseYear: "2001", releaseDate: "",
    discontinuedDate: "", heightMm: "0.125", widthMm: "", depthMm: "", weightGrams: "1.5",
  };
  const deviceList = await fetch(`${base}/admin/devices`, { headers: { Cookie: adminCookie } });
  assert.equal(deviceList.status, 200);
  assert.match(await deviceList.text(), /Nokia 3310/);
  const createdDevice = await submit("/admin/devices/new", deviceFields, adminCookie, 'value="device-editor"');
  assert.equal(createdDevice.status, 303);
  assert.equal(createdDevice.headers.get("location"), "/admin/devices/90909090-9090-4090-8090-909090909090");
  assert.equal(adminDevices.at(-1).content.heightMm, 0.125);
  const editDevice = await fetch(`${base}/admin/devices/90909090-9090-4090-8090-909090909090`, { headers: { Cookie: adminCookie } });
  const editDeviceHtml = await editDevice.text();
  assert.match(editDeviceHtml, /Editorial test device/);
  assert.match(editDeviceHtml, /Draft/);
  const updatedDevice = await submit(
    "/admin/devices/90909090-9090-4090-8090-909090909090",
    { ...deviceFields, name: "Editorial device revised" }, adminCookie, 'value="device-editor"',
  );
  assert.equal(updatedDevice.status, 303);
  assert.equal(adminDevices.at(-1).content.name, "Editorial device revised");
  const falseSpecification = await submit(
    "/admin/devices/90909090-9090-4090-8090-909090909090",
    { dataType: "boolean", valueBoolean: "false" },
    adminCookie,
    `value="${adminDefinitions[0].id}"`,
  );
  assert.equal(falseSpecification.status, 303);
  assert.equal(adminDevices.at(-1).specifications[0].valueBoolean, false);
  const zeroSpecification = await submit(
    "/admin/devices/90909090-9090-4090-8090-909090909090",
    { dataType: "number", valueNumber: "0" },
    adminCookie,
    `value="${adminDefinitions[1].id}"`,
  );
  assert.equal(zeroSpecification.status, 303);
  assert.equal(adminDevices.at(-1).specifications[1].valueNumber, 0);
  const publishedDevice = await submit(
    "/admin/devices/90909090-9090-4090-8090-909090909090",
    { intent: "publish" }, adminCookie, 'value="lifecycle"',
  );
  assert.equal(publishedDevice.status, 303);
  assert.equal(adminDevices.at(-1).status, "published");
  const refusedArchive = await submit(
    "/admin/devices/90909090-9090-4090-8090-909090909090",
    { intent: "archive" }, adminCookie, 'value="lifecycle"',
  );
  assert.equal(refusedArchive.status, 303);
  const refusedArchivePage = await fetch(new URL(refusedArchive.headers.get("location"), base), { headers: { Cookie: adminCookie } });
  assert.match(await refusedArchivePage.text(), /CONFIRMATION_REQUIRED/);
  const archivedDevice = await submit(
    "/admin/devices/90909090-9090-4090-8090-909090909090",
    { intent: "archive", confirmArchive: "yes" }, adminCookie, 'value="lifecycle"',
  );
  assert.equal(archivedDevice.status, 303);
  assert.equal(adminDevices.at(-1).status, "archived");
  const archivedPage = await fetch(`${base}/admin/devices/90909090-9090-4090-8090-909090909090`, { headers: { Cookie: adminCookie } });
  const archivedHtml = await archivedPage.text();
  assert.match(archivedHtml, /Archived/);
  assert.doesNotMatch(archivedHtml, />Save device</);
  assert.doesNotMatch(archivedHtml, /value="lifecycle"/);
  assert.doesNotMatch(archivedHtml, />Save value</);
  assert.doesNotMatch(archivedHtml, />Remove value</);
  const archivedList = await fetch(`${base}/admin/devices?status=archived`, { headers: { Cookie: adminCookie } });
  assert.match(await archivedList.text(), /Editorial device revised/);

  const brandsPage = await fetch(`${base}/admin/references/brands`, { headers: { Cookie: adminCookie } });
  assert.equal(brandsPage.status, 200);
  assert.match(await brandsPage.text(), /Nokia/);
  const createdBrand = await submit(
    "/admin/references/brands",
    { name: "Test maker", slug: "test-maker", description: "Created by the admin smoke workflow." },
    adminCookie,
    'value="reference-create"',
  );
  assert.equal(createdBrand.status, 303);
  assert.ok(adminBrands.some((item) => item.slug === "test-maker"));
  const updatedBrand = await submit(
    "/admin/references/brands",
    { name: "Test maker revised", slug: "test-maker", description: "Updated." },
    adminCookie,
    'value="reference-edit-12121212-1212-4212-8212-121212121212"',
  );
  assert.equal(updatedBrand.status, 303);
  assert.equal(adminBrands.find((item) => item.slug === "test-maker").name, "Test maker revised");
  const brandConflict = await submit(
    "/admin/references/brands",
    { confirmDelete: "yes" }, adminCookie, `value="reference-delete-${brand.id}"`,
  );
  assert.equal(brandConflict.status, 303);
  const conflictPage = await fetch(new URL(brandConflict.headers.get("location"), base), { headers: { Cookie: adminCookie } });
  assert.match(await conflictPage.text(), /REFERENCE_CONFLICT/);
  rateLimitReferenceDeletes = true;
  const rateLimitedDelete = await submit(
    "/admin/references/brands",
    { confirmDelete: "yes" }, adminCookie, 'value="reference-delete-12121212-1212-4212-8212-121212121212"',
  );
  assert.equal(rateLimitedDelete.status, 303);
  const rateLimitedDeletePage = await fetch(new URL(rateLimitedDelete.headers.get("location"), base), { headers: { Cookie: adminCookie } });
  const rateLimitedDeleteHtml = await rateLimitedDeletePage.text();
  assert.match(rateLimitedDeleteHtml, /RATE_LIMITED/);
  assert.match(rateLimitedDeleteHtml, /Try again[\s\S]*60[\s\S]*seconds/);
  assert.match(rateLimitedDeleteHtml, /smoke-trace/);
  rateLimitReferenceDeletes = false;
  const deletedBrand = await submit(
    "/admin/references/brands",
    { confirmDelete: "yes" }, adminCookie, 'value="reference-delete-12121212-1212-4212-8212-121212121212"',
  );
  assert.equal(deletedBrand.status, 303);
  assert.equal(adminBrands.some((item) => item.slug === "test-maker"), false);
  const categoriesManager = await fetch(`${base}/admin/references/categories`, { headers: { Cookie: adminCookie } });
  assert.match(await categoriesManager.text(), /Parent category/);
  const groupsManager = await fetch(`${base}/admin/references/specification-groups`, { headers: { Cookie: adminCookie } });
  assert.match(await groupsManager.text(), /Core specifications/);
  const definitionsManager = await fetch(`${base}/admin/references/specification-definitions`, { headers: { Cookie: adminCookie } });
  const definitionsHtml = await definitionsManager.text();
  assert.match(definitionsHtml, /Feature enabled/);
  assert.match(definitionsHtml, /Comparable/);
  const referenceCases = [
    {
      kind: "categories", list: adminCategories,
      create: { name: "Test category", slug: "test-category", description: "Created.", displayOrder: "20", parentCategoryId: "" },
      update: { name: "Test category revised", slug: "test-category", description: "Updated.", displayOrder: "21", parentCategoryId: "" },
    },
    {
      kind: "specification-groups", list: adminGroups,
      create: { name: "Test group", key: "test_group", displayOrder: "20" },
      update: { name: "Test group revised", key: "test_group", displayOrder: "21" },
    },
    {
      kind: "specification-definitions", list: adminDefinitions,
      create: { name: "Test definition", key: "test_definition", groupId: adminGroups[0].id, dataType: "text", displayOrder: "30", unit: "", isComparable: "true" },
      update: { name: "Test definition revised", key: "test_definition", groupId: adminGroups[0].id, dataType: "text", displayOrder: "31", unit: "", isComparable: "true" },
    },
  ];
  for (const referenceCase of referenceCases) {
    const referencePath = `/admin/references/${referenceCase.kind}`;
    const created = await submit(referencePath, referenceCase.create, adminCookie, 'value="reference-create"');
    assert.equal(created.status, 303);
    assert.ok(referenceCase.list.some((item) => item.id === "12121212-1212-4212-8212-121212121212"));
    const updated = await submit(referencePath, referenceCase.update, adminCookie, 'value="reference-edit-12121212-1212-4212-8212-121212121212"');
    assert.equal(updated.status, 303);
    assert.equal(referenceCase.list.find((item) => item.id === "12121212-1212-4212-8212-121212121212").name, referenceCase.update.name);
    const deleted = await submit(referencePath, { confirmDelete: "yes" }, adminCookie, 'value="reference-delete-12121212-1212-4212-8212-121212121212"');
    assert.equal(deleted.status, 303);
    assert.equal(referenceCase.list.some((item) => item.id === "12121212-1212-4212-8212-121212121212"), false);
  }
  const unknownManager = await fetch(`${base}/admin/references/not-a-resource`, { headers: { Cookie: adminCookie } });
  assert.equal(unknownManager.status, 404);
  const rateLimitedAdmin = await submit(
    "/admin/devices/new", { ...deviceFields, name: "Rate Limited Device", slug: "rate-limited-device" }, adminCookie, 'value="device-editor"',
  );
  assert.equal(rateLimitedAdmin.status, 303);
  const rateLimitedAdminPage = await fetch(new URL(rateLimitedAdmin.headers.get("location"), base), { headers: { Cookie: adminCookie } });
  const rateLimitedAdminHtml = await rateLimitedAdminPage.text();
  assert.match(rateLimitedAdminHtml, /RATE_LIMITED/);
  assert.match(rateLimitedAdminHtml, /Try again[\s\S]*60[\s\S]*seconds/);
  const oversizedAdmin = await submit(
    "/admin/devices/new", { ...deviceFields, name: "Oversized Device", slug: "oversized-device" }, adminCookie, 'value="device-editor"',
  );
  assert.equal(oversizedAdmin.status, 303);
  const oversizedAdminPage = await fetch(new URL(oversizedAdmin.headers.get("location"), base), { headers: { Cookie: adminCookie } });
  assert.match(await oversizedAdminPage.text(), /PAYLOAD_TOO_LARGE/);
  const securedResponse = await fetch(`${base}/admin`, { headers: { Cookie: adminCookie } });
  assert.equal(securedResponse.headers.get("x-content-type-options"), "nosniff");
  assert.equal(securedResponse.headers.get("x-frame-options"), "DENY");
  assert.equal(securedResponse.headers.get("referrer-policy"), "strict-origin-when-cross-origin");

  const logout = await submit("/admin", {}, adminCookie);
  assert.equal(logout.status, 303);
  assert.equal(logout.headers.get("location"), "/admin/login");
  assert.match(logout.headers.get("set-cookie"), /(?:Max-Age=0|Expires=Thu, 01 Jan 1970)/);

  available = false;
  assert.match(await html("/devices"), /smoke-trace/);
  assert.match(await html("/devices/nokia-3310"), /This exhibit is temporarily unavailable/);
  assert.match(await html("/brands/nokia"), /This maker record is temporarily unavailable/);
  assert.match(await html("/search?q=Nokia"), /These records are temporarily unavailable/);
  assert.match(await html("/timeline"), /These records are temporarily unavailable/);
  assert.match(await html("/"), /The collection cannot be loaded right now/);
  assert.doesNotMatch(logs, /TypeError|ReferenceError|SyntaxError/);
  assert.doesNotMatch(logs, new RegExp(adminKey));
  console.log("PASS: public SSR/discovery/comparison plus protected admin session, device lifecycle/specifications, reference CRUD, operational errors, and safe headers.");
} finally {
  if (web.exitCode === null) {
    const stopped = once(web, "exit");
    web.kill();
    await stopped;
  }
  api.closeAllConnections();
  await new Promise((resolve) => api.close(resolve));
}
