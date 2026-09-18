import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import { DeviceCard } from "@/components/device-card";
import { DiscoveryError } from "@/components/discovery-error";
import { DiscoveryPagination } from "@/components/discovery-pagination";
import { LoadingState } from "@/components/loading-state";
import { SearchForm } from "@/components/search-form";
import { discoveryPageHref, normalizeSearchQuery } from "@/lib/discovery-query";
import { searchDevices } from "@/lib/techvault-api";

export const dynamic = "force-dynamic";
export const metadata: Metadata = {
  title: "Search the archive",
  description: "Find phones and computers by name, maker, model number, and description.",
  alternates: { canonical: "/search" },
  robots: { index: false, follow: true },
};

async function SearchContent({ searchParams }: PageProps<"/search">) {
  const params = normalizeSearchQuery(await searchParams);
  const result = params.q === undefined ? null : await searchDevices({ ...params, pageSize: params.pageSize ?? "12" });

  return (
    <main className="shell discovery-page">
      <header className="discovery-heading">
        <span className="eyebrow">Find an object</span>
        <h1>Search the archive</h1>
        <p>A name, a maker, a detail you remember. Start there.</p>
      </header>
      <SearchForm query={params.q} pageSize={params.pageSize ?? (result === null ? undefined : "12")} invalid={result !== null && !result.ok && result.status === 400} />

      <section className="discovery-results" aria-labelledby="search-results-heading">
        <h2 id="search-results-heading">{result === null ? "What are you looking for?" : "Search results"}</h2>
        {result === null ? (
          <div className="discovery-intro">
            <p>Enter a search above, or explore the archive by collection or release year.</p>
            <div className="discovery-links">
              <Link href="/devices" className="text-link">Browse all devices</Link>
              <Link href="/timeline" className="text-link">Explore the timeline</Link>
            </div>
          </div>
        ) : !result.ok ? (
          <DiscoveryError error={result.error} status={result.status} formId="search-query" />
        ) : (
          <>
            <p className="discovery-result-summary">{result.data.pagination.total} {result.data.pagination.total === 1 ? "match" : "matches"} for “{params.q}” · Most relevant first</p>
            {result.data.data.length > 0 ? (
              <div className="device-grid">
                {result.data.data.map((device) => <DeviceCard device={device} key={device.id} />)}
              </div>
            ) : (
              <div className="empty-state">
                <span>{result.data.pagination.total > 0 ? "PAGE OUTSIDE RESULTS" : "NO MATCHES"}</span>
                <h3>{result.data.pagination.total > 0 ? "You’re past the last page." : "No objects match those words."}</h3>
                <p>{result.data.pagination.total > 0 ? "Your search has matches on earlier pages." : "Check the spelling or try fewer, complete words. Partial words and typo correction are not supported."}</p>
                {result.data.pagination.total > 0
                  ? <Link href={discoveryPageHref("/search", params, 1)} className="text-link">Go to first page</Link>
                  : <a href="#search-query" className="text-link">Try another search</a>}
              </div>
            )}
            <DiscoveryPagination route="/search" params={params} pagination={result.data.pagination} />
          </>
        )}
      </section>
    </main>
  );
}

export default function SearchPage(props: PageProps<"/search">) {
  return <Suspense fallback={<LoadingState />}><SearchContent {...props} /></Suspense>;
}
