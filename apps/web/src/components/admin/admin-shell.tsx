import Link from "next/link";
import type { ReactNode } from "react";
import { logoutAction } from "@/app/admin/actions";

const navigation = [
  { href: "/admin", label: "Overview" },
  { href: "/admin/devices", label: "Devices" },
  { href: "/admin/references/brands", label: "Brands" },
  { href: "/admin/references/categories", label: "Categories" },
  { href: "/admin/references/specification-groups", label: "Spec groups" },
  { href: "/admin/references/specification-definitions", label: "Definitions" },
] as const;

export function AdminShell({ children }: { children: ReactNode }) {
  return (
    <div className="admin-workspace">
      <aside className="admin-sidebar">
        <Link href="/admin" className="admin-wordmark" aria-label="TechVault editorial dashboard">
          <span>TV</span> TechVault editorial
        </Link>
        <nav aria-label="Admin navigation">
          {navigation.map((item) => <Link href={item.href} key={item.href}>{item.label}</Link>)}
        </nav>
        <div className="admin-sidebar-footer">
          <Link href="/" target="_blank">View public archive ↗</Link>
          <form action={logoutAction}>
            <button type="submit">End session</button>
          </form>
        </div>
      </aside>
      <div className="admin-content">{children}</div>
    </div>
  );
}
