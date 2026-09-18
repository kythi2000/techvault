import { RetryButton } from "@/components/retry-button";
import type { ApiError } from "@/lib/contracts";

export function DiscoveryError({ error, status, formId }: { error: ApiError; status: number; formId: string }) {
  return (
    <div className="error-state" role="alert">
      <span>{error.code}</span>
      <h2>{status === 400 ? "Check your search or filters." : "These records are temporarily unavailable."}</h2>
      <p>{error.message}</p>
      <p className="trace">Reference: {error.traceId}</p>
      {status === 400 ? <a href={`#${formId}`} className="text-link">Edit your request</a> : <RetryButton />}
    </div>
  );
}
