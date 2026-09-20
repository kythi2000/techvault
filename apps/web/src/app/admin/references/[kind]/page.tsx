import { notFound, redirect } from "next/navigation";
import { AdminShell } from "@/components/admin/admin-shell";
import { AdminErrorMessage } from "@/components/admin/admin-error-message";
import { ReferenceManager } from "@/components/admin/reference-manager";
import { adminGetAllReferences, adminListReferences } from "@/lib/admin-api";
import type { AdminCategory, AdminSpecificationGroup } from "@/lib/admin-contracts";
import { adminReferenceConfigs, isAdminReferenceKind } from "@/lib/admin-reference-config";
import { requireAdminSession } from "@/lib/admin-session";

const errorCopy: Record<string, string> = {
  CONFIRMATION_REQUIRED: "Confirm physical deletion before removing this reference.",
  REFERENCE_CONFLICT: "This reference is still in use and cannot be deleted.",
  REFERENCE_DELETE_FAILED: "The reference could not be deleted. Review its dependencies and try again.",
  RATE_LIMITED: "Too many editorial requests.",
  PAYLOAD_TOO_LARGE: "The request exceeds the configured payload limit. Shorten it and try again.",
};

export default async function AdminReferencePage({ params, searchParams }: PageProps<"/admin/references/[kind]">) {
  const { kind: rawKind } = await params;
  if (!isAdminReferenceKind(rawKind)) notFound();
  const kind = rawKind;
  const session = await requireAdminSession();
  const query = await searchParams;
  const page = typeof query.page === "string" ? query.page : "1";
  const retry = typeof query.retry === "string" && /^\d{1,9}$/.test(query.retry) ? Number(query.retry) : undefined;
  const trace = typeof query.trace === "string" && /^[A-Za-z0-9._:-]{1,128}$/.test(query.trace) ? query.trace : undefined;
  const [items, categories, groups] = await Promise.all([
    adminListReferences(session.apiKey, kind, { page, pageSize: "24" }),
    adminGetAllReferences(session.apiKey, "categories"),
    adminGetAllReferences(session.apiKey, "specification-groups"),
  ]);
  const failed = [items, categories, groups].find((result) => !result.ok);
  if (failed && !failed.ok && failed.status === 401) redirect("/admin/login?reauth=1");
  const config = adminReferenceConfigs[kind];

  return (
    <AdminShell>
      <main className="admin-main">
        <header className="admin-record-header">
          <div><p className="admin-kicker">Reference data</p><h1>{config.plural}</h1></div>
          <p>{config.description} Immutable identifiers remain visible but locked after creation.</p>
        </header>
        {typeof query.saved === "string" && ["created", "updated", "deleted"].includes(query.saved) && <div className="admin-banner admin-action-success" role="status">Reference {query.saved}.</div>}
        {typeof query.error === "string" && errorCopy[query.error] && <AdminErrorMessage code={query.error} message={errorCopy[query.error]} retryAfterSeconds={retry} traceId={trace} />}
        {failed && !failed.ok ? (
          <AdminErrorMessage code={failed.error.code} message={failed.error.message} traceId={failed.error.traceId} retryAfterSeconds={failed.retryAfterSeconds} />
        ) : items.ok && categories.ok && groups.ok ? (
          <ReferenceManager kind={kind} result={items.data} categories={categories.data as AdminCategory[]} groups={groups.data as AdminSpecificationGroup[]} />
        ) : null}
      </main>
    </AdminShell>
  );
}
