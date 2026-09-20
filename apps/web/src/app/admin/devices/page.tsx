import Link from "next/link";
import { redirect } from "next/navigation";
import { AdminPagination } from "@/components/admin/admin-pagination";
import { AdminShell } from "@/components/admin/admin-shell";
import { adminListDevices } from "@/lib/admin-api";
import type { AdminStatus } from "@/lib/admin-contracts";
import { requireAdminSession } from "@/lib/admin-session";

const statuses = ["draft", "published", "archived"] as const;

function one(value: string | string[] | undefined): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function label(status: AdminStatus) {
  return status[0].toUpperCase() + status.slice(1);
}

export default async function AdminDevicesPage({ searchParams }: PageProps<"/admin/devices">) {
  const session = await requireAdminSession();
  const query = await searchParams;
  const rawStatus = one(query.status);
  const status = statuses.includes(rawStatus as AdminStatus) ? rawStatus as AdminStatus : undefined;
  const page = one(query.page) ?? "1";
  const result = await adminListDevices(session.apiKey, { page, pageSize: "24", status });
  if (!result.ok && result.status === 401) redirect("/admin/login");

  return (
    <AdminShell>
      <main className="admin-main">
        <header className="admin-page-header admin-page-header-actions">
          <div><p className="admin-kicker">Catalog records</p><h1>Devices</h1></div>
          <Link className="admin-button admin-button-primary" href="/admin/devices/new">Create device</Link>
        </header>
        <nav className="admin-filter-tabs" aria-label="Filter by status">
          <Link href="/admin/devices" aria-current={!status ? "page" : undefined}>All</Link>
          {statuses.map((item) => <Link href={`/admin/devices?status=${item}`} aria-current={status === item ? "page" : undefined} key={item}>{label(item)}</Link>)}
        </nav>

        {!result.ok ? (
          <div className="admin-banner admin-banner-error" role="alert"><strong>{result.error.code}</strong><span>{result.error.message}</span><small>Trace: {result.error.traceId}</small></div>
        ) : result.data.data.length === 0 ? (
          <div className="admin-empty"><h2>No devices in this view</h2><p>Choose another status or create a new draft.</p></div>
        ) : (
          <>
            <div className="admin-table-wrap">
              <table className="admin-table">
                <thead><tr><th>Device</th><th>Status</th><th>Slug</th><th>Updated</th><th><span className="sr-only">Action</span></th></tr></thead>
                <tbody>{result.data.data.map((device) => (
                  <tr key={device.id}>
                    <td><strong>{device.name}</strong></td>
                    <td><span className={`admin-status admin-status-${device.status}`}>{label(device.status)}</span></td>
                    <td><code>{device.slug}</code></td>
                    <td><time dateTime={device.updatedAt}>{new Date(device.updatedAt).toLocaleDateString("en-GB")}</time></td>
                    <td><Link href={`/admin/devices/${device.id}`}>Open →</Link></td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
            <AdminPagination pagination={result.data.pagination} status={status} />
          </>
        )}
      </main>
    </AdminShell>
  );
}
