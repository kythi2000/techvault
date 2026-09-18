import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import { DeviceCard } from "@/components/device-card";
import { DiscoveryError } from "@/components/discovery-error";
import { DiscoveryPagination } from "@/components/discovery-pagination";
import { LoadingState } from "@/components/loading-state";
import { TimelineFilters } from "@/components/timeline-filters";
import { TimelineScroller } from "@/components/timeline-scroller";
import { discoveryPageHref, normalizeTimelineQuery, toTimelineQuery } from "@/lib/discovery-query";
import { formatDate } from "@/lib/format";
import { getBrands, getCategories, getTimeline } from "@/lib/techvault-api";

export const dynamic = "force-dynamic";

export async function generateMetadata({ searchParams }: PageProps<"/timeline">): Promise<Metadata> {
  const params = normalizeTimelineQuery(await searchParams);
  return {
    title: "Technology timeline",
    description: "Explore phones and computers in release order, with filters for makers, categories, and eras.",
    alternates: { canonical: "/timeline" },
    robots: Object.keys(params).length > 0 ? { index: false, follow: true } : undefined,
  };
}

async function TimelineContent({ searchParams }: PageProps<"/timeline">) {
  const params = normalizeTimelineQuery(await searchParams);
  const [result, brands, categories] = await Promise.all([
    getTimeline({ ...params, pageSize: params.pageSize ?? "12" }), getBrands(), getCategories(),
  ]);

  return (
    <main className="shell discovery-page">
      <header className="discovery-heading">
        <span className="eyebrow">The archive through time</span>
        <h1>Technology timeline</h1>
        <p>Follow the release years of the phones and computers in the archive, from earliest to latest.</p>
        <nav className="discovery-links" aria-label="Timeline collections">
          <Link href="/timeline" className="text-link">All devices</Link>
          <Link href="/timeline?type=phones" className="text-link">Phone timeline</Link>
          <Link href="/timeline?type=computers" className="text-link">Computer timeline</Link>
        </nav>
      </header>
      <TimelineFilters params={params} brands={brands.ok ? brands.data : []} categories={categories.ok ? categories.data : []} referencesUnavailable={!brands.ok || !categories.ok} />
      <section className="discovery-results" aria-labelledby="timeline-results-heading">
        <h2 id="timeline-results-heading">In release order</h2>
        <p className="discovery-help">Only objects with a known release year appear here. <Link href="/devices">Browse the full archive</Link> for records with unknown dates.</p>
        {!result.ok ? <DiscoveryError error={result.error} status={result.status} formId="timeline-filters" /> : (
          <>
            <p className="discovery-result-summary">{result.data.pagination.total} {result.data.pagination.total === 1 ? "object" : "objects"} · Oldest first · {result.data.data.length} on this page</p>
            {result.data.data.length > 0 ? (
              <TimelineScroller key={toTimelineQuery(params)}>
                <ol className="timeline-track">
                  {result.data.data.map((device) => (
                    <li className="timeline-entry" key={device.id}>
                      <div className="timeline-date">
                        <strong>{device.releaseYear}</strong>
                        {device.releaseDate
                          ? <time dateTime={device.releaseDate}>{formatDate(device.releaseDate)}</time>
                          : <span>Exact release date unknown</span>}
                      </div>
                      <DeviceCard device={device} />
                    </li>
                  ))}
                </ol>
              </TimelineScroller>
            ) : (
              <div className="empty-state">
                <span>{result.data.pagination.total > 0 ? "PAGE OUTSIDE TIMELINE" : "NO DATED OBJECTS"}</span>
                <h3>{result.data.pagination.total > 0 ? "You’re past the last page." : "No objects in this period."}</h3>
                <p>{result.data.pagination.total > 0 ? "There are matching records on earlier pages." : "Try widening the years or removing a filter. All selected filters must match."}</p>
                <Link href={result.data.pagination.total > 0 ? discoveryPageHref("/timeline", params, 1) : "/timeline"} className="text-link">
                  {result.data.pagination.total > 0 ? "Go to first page" : "Clear all filters"}
                </Link>
              </div>
            )}
            <DiscoveryPagination route="/timeline" params={params} pagination={result.data.pagination} />
          </>
        )}
      </section>
    </main>
  );
}

export default function TimelinePage(props: PageProps<"/timeline">) {
  return <Suspense fallback={<LoadingState />}><TimelineContent {...props} /></Suspense>;
}
