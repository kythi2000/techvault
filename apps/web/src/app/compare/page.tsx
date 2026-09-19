import type { Metadata } from "next";
import { Suspense } from "react";
import { ComparisonError } from "@/components/comparison-error";
import { ComparisonSelector } from "@/components/comparison-selector";
import { ComparisonTable } from "@/components/comparison-table";
import { LoadingState } from "@/components/loading-state";
import { normalizeComparisonQuery } from "@/lib/comparison-query";
import { compareDevices, getAllDevices } from "@/lib/techvault-api";

export const dynamic = "force-dynamic";

export async function generateMetadata({ searchParams }: PageProps<"/compare">): Promise<Metadata> {
  const params = normalizeComparisonQuery(await searchParams);
  const hasState = Object.keys(params).length > 0;
  return {
    title: "Compare devices",
    description: "Compare two compatible phones or computers by their structured technical specifications.",
    alternates: { canonical: "/compare" },
    robots: { index: !hasState, follow: true },
  };
}

async function CompareContent({ searchParams }: PageProps<"/compare">) {
  const params = normalizeComparisonQuery(await searchParams);
  const selectedSlugs = params.devices?.split(",") ?? [];
  const isPartialSelection = selectedSlugs.length === 1 && /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(selectedSlugs[0]);
  const [deviceOptions, result] = await Promise.all([
    getAllDevices(),
    params.devices === undefined || isPartialSelection ? Promise.resolve(null) : compareDevices(params),
  ]);

  return (
    <main className="shell discovery-page comparison-page">
      <header className="discovery-heading">
        <span className="eyebrow">Side by side</span>
        <h1>Compare the record, not the reputation.</h1>
        <p>Choose two compatible objects and inspect aligned technical evidence without scores, winners, or invented precision.</p>
      </header>

      {deviceOptions.ok ? (
        <ComparisonSelector
          key={`${params.devices ?? "empty"}:${params.differencesOnly ?? "false"}`}
          devices={deviceOptions.data}
          selectedSlugs={selectedSlugs}
          differencesOnly={params.differencesOnly === "true"}
          invalid={result !== null && !result.ok && result.status === 400}
        />
      ) : (
        <div className="error-state" role="alert">
          <span>{deviceOptions.error.code}</span>
          <h2>The object list is temporarily unavailable.</h2>
          <p>{deviceOptions.error.message}</p>
          <p className="trace">Reference: {deviceOptions.error.traceId}</p>
        </div>
      )}

      <section className="comparison-results" aria-labelledby="comparison-results-heading">
        <h2 id="comparison-results-heading">{result === null ? "Choose two objects" : "Technical comparison"}</h2>
        {result === null ? (
          <div className="discovery-intro">
            <p>The two phone records and the two all-in-one computer records in the launch archive have explicit compatibility assignments.</p>
          </div>
        ) : result.ok ? (
          <ComparisonTable comparison={result.data} />
        ) : (
          <ComparisonError error={result.error} retryAfterSeconds={result.retryAfterSeconds} />
        )}
      </section>
    </main>
  );
}

export default function ComparePage(props: PageProps<"/compare">) {
  return <Suspense fallback={<LoadingState />}><CompareContent {...props} /></Suspense>;
}
