import type { AdminActionState } from "@/lib/admin-action-state";

export function AdminActionMessage({ state }: { state: AdminActionState }) {
  if (!state) return null;
  return (
    <div className="admin-action-message" role="alert" aria-live="polite">
      <strong>{state.code}</strong>
      <span>{state.message}</span>
      {state.retryAfterSeconds !== undefined && <span>Try again in {state.retryAfterSeconds} seconds.</span>}
      {state.traceId && <small>Trace: {state.traceId}</small>}
    </div>
  );
}
