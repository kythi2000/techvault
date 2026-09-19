import "server-only";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import {
  ADMIN_SESSION_TTL_SECONDS,
  AdminSessionConfigurationError,
  openAdminSession,
  sealAdminSession,
  type AdminSessionPayload,
} from "./admin-session-crypto.ts";

const COOKIE_NAME = "techvault_admin_session";

function sessionSecret(): string {
  const secret = process.env.TECHVAULT_ADMIN_SESSION_SECRET;
  if (!secret) throw new AdminSessionConfigurationError();
  return secret;
}

export async function readAdminSession(): Promise<AdminSessionPayload | null> {
  const token = (await cookies()).get(COOKIE_NAME)?.value;
  if (!token) return null;
  try {
    return await openAdminSession(token, sessionSecret());
  } catch (error) {
    if (error instanceof AdminSessionConfigurationError) return null;
    throw error;
  }
}

export async function writeAdminSession(apiKey: string): Promise<void> {
  const token = await sealAdminSession(apiKey, sessionSecret());
  (await cookies()).set(COOKIE_NAME, token, {
    httpOnly: true,
    sameSite: "strict",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: ADMIN_SESSION_TTL_SECONDS,
    priority: "high",
  });
}

export async function clearAdminSession(): Promise<void> {
  (await cookies()).delete(COOKIE_NAME);
}

export async function requireAdminSession(): Promise<AdminSessionPayload> {
  const session = await readAdminSession();
  if (!session) redirect("/admin/login");
  return session;
}
