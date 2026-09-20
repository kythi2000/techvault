import { redirect } from "next/navigation";
import { AdminShell } from "@/components/admin/admin-shell";
import { DeviceEditor } from "@/components/admin/device-editor";
import { adminGetAllComparisonGroups, adminGetAllReferences } from "@/lib/admin-api";
import type { AdminBrand, AdminCategory } from "@/lib/admin-contracts";
import { requireAdminSession } from "@/lib/admin-session";

export default async function NewAdminDevicePage() {
  const session = await requireAdminSession();
  const [brands, categories, groups] = await Promise.all([
    adminGetAllReferences(session.apiKey, "brands"),
    adminGetAllReferences(session.apiKey, "categories"),
    adminGetAllComparisonGroups(session.apiKey),
  ]);
  const failed = [brands, categories, groups].find((result) => !result.ok);
  if (failed && !failed.ok && failed.status === 401) redirect("/admin/login");

  return (
    <AdminShell>
      <main className="admin-main">
        <header className="admin-record-header"><div><p className="admin-kicker">New catalog record</p><h1>Create a device draft</h1></div><p>Start with identity fields. Editorial content can remain incomplete until publication.</p></header>
        {failed && !failed.ok ? (
          <div className="admin-banner admin-banner-error" role="alert"><strong>{failed.error.code}</strong><span>{failed.error.message}</span><small>Trace: {failed.error.traceId}</small></div>
        ) : brands.ok && categories.ok && groups.ok ? (
          <DeviceEditor brands={brands.data as AdminBrand[]} categories={categories.data as AdminCategory[]} comparisonGroups={groups.data} />
        ) : null}
      </main>
    </AdminShell>
  );
}
