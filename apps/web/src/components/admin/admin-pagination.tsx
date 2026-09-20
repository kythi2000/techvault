import Link from "next/link";
import type { Pagination } from "@/lib/contracts";

function href(page: number, status?: string) {
  const query = new URLSearchParams({ page: String(page) });
  if (status) query.set("status", status);
  return `/admin/devices?${query}`;
}

export function AdminPagination({ pagination, status }: { pagination: Pagination; status?: string }) {
  if (pagination.totalPages <= 1 || pagination.page > pagination.totalPages) return null;
  return (
    <nav className="admin-pagination" aria-label="Device pagination">
      {pagination.page > 1 ? <Link href={href(pagination.page - 1, status)}>← Previous</Link> : <span />}
      <span>Page {pagination.page} of {pagination.totalPages}</span>
      {pagination.page < pagination.totalPages ? <Link href={href(pagination.page + 1, status)}>Next →</Link> : <span />}
    </nav>
  );
}
