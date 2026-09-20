import Link from "next/link";
import { redirect } from "next/navigation";
import { AdminShell } from "@/components/admin/admin-shell";
import { AdminErrorMessage } from "@/components/admin/admin-error-message";
import { adminListDevices } from "@/lib/admin-api";
import { requireAdminSession } from "@/lib/admin-session";

const sections = [
  { href: "/admin/devices", index: "01", title: "Devices", copy: "Create drafts, edit complete records, publish, archive, and maintain typed specifications." },
  { href: "/admin/references/brands", index: "02", title: "Brands", copy: "Maintain the makers used by every catalog record." },
  { href: "/admin/references/categories", index: "03", title: "Categories", copy: "Organize the archive hierarchy and editorial display order." },
  { href: "/admin/references/specification-groups", index: "04", title: "Specification groups", copy: "Control the sections used to present structured technical data." },
  { href: "/admin/references/specification-definitions", index: "05", title: "Specification definitions", copy: "Manage typed fields and their comparison eligibility." },
] as const;

export default async function AdminDashboardPage() {
  const session = await requireAdminSession();
  const check = await adminListDevices(session.apiKey, { page: "1", pageSize: "1" });
  if (!check.ok && check.status === 401) redirect("/admin/login?reauth=1");

  return (
    <AdminShell>
      <main className="admin-main">
        <header className="admin-page-header">
          <div>
            <p className="admin-kicker">Catalog control</p>
            <h1>Editorial dashboard</h1>
          </div>
          <p>Choose a collection to edit. Every save is validated against the active backend contract.</p>
        </header>

        {!check.ok && <AdminErrorMessage code={check.error.code} message={check.error.message} traceId={check.error.traceId} retryAfterSeconds={check.retryAfterSeconds} />}

        <nav className="admin-dashboard-grid" aria-label="Editorial sections">
          {sections.map((section) => (
            <Link href={section.href} className="admin-dashboard-card" key={section.href}>
              <span>{section.index}</span>
              <h2>{section.title}</h2>
              <p>{section.copy}</p>
              <strong>Open manager <span aria-hidden="true">→</span></strong>
            </Link>
          ))}
        </nav>
      </main>
    </AdminShell>
  );
}
