import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { SpecificationGroups } from "@/components/specification-groups";
import { RetryButton } from "@/components/retry-button";
import { archiveNumber } from "@/lib/format";
import { getDevice } from "@/lib/techvault-api";

export const dynamic = "force-dynamic";

export async function generateMetadata({ params }: PageProps<"/devices/[slug]/specs">): Promise<Metadata> {
  const { slug } = await params;
  const result = await getDevice(slug);

  if (!result.ok) {
    return {
      title: result.status === 404 ? "Specifications not found" : "Technical record unavailable",
      robots: { index: false, follow: false },
    };
  }

  const device = result.data.data;
  return {
    title: `${device.name} specifications`,
    description: `Complete technical specifications for the ${device.name}, organized from the TechVault archive.`,
    alternates: { canonical: `/devices/${device.slug}/specs` },
  };
}

export default async function DeviceSpecsPage({ params }: PageProps<"/devices/[slug]/specs">) {
  const { slug } = await params;
  const result = await getDevice(slug);

  if (!result.ok && result.status === 404) notFound();

  if (!result.ok) {
    return (
      <main className="shell status-page" role="alert">
        <span className="status-code">{result.error.code}</span>
        <h1>The technical record is unavailable.</h1>
        <p>{result.error.message}</p>
        <p className="trace">Reference: {result.error.traceId}</p>
        <RetryButton />
      </main>
    );
  }

  const device = result.data.data;

  return (
    <main className="spec-page shell">
      <nav className="breadcrumbs" aria-label="Breadcrumb">
        <Link href="/">Home</Link><span>/</span>
        <Link href="/devices">Archive</Link><span>/</span>
        <Link href={`/devices/${device.slug}`}>{device.name}</Link><span>/</span>
        <span aria-current="page">Specifications</span>
      </nav>

      <header className="spec-page-header">
        <div>
          <span className="eyebrow">Technical record · TV–{archiveNumber(device.id)}</span>
          <h1>{device.name}</h1>
        </div>
        <p>Every catalogued value, ordered by its archival specification group. Unknown values are never inferred.</p>
      </header>

      <div className="record-notice">
        <span>Record status</span>
        <strong>Published</strong>
        <span>Last updated</span>
        <strong>{new Date(device.updatedAt).toLocaleDateString("en", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" })}</strong>
      </div>

      <SpecificationGroups groups={device.specificationGroups} />

      <div className="spec-back-link">
        <Link href={`/devices/${device.slug}`}>← Back to {device.name}</Link>
      </div>
    </main>
  );
}
