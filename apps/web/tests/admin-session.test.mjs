import assert from "node:assert/strict";
import { test } from "node:test";
import {
  ADMIN_SESSION_TTL_SECONDS,
  AdminSessionConfigurationError,
  openAdminSession,
  sealAdminSession,
} from "../src/lib/admin-session-crypto.ts";

const issuedAt = new Date("2026-09-19T00:00:00.000Z");
const validSecret = Buffer.alloc(32, 7).toString("base64");

test("admin session round-trips without exposing its API key", async () => {
  const token = await sealAdminSession("editor-key", validSecret, issuedAt);
  const session = await openAdminSession(token, validSecret, new Date("2026-09-19T01:00:00.000Z"));

  assert.equal(session.apiKey, "editor-key");
  assert.equal(session.expiresAt, issuedAt.getTime() + ADMIN_SESSION_TTL_SECONDS * 1000);
  assert.doesNotMatch(token, /editor-key/);
});

test("admin session expires after eight hours and rejects tampering", async () => {
  const token = await sealAdminSession("editor-key", validSecret, issuedAt);

  assert.equal(await openAdminSession(token, validSecret, new Date("2026-09-19T08:00:00.000Z")), null);
  assert.equal(await openAdminSession(`${token.slice(0, -1)}x`, validSecret, new Date("2026-09-19T01:00:00.000Z")), null);
  assert.equal(await openAdminSession("not-a-session", validSecret, new Date("2026-09-19T01:00:00.000Z")), null);
});

test("admin session rejects a different encryption key", async () => {
  const token = await sealAdminSession("editor-key", validSecret, issuedAt);
  const otherSecret = Buffer.alloc(32, 8).toString("base64");

  assert.equal(await openAdminSession(token, otherSecret, new Date("2026-09-19T01:00:00.000Z")), null);
});

test("admin session configuration requires canonical base64 encoding of exactly 32 bytes", async () => {
  for (const secret of ["", "not-base64", Buffer.alloc(31).toString("base64"), `${validSecret}ignored`]) {
    await assert.rejects(
      sealAdminSession("editor-key", secret, issuedAt),
      (error) => error instanceof AdminSessionConfigurationError &&
        error.message === "The admin session secret is not configured correctly.",
    );
  }
});
