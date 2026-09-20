"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import {
  adminChangeDeviceStatus,
  adminRemoveSpecification,
  adminSaveDevice,
  adminSetSpecification,
  type AdminResult,
} from "@/lib/admin-api";
import { adminFailureState, type AdminActionState } from "@/lib/admin-action-state";
import type { SpecificationDataType } from "@/lib/admin-contracts";
import { AdminFormError, parseDeviceForm, parseSpecificationForm, retainDeviceFormValues } from "@/lib/admin-form-data";
import { clearAdminSession, readAdminSession } from "@/lib/admin-session";

async function apiKey(): Promise<string> {
  const session = await readAdminSession();
  if (!session) redirect("/admin/login");
  return session.apiKey;
}

async function failure(result: Extract<AdminResult<unknown>, { ok: false }>): Promise<AdminActionState> {
  if (result.status === 401) {
    await clearAdminSession();
    redirect("/admin/login");
  }
  return adminFailureState(result);
}

function formFailure(error: unknown, values?: Record<string, string>): NonNullable<AdminActionState> {
  return {
    ok: false,
    code: "VALIDATION_ERROR",
    message: error instanceof AdminFormError ? error.message : "The submitted form could not be read.",
    ...(values ? { values } : {}),
  };
}

function operationalErrorPath(id: string | null, result: Extract<AdminResult<unknown>, { ok: false }>): string | null {
  if (result.status !== 413 && result.status !== 429) return null;
  const query = new URLSearchParams({ error: result.status === 413 ? "PAYLOAD_TOO_LARGE" : "RATE_LIMITED" });
  if (result.retryAfterSeconds !== undefined) query.set("retry", String(result.retryAfterSeconds));
  if (/^[A-Za-z0-9._:-]{1,128}$/.test(result.error.traceId)) query.set("trace", result.error.traceId);
  return `/admin/devices/${id ?? "new"}?${query}`;
}

export async function saveDeviceAction(
  id: string | null,
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  const values = retainDeviceFormValues(formData);
  let input;
  try {
    input = parseDeviceForm(formData);
  } catch (error) {
    return formFailure(error, values);
  }
  const result = await adminSaveDevice(await apiKey(), input, id ?? undefined);
  if (!result.ok) {
    const errorPath = operationalErrorPath(id, result);
    if (errorPath) redirect(errorPath);
    const state = await failure(result);
    return state && !state.ok ? { ...state, values } : state;
  }
  revalidatePath("/admin/devices");
  redirect(`/admin/devices/${result.data.id}`);
}

export async function lifecycleAction(
  deviceId: string,
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  const intent = formData.get("intent");
  if (intent !== "publish" && intent !== "unpublish" && intent !== "archive") {
    return { ok: false, code: "VALIDATION_ERROR", message: "Unsupported device lifecycle action." };
  }
  if (intent === "archive" && formData.get("confirmArchive") !== "yes") {
    redirect(`/admin/devices/${deviceId}?error=CONFIRMATION_REQUIRED`);
  }
  const result = await adminChangeDeviceStatus(await apiKey(), deviceId, intent);
  if (!result.ok) return failure(result);
  revalidatePath("/admin/devices");
  revalidatePath(`/admin/devices/${deviceId}`);
  redirect(`/admin/devices/${deviceId}`);
}

export async function setSpecificationAction(
  deviceId: string,
  definitionId: string,
  dataType: SpecificationDataType,
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  formData.set("dataType", dataType);
  let input;
  try {
    input = parseSpecificationForm(formData);
  } catch (error) {
    return formFailure(error);
  }
  const result = await adminSetSpecification(await apiKey(), deviceId, definitionId, input);
  if (!result.ok) return failure(result);
  redirect(`/admin/devices/${deviceId}?saved=specification`);
}

export async function removeSpecificationAction(
  deviceId: string,
  definitionId: string,
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  if (formData.get("confirmRemove") !== "yes") {
    return { ok: false, code: "CONFIRMATION_REQUIRED", message: "Confirm removal to mark this value unknown." };
  }
  const result = await adminRemoveSpecification(await apiKey(), deviceId, definitionId);
  if (!result.ok) return failure(result);
  redirect(`/admin/devices/${deviceId}?saved=specification-removed`);
}
