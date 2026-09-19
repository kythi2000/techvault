import { Buffer } from "node:buffer";

export const ADMIN_SESSION_TTL_SECONDS = 8 * 60 * 60;

const VERSION = 1;
const IV_BYTES = 12;
const TAG_BYTES = 16;
const encoder = new TextEncoder();
const decoder = new TextDecoder();

export interface AdminSessionPayload {
  apiKey: string;
  expiresAt: number;
}

export class AdminSessionConfigurationError extends Error {
  constructor() {
    super("The admin session secret is not configured correctly.");
    this.name = "AdminSessionConfigurationError";
  }
}

function decodeSecret(secret: string): ArrayBuffer {
  if (!/^[A-Za-z0-9+/]+={0,2}$/.test(secret) || secret.length % 4 !== 0) {
    throw new AdminSessionConfigurationError();
  }
  const bytes = Buffer.from(secret, "base64");
  if (bytes.length !== 32 || bytes.toString("base64") !== secret) {
    throw new AdminSessionConfigurationError();
  }
  const keyBytes = new Uint8Array(bytes.length);
  keyBytes.set(bytes);
  return keyBytes.buffer;
}

async function importSecret(secret: string): Promise<CryptoKey> {
  return crypto.subtle.importKey("raw", decodeSecret(secret), "AES-GCM", false, ["encrypt", "decrypt"]);
}

export async function sealAdminSession(apiKey: string, secret: string, now = new Date()): Promise<string> {
  const key = await importSecret(secret);
  const iv = crypto.getRandomValues(new Uint8Array(IV_BYTES));
  const payload: AdminSessionPayload = {
    apiKey,
    expiresAt: now.getTime() + ADMIN_SESSION_TTL_SECONDS * 1000,
  };
  const encrypted = new Uint8Array(await crypto.subtle.encrypt(
    { name: "AES-GCM", iv, additionalData: new Uint8Array([VERSION]) },
    key,
    encoder.encode(JSON.stringify(payload)),
  ));
  const token = new Uint8Array(1 + iv.length + encrypted.length);
  token[0] = VERSION;
  token.set(iv, 1);
  token.set(encrypted, 1 + iv.length);
  return Buffer.from(token).toString("base64url");
}

export async function openAdminSession(token: string, secret: string, now = new Date()): Promise<AdminSessionPayload | null> {
  const key = await importSecret(secret);
  try {
    const bytes = new Uint8Array(Buffer.from(token, "base64url"));
    if (bytes.length < 1 + IV_BYTES + TAG_BYTES || bytes[0] !== VERSION) return null;
    const decrypted = await crypto.subtle.decrypt(
      { name: "AES-GCM", iv: bytes.slice(1, 1 + IV_BYTES), additionalData: new Uint8Array([VERSION]) },
      key,
      bytes.slice(1 + IV_BYTES),
    );
    const payload: unknown = JSON.parse(decoder.decode(decrypted));
    if (!payload || typeof payload !== "object") return null;
    const { apiKey, expiresAt } = payload as Partial<AdminSessionPayload>;
    if (typeof apiKey !== "string" || apiKey.length === 0 || typeof expiresAt !== "number" || !Number.isSafeInteger(expiresAt)) return null;
    if (expiresAt <= now.getTime()) return null;
    return { apiKey, expiresAt };
  } catch {
    return null;
  }
}
