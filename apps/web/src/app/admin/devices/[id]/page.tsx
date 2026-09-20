import { notFound, redirect } from "next/navigation";
import { AdminShell } from "@/components/admin/admin-shell";
import { AdminErrorMessage } from "@/components/admin/admin-error-message";
import { DeviceEditor } from "@/components/admin/device-editor";
import { DeviceLifecycle } from "@/components/admin/device-lifecycle";
import { SpecificationEditor } from "@/components/admin/specification-editor";
import {
  adminGetAllComparisonGroups,
  adminGetAllReferences,
  adminGetDevice,
} from "@/lib/admin-api";
import type { AdminBrand, AdminCategory, AdminSpecificationDefinition } from "@/lib/admin-contracts";
import { requireAdminSession } from "@/lib/admin-session";

function statusLabel(status: string) {
  return status[0].toUpperCase() + status.slice(1);
}

export default async function EditAdminDevicePage({ params, searchParams }: PageProps<"/admin/devices/[id]">) {
  const session = await requireAdminSession();
  const { id } = await params;
  const notice = await searchParams;
  const retry = typeof notice.retry === "string" && /^\d{1,9}$/.test(notice.retry) ? notice.retry : undefined;
  const trace = typeof notice.trace === "string" && /^[A-Za-z0-9._:-]{1,128}$/.test(notice.trace) ? notice.trace : undefined;
  const [device, brands, categories, comparisonGroups, definitions] = await Promise.all([
    adminGetDevice(session.apiKey, id),
    adminGetAllReferences(session.apiKey, "brands"),
    adminGetAllReferences(session.apiKey, "categories"),
    adminGetAllComparisonGroups(session.apiKey),
    adminGetAllReferences(session.apiKey, "specification-definitions"),
  ]);
  if (!device.ok && device.status === 404) notFound();
  const failed = [device, brands, categories, comparisonGroups, definitions].find((result) => !result.ok);
  if (failed && !failed.ok && failed.status === 401) redirect("/admin/login?reauth=1");

  if (failed && !failed.ok) {
    return (
      <AdminShell><main className="admin-main"><AdminErrorMessage code={failed.error.code} message={failed.error.message} traceId={failed.error.traceId} retryAfterSeconds={failed.retryAfterSeconds} /></main></AdminShell>
    );
  }
  if (!device.ok || !brands.ok || !categories.ok || !comparisonGroups.ok || !definitions.ok) return null;

  return (
    <AdminShell>
      <main className="admin-main">
        <header className="admin-record-header">
          <div><p className="admin-kicker">Device record</p><h1>{device.data.content.name}</h1></div>
          <div className="admin-record-meta"><span className={`admin-status admin-status-${device.data.status}`}>{statusLabel(device.data.status)}</span><code>{device.data.id}</code></div>
        </header>
        {notice.error === "CONFIRMATION_REQUIRED" && <div className="admin-banner admin-banner-error" role="alert"><strong>CONFIRMATION_REQUIRED</strong><span>Confirm that this device should be archived.</span></div>}
        {notice.error === "RATE_LIMITED" && <AdminErrorMessage code="RATE_LIMITED" message="Too many editorial requests." retryAfterSeconds={retry ? Number(retry) : undefined} traceId={trace} />}
        {notice.error === "PAYLOAD_TOO_LARGE" && <AdminErrorMessage code="PAYLOAD_TOO_LARGE" message="The submitted content exceeds the configured request limit. Shorten it and try again." traceId={trace} />}
        {notice.saved === "specification" && <div className="admin-banner admin-action-success" role="status">Specification saved.</div>}
        {notice.saved === "specification-removed" && <div className="admin-banner admin-action-success" role="status">Specification removed.</div>}
        <section className="admin-panel" aria-labelledby="lifecycle-title">
          <div className="admin-panel-heading"><div><p className="admin-kicker">Workflow</p><h2 id="lifecycle-title">Lifecycle</h2></div><p>Publishing makes this record visible across the public archive. Archiving removes it and cannot be reversed here.</p></div>
          <DeviceLifecycle deviceId={device.data.id} status={device.data.status} />
        </section>
        <section className="admin-panel" aria-labelledby="content-title">
          <div className="admin-panel-heading"><div><p className="admin-kicker">Full replacement</p><h2 id="content-title">Content</h2></div><p>Slug changes do not redirect old public URLs. Review every field before saving.</p></div>
          <DeviceEditor device={device.data} brands={brands.data as AdminBrand[]} categories={categories.data as AdminCategory[]} comparisonGroups={comparisonGroups.data} />
        </section>
        <section className="admin-panel" aria-labelledby="specification-title">
          <div className="admin-panel-heading"><div><p className="admin-kicker">Typed values</p><h2 id="specification-title">Specifications</h2></div><p>Unknown values are absent. Numeric zero and boolean false are stored values.</p></div>
          <SpecificationEditor deviceId={device.data.id} definitions={definitions.data as AdminSpecificationDefinition[]} values={device.data.specifications} readOnly={device.data.status === "archived"} />
        </section>
      </main>
    </AdminShell>
  );
}
