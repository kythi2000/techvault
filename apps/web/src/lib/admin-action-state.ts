import type { AdminResult } from "./admin-api.ts";

export type AdminActionState = {
  ok: true;
  message: string;
} | {
  ok: false;
  code: string;
  message: string;
  traceId?: string;
  retryAfterSeconds?: number;
} | null;

export function adminFailureState(result: Extract<AdminResult<unknown>, { ok: false }>): NonNullable<AdminActionState> {
  return {
    ok: false,
    code: result.error.code,
    message: result.error.message,
    ...(result.error.traceId ? { traceId: result.error.traceId } : {}),
    ...(result.retryAfterSeconds === undefined ? {} : { retryAfterSeconds: result.retryAfterSeconds }),
  };
}
