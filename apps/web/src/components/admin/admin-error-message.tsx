type Props = {
  code: string;
  message: string;
  traceId?: string | null;
  retryAfterSeconds?: number;
  variant?: "action" | "banner";
};

export function AdminErrorMessage({ code, message, traceId, retryAfterSeconds, variant = "banner" }: Props) {
  const safeTrace = traceId && /^[A-Za-z0-9._:-]{1,128}$/.test(traceId) ? traceId : undefined;
  const safeRetry = Number.isSafeInteger(retryAfterSeconds) && retryAfterSeconds !== undefined && retryAfterSeconds >= 0
    ? retryAfterSeconds
    : undefined;

  return (
    <div className={variant === "banner" ? "admin-banner admin-banner-error" : "admin-action-message"} role="alert" aria-live="polite">
      <strong>{code}</strong>
      <span>{message}</span>
      {safeRetry !== undefined && <span>Try again in {safeRetry} seconds.</span>}
      {safeTrace && <small>Trace: {safeTrace}</small>}
    </div>
  );
}
