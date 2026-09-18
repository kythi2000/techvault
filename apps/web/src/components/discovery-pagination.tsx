import Link from "next/link";
import type { Pagination } from "@/lib/contracts";
import { discoveryPageHref, type DiscoveryRoute, type SearchParams, type TimelineParams } from "@/lib/discovery-query";

export function DiscoveryPagination({ route, params, pagination }: {
  route: DiscoveryRoute;
  params: SearchParams | TimelineParams;
  pagination: Pagination;
}) {
  const { page, totalPages } = pagination;
  if (totalPages <= 1 || page > totalPages) return null;

  return (
    <nav className="pagination" aria-label={route === "/search" ? "Search pagination" : "Timeline pagination"}>
      {page > 1 ? <Link href={discoveryPageHref(route, params, page - 1)}>← Previous</Link> : <span />}
      <span>Page {page} of {totalPages}</span>
      {page < Math.min(10000, totalPages) ? <Link href={discoveryPageHref(route, params, page + 1)}>Next →</Link> : <span />}
    </nav>
  );
}
