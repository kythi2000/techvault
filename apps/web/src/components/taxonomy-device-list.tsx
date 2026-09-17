import Link from "next/link";
import { ArrowIcon } from "@/components/icons";
import { DeviceCard } from "@/components/device-card";
import { RetryButton } from "@/components/retry-button";
import type { DeviceCard as DeviceCardModel } from "@/lib/contracts";
import type { ApiResult, PaginatedData } from "@/lib/techvault-api";

interface TaxonomyDeviceListProps {
  result: ApiResult<PaginatedData<DeviceCardModel>>;
  path: string;
  emptyTitle: string;
}

export function TaxonomyDeviceList({ result, path, emptyTitle }: TaxonomyDeviceListProps) {
  if (!result.ok) {
    return (
      <div className="error-state" role="alert">
        <span>{result.error.code}</span>
        <h2>The collection could not be opened.</h2>
        <p>{result.error.message}</p>
        <p className="trace">Reference: {result.error.traceId}</p>
        <RetryButton />
      </div>
    );
  }

  const { data, pagination } = result.data;
  if (data.length === 0) {
    return (
      <div className="empty-state">
        <span>{pagination.total > 0 ? "PAGE OUTSIDE COLLECTION" : "COLLECTION IN PROGRESS"}</span>
        <h2>{pagination.total > 0 ? "You’re past the last page." : emptyTitle}</h2>
        <p>{pagination.total > 0 ? "There are published objects on earlier pages." : "No published objects are assigned here yet."}</p>
        {pagination.total > 0 && <Link href={path} className="text-link">Go to first page <ArrowIcon /></Link>}
      </div>
    );
  }

  const separator = path.includes("?") ? "&" : "?";
  const page = pagination.page;
  return (
    <>
      <div className="taxonomy-result-count">{pagination.total} {pagination.total === 1 ? "published object" : "published objects"}</div>
      <div className="device-grid">
        {data.map((device) => <DeviceCard device={device} key={device.id} />)}
      </div>
      {pagination.totalPages > 1 && page <= pagination.totalPages && (
        <nav className="pagination" aria-label="Collection pagination">
          {page > 1 ? <Link href={`${path}${separator}page=${page - 1}`}>← Previous</Link> : <span />}
          <span>Page {page} of {pagination.totalPages}</span>
          {page < Math.min(10000, pagination.totalPages) ? <Link href={`${path}${separator}page=${page + 1}`}>Next →</Link> : <span />}
        </nav>
      )}
    </>
  );
}
