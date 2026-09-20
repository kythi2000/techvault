import type { AdminActionState } from "@/lib/admin-action-state";

export function AdminActionMessage({ state }: { state: AdminActionState }) {
  if (!state) return null;
  if (state.ok) {
    return <div className="admin-action-message admin-action-success" role="status" aria-live="polite">{state.message}</div>;
  }
  return (
    <div className="admin-action-message" role="alert" aria-live="polite">
      <strong>{state.code}</strong>
      <span>{state.message}</span>
      {state.retryAfterSeconds !== undefined && <span>Try again in {state.retryAfterSeconds} seconds.</span>}
      {state.traceId && <small>Trace: {state.traceId}</small>}
    </div>
  );
}
