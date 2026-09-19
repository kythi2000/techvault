import { RetryButton } from "@/components/retry-button";
import type { ApiError } from "@/lib/contracts";

const titles: Record<string, string> = {
  VALIDATION_ERROR: "Choose two distinct objects.",
  DEVICE_NOT_FOUND: "One object is no longer public.",
  COMPARISON_UNAVAILABLE: "These records are not ready to compare.",
  INCOMPATIBLE_DEVICES: "Choose objects from the same comparison group.",
  RATE_LIMITED: "The archive needs a short pause.",
};

export function ComparisonError({ error, retryAfterSeconds }: { error: ApiError; retryAfterSeconds?: number }) {
  return (
    <div className="error-state comparison-error" role="alert">
      <span>{error.code}</span>
      <h2>{titles[error.code] ?? "The comparison is temporarily unavailable."}</h2>
      <p>{error.message}</p>
      {retryAfterSeconds !== undefined && <p>Try again in {retryAfterSeconds} seconds.</p>}
      <p className="trace">Reference: {error.traceId}</p>
      <div className="comparison-error-actions">
        <a href="#comparison-selector" className="text-link">Edit your selection</a>
        {error.code === "RATE_LIMITED" || !titles[error.code] ? <RetryButton /> : null}
      </div>
    </div>
  );
}

