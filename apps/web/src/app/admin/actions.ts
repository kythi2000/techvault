"use server";

import { redirect } from "next/navigation";
import { adminListDevices } from "@/lib/admin-api";
import { adminFailureState, type AdminActionState } from "@/lib/admin-action-state";
import { clearAdminSession, writeAdminSession } from "@/lib/admin-session";
import { AdminSessionConfigurationError } from "@/lib/admin-session-crypto";

export async function loginAction(
  _previousState: AdminActionState,
  formData: FormData,
): Promise<AdminActionState> {
  const value = formData.get("apiKey");
  if (typeof value !== "string" || value.trim() === "") {
    return { ok: false, code: "VALIDATION_ERROR", message: "Enter the editor API key." };
  }

  const apiKey = value.trim();
  const verified = await adminListDevices(apiKey, { page: "1", pageSize: "1" });
  if (!verified.ok) return adminFailureState(verified);

  try {
    await writeAdminSession(apiKey);
  } catch (error) {
    if (error instanceof AdminSessionConfigurationError) {
      return {
        ok: false,
        code: "SESSION_CONFIGURATION_ERROR",
        message: "The editorial session is not configured on this server.",
      };
    }
    throw error;
  }
  redirect("/admin");
}

export async function logoutAction(): Promise<never> {
  await clearAdminSession();
  redirect("/admin/login");
}
