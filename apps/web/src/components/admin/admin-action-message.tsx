import type { AdminActionState } from "@/lib/admin-action-state";
import { AdminErrorMessage } from "./admin-error-message";

export function AdminActionMessage({ state }: { state: AdminActionState }) {
  if (!state) return null;
  if (state.ok) {
    return <div className="admin-action-message admin-action-success" role="status" aria-live="polite">{state.message}</div>;
  }
  return <AdminErrorMessage code={state.code} message={state.message} traceId={state.traceId} retryAfterSeconds={state.retryAfterSeconds} variant="action" />;
}
