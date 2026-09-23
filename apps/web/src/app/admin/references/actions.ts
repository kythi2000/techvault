"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import {
  adminDeleteReference,
  adminSaveReference,
  type AdminReferenceKind,
  type AdminResult,
} from "@/lib/admin-api";
import { adminFailureState, type AdminActionState } from "@/lib/admin-action-state";
import { AdminFormError, parseReferenceForm, retainReferenceFormValues } from "@/lib/admin-form-data";
import { isAdminReferenceKind } from "@/lib/admin-reference-config";
import { clearAdminSession, readAdminSession } from "@/lib/admin-session";

async function apiKey(): Promise<string> {
  const session = await readAdminSession();
  if (!session) redirect("/admin/login");
  return session.apiKey;
}

async function failure(result: Extract<AdminResult<unknown>, { ok: false }>): Promise<AdminActionState> {
  if (result.status === 401) {
    await clearAdminSession();
    redirect("/admin/login?reauth=1");
  }
  return adminFailureState(result);
}

function route(kind: AdminReferenceKind, query: string) {
  return `/admin/references/${kind}?${query}`;
}

function operationalRoute(kind: AdminReferenceKind, result: Extract<AdminResult<unknown>, { ok: false }>): string | null {
  if (result.status !== 413 && result.status !== 429) return null;
  const query = new URLSearchParams({ error: result.status === 413 ? "PAYLOAD_TOO_LARGE" : "RATE_LIMITED" });
  if (result.retryAfterSeconds !== undefined) query.set("retry", String(result.retryAfterSeconds));
  if (/^[A-Za-z0-9._:-]{1,128}$/.test(result.error.traceId)) query.set("trace", result.error.traceId);
  return route(kind, query.toString());
}

export async function saveReferenceAction(
  kind: AdminReferenceKind,
  id: string | null,
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  if (!isAdminReferenceKind(kind)) {
    return { ok: false, code: "VALIDATION_ERROR", message: "Unsupported admin resource." };
  }
  const values = retainReferenceFormValues(kind, formData);
  let input;
  try {
    input = parseReferenceForm(kind, formData);
  } catch (error) {
    return {
      ok: false,
      code: "VALIDATION_ERROR",
      message: error instanceof AdminFormError ? error.message : "The submitted form could not be read.",
      values,
    };
  }
  const result = await adminSaveReference(await apiKey(), kind, input, id ?? undefined);
  if (!result.ok) {
    const errorRoute = operationalRoute(kind, result);
    if (errorRoute) redirect(errorRoute);
    const state = await failure(result);
    return state && !state.ok ? { ...state, values } : state;
  }
  revalidatePath(`/admin/references/${kind}`);
  redirect(route(kind, `saved=${id ? "updated" : "created"}`));
}

export async function deleteReferenceAction(
  kind: AdminReferenceKind,
  id: string,
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  if (!isAdminReferenceKind(kind)) {
    return { ok: false, code: "VALIDATION_ERROR", message: "Unsupported admin resource." };
  }
  if (formData.get("confirmDelete") !== "yes") {
    redirect(route(kind, "error=CONFIRMATION_REQUIRED"));
  }
  const result = await adminDeleteReference(await apiKey(), kind, id);
  if (!result.ok) {
    if (result.status === 401) return failure(result);
    const errorRoute = operationalRoute(kind, result);
    if (errorRoute) redirect(errorRoute);
    const safeCode = result.error.code === "REFERENCE_CONFLICT" ? "REFERENCE_CONFLICT" : "REFERENCE_DELETE_FAILED";
    redirect(route(kind, `error=${safeCode}`));
  }
  revalidatePath(`/admin/references/${kind}`);
  redirect(route(kind, "saved=deleted"));
}
