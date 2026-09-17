// HTTP/SSR checks, not browser tests. Run after npm run build.
import assert from "node:assert/strict";
import http from "node:http";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { brand, card, category, detail, paged } from "./fixtures.mjs";

let available = true;
const api = http.createServer((request, response) => {
  const url = new URL(request.url, "http://localhost");
  response.setHeader("Content-Type", "application/json");
  response.setHeader("X-Trace-Id", "smoke-trace");
  const error = (status, code) => {
    response.statusCode = status;
    response.end(JSON.stringify({ error: { code, message: "Synthetic API error.", traceId: "smoke-trace" } }));
  };
  if (!available) return error(500, "UNEXPECTED_ERROR");
  if (url.pathname === "/api/v1/devices/nokia-3310") return response.end(JSON.stringify({ data: detail }));
  if (url.pathname.startsWith("/api/v1/devices/")) return error(404, "DEVICE_NOT_FOUND");
  if (url.pathname === "/api/v1/brands") return response.end(JSON.stringify(paged([brand], 1, 100)));
  if (url.pathname === "/api/v1/brands/nokia") return response.end(JSON.stringify({ data: brand }));
  if (url.pathname.startsWith("/api/v1/brands/")) return error(404, "BRAND_NOT_FOUND");
  if (url.pathname === "/api/v1/categories") return response.end(JSON.stringify(paged([category], 1, 100)));
  if (url.searchParams.get("page") === "abc") return error(400, "VALIDATION_ERROR");
  const page = Number(url.searchParams.get("page") ?? 1);
  const pageSize = Number(url.searchParams.get("pageSize") ?? 12);
  let cards = [card, { ...card, id: "99999999-9999-4999-8999-999999999999", name: "Synthetic second object", slug: "test-second-object" }];
  if (url.pathname === "/api/v1/computers" || url.searchParams.get("brand") === "unknown-brand") cards = [];
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
  env: { ...process.env, TECHVAULT_API_URL: `http://127.0.0.1:${apiPort}`, NEXT_PUBLIC_SITE_URL: "http://localhost:3000" },
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

try {
  const deadline = Date.now() + 20000;
  while (!logs.includes("Ready")) {
    if (web.exitCode !== null || Date.now() > deadline) throw new Error(`Web server did not start. ${logs}`);
    await new Promise((resolve) => setTimeout(resolve, 100));
  }

  const home = await html("/");
  assert.match(home, /Technology has a/);
  assert.match(home, /Nokia 3310/);
  assert.doesNotMatch(home, /Frontend Phase|local catalog API/);

  const filtered = await html("/devices?brand=nokia&type=phones&year=2000&fromYear=1990&toYear=2009&sort=name-asc&pageSize=1");
  assert.match(filtered, /Nokia 3310/);
  assert.match(filtered, /href="\/devices\?brand=nokia&amp;type=phones&amp;year=2000&amp;fromYear=1990&amp;toYear=2009&amp;sort=name-asc&amp;page=2&amp;pageSize=1"/);
  assert.match(filtered, /name="fromYear"[^>]*value="1990"/);
  assert.match(filtered, /name="pageSize"[^>]*value="1"/);
  assert.match(await html("/phones"), /Phone archive/);
  assert.match(await html("/computers"), /This cabinet is still empty/);
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

  available = false;
  assert.match(await html("/devices"), /smoke-trace/);
  assert.match(await html("/devices/nokia-3310"), /This exhibit is temporarily unavailable/);
  assert.match(await html("/brands/nokia"), /This maker record is temporarily unavailable/);
  assert.match(await html("/"), /The collection cannot be loaded right now/);
  assert.doesNotMatch(logs, /TypeError|ReferenceError|SyntaxError/);
  console.log("PASS: SSR, URL filters, taxonomy pages, pagination, metadata, typed specs, empty states, HTTP 404 and API failure states.");
} finally {
  if (web.exitCode === null) {
    const stopped = once(web, "exit");
    web.kill();
    await stopped;
  }
  api.closeAllConnections();
  await new Promise((resolve) => api.close(resolve));
}
