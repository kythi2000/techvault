import { redirect } from "next/navigation";
import { AdminShell } from "@/components/admin/admin-shell";
import { AdminErrorMessage } from "@/components/admin/admin-error-message";
import { DeviceEditor } from "@/components/admin/device-editor";
import { adminGetAllComparisonGroups, adminGetAllReferences } from "@/lib/admin-api";
import type { AdminBrand, AdminCategory } from "@/lib/admin-contracts";
import { requireAdminSession } from "@/lib/admin-session";

export default async function NewAdminDevicePage({ searchParams }: PageProps<"/admin/devices/new">) {
  const session = await requireAdminSession();
  const notice = await searchParams;
  const retry = typeof notice.retry === "string" && /^\d{1,9}$/.test(notice.retry) ? notice.retry : undefined;
  const trace = typeof notice.trace === "string" && /^[A-Za-z0-9._:-]{1,128}$/.test(notice.trace) ? notice.trace : undefined;
  const [brands, categories, groups] = await Promise.all([
    adminGetAllReferences(session.apiKey, "brands"),
    adminGetAllReferences(session.apiKey, "categories"),
    adminGetAllComparisonGroups(session.apiKey),
  ]);
  const failed = [brands, categories, groups].find((result) => !result.ok);
  if (failed && !failed.ok && failed.status === 401) redirect("/admin/login?reauth=1");

  return (
    <AdminShell>
      <main className="admin-main">
        <header className="admin-record-header"><div><p className="admin-kicker">New catalog record</p><h1>Create a device draft</h1></div><p>Start with identity fields. Editorial content can remain incomplete until publication.</p></header>
        {notice.error === "RATE_LIMITED" && <AdminErrorMessage code="RATE_LIMITED" message="Too many editorial requests." retryAfterSeconds={retry ? Number(retry) : undefined} traceId={trace} />}
        {notice.error === "PAYLOAD_TOO_LARGE" && <AdminErrorMessage code="PAYLOAD_TOO_LARGE" message="The submitted content exceeds the configured request limit. Shorten it and try again." traceId={trace} />}
        {failed && !failed.ok ? (
          <AdminErrorMessage code={failed.error.code} message={failed.error.message} traceId={failed.error.traceId} retryAfterSeconds={failed.retryAfterSeconds} />
        ) : brands.ok && categories.ok && groups.ok ? (
          <DeviceEditor brands={brands.data as AdminBrand[]} categories={categories.data as AdminCategory[]} comparisonGroups={groups.data} />
        ) : null}
      </main>
    </AdminShell>
  );
}
