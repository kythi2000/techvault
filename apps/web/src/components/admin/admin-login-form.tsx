"use client";

import { useActionState } from "react";
import { loginAction } from "@/app/admin/actions";
import { AdminActionMessage } from "./admin-action-message";

export function AdminLoginForm() {
  const [state, formAction, pending] = useActionState(loginAction, null, "/admin/login");

  return (
    <form action={formAction} className="admin-login-form">
      <label htmlFor="admin-api-key">Editor API key</label>
      <input
        id="admin-api-key"
        name="apiKey"
        type="password"
        autoComplete="current-password"
        required
        minLength={32}
        maxLength={256}
        spellCheck={false}
      />
      <AdminActionMessage state={state} />
      <button className="admin-button admin-button-primary" type="submit" disabled={pending}>
        {pending ? "Verifying…" : "Enter workspace"}
      </button>
    </form>
  );
}
