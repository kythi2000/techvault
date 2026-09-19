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
const adminSessionSecret = Buffer.alloc(32, 9).toString("base64");
const comparisonGroup = { id: "10101010-1010-4010-8010-101010101010", key: "phone", name: "Phones" };
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
const api = http.createServer((request, response) => {
  const url = new URL(request.url, "http://localhost");
  response.setHeader("Content-Type", "application/json");
  response.setHeader("X-Trace-Id", "smoke-trace");
  const error = (status, code) => {
    response.statusCode = status;
    response.end(JSON.stringify({ error: { code, message: "Synthetic API error.", traceId: "smoke-trace" } }));
  };
  if (!available) return error(500, "UNEXPECTED_ERROR");
  if (url.pathname.startsWith("/api/v1/admin/")) {
    if (request.headers.authorization !== `Bearer ${adminKey}`) return error(401, "UNAUTHORIZED");
    if (url.pathname === "/api/v1/admin/devices" && request.method === "GET") {
      return response.end(JSON.stringify(paged([], Number(url.searchParams.get("page") ?? 1), Number(url.searchParams.get("pageSize") ?? 24), 0)));
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

function actionFields(page) {
  const form = page.match(/<form\b[^>]*>[\s\S]*?<\/form>/)?.[0];
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

async function submit(path, fields, cookie) {
  const pageResponse = await fetch(`${base}${path}`, {
    headers: cookie ? { Cookie: cookie } : {},
    signal: AbortSignal.timeout(15000),
  });
  assert.equal(pageResponse.status, 200, path);
  const page = await pageResponse.text();
  const body = new FormData();
  for (const [name, value] of Object.entries({ ...actionFields(page), ...fields })) body.set(name, value);
  return fetch(`${base}${path}`, {
    method: "POST",
    headers: {
      ...(cookie ? { Cookie: cookie } : {}),
    },
    body,
    redirect: "manual",
    signal: AbortSignal.timeout(15000),
  });
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
  const dashboardResponse = await fetch(`${base}/admin`, { headers: { Cookie: adminCookie }, redirect: "manual" });
  assert.equal(dashboardResponse.status, 200);
  const dashboard = await dashboardResponse.text();
  assert.match(dashboard, /Editorial dashboard/);
  assert.match(dashboard, /Devices/);
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
  console.log("PASS: SSR, search relevance, timeline chronology/filters, mixed catalog, URL pagination, metadata, typed specs, HTTP 404 and failure states.");
} finally {
  if (web.exitCode === null) {
    const stopped = once(web, "exit");
    web.kill();
    await stopped;
  }
  api.closeAllConnections();
  await new Promise((resolve) => api.close(resolve));
}
