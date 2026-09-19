import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { DeviceObject } from "@/components/device-object";
import { ArrowIcon } from "@/components/icons";
import { SpecificationGroups } from "@/components/specification-groups";
import { RetryButton } from "@/components/retry-button";
import { archiveNumber, formatDate, formatDimensions } from "@/lib/format";
import { getDevice } from "@/lib/techvault-api";

export const dynamic = "force-dynamic";

export async function generateMetadata({ params }: PageProps<"/devices/[slug]">): Promise<Metadata> {
  const { slug } = await params;
  const result = await getDevice(slug);

  if (!result.ok) {
    return {
      title: result.status === 404 ? "Object not found" : "Exhibit unavailable",
      robots: { index: false, follow: false },
    };
  }

  const device = result.data.data;
  return {
    title: device.seoTitle ? { absolute: device.seoTitle } : `${device.name} — Specs & history`,
    description: device.seoDescription || device.shortDescription,
    alternates: { canonical: `/devices/${device.slug}` },
    openGraph: {
      type: "article",
      title: device.seoTitle || device.name,
      description: device.seoDescription || device.shortDescription,
      publishedTime: device.publishedAt ?? undefined,
      modifiedTime: device.updatedAt,
    },
  };
}

export default async function DevicePage({ params }: PageProps<"/devices/[slug]">) {
  const { slug } = await params;
  const result = await getDevice(slug);

  if (!result.ok && result.status === 404) notFound();

  if (!result.ok) {
    return (
      <main className="shell status-page" role="alert">
        <span className="status-code">{result.error.code}</span>
        <h1>This exhibit is temporarily unavailable.</h1>
        <p>{result.error.message}</p>
        <p className="trace">Reference: {result.error.traceId}</p>
        <RetryButton />
      </main>
    );
  }

  const device = result.data.data;
  const publishedYear = device.releaseYear ?? "Year unknown";

  return (
    <main>
      <nav className="shell breadcrumbs" aria-label="Breadcrumb">
        <Link href="/">Home</Link><span>/</span>
        <Link href="/devices">Archive</Link><span>/</span>
        <span aria-current="page">{device.name}</span>
      </nav>

      <article>
        <header className="device-hero shell">
          <div className="device-hero-copy">
            <div className="object-label">
              <span>TV–{archiveNumber(device.id)}</span>
              <span>{device.category.name}</span>
            </div>
            <p className="eyebrow">{device.brand.name} · {publishedYear}</p>
            <h1>{device.name}</h1>
            <p className="device-deck">{device.shortDescription}</p>
            <div className="hero-actions">
              <Link href={`/devices/${device.slug}/specs`} className="button button-dark">
                View full specifications <ArrowIcon />
              </Link>
              <a href="#history" className="button button-light">Read its story</a>
              <Link href={`/compare?devices=${encodeURIComponent(device.slug)}`} className="button button-light">
                Compare this object
              </Link>
            </div>
          </div>
          <DeviceObject
            name={device.name}
            releaseYear={device.releaseYear}
            categorySlug={device.category.slug}
            parentSlug={device.category.parentSlug}
            large
          />
        </header>

        <section className="key-facts">
          <div className="shell key-facts-grid">
            <div><span>Maker</span><strong><Link href={`/brands/${device.brand.slug}`}>{device.brand.name}</Link></strong></div>
            <div><span>Released</span><strong>{device.releaseDate ? formatDate(device.releaseDate) : publishedYear}</strong></div>
            <div><span>Category</span><strong><Link href={`/categories/${device.category.slug}`}>{device.category.name}</Link></strong></div>
            <div><span>Weight</span><strong>{device.physicalDetails.weightGrams === null ? "Unknown" : `${device.physicalDetails.weightGrams} g`}</strong></div>
          </div>
        </section>

        <section className="device-story shell">
          <div className="story-index">
            <span>01 / Overview</span>
            <p>Dimensions: {formatDimensions(device)}</p>
          </div>
          <div className="story-copy">
            <h2>An object in context</h2>
            {device.description ? <p>{device.description}</p> : <p>{device.shortDescription}</p>}
          </div>
        </section>

        <section className="history-section" id="history">
          <div className="shell device-story">
            <div className="story-index">
              <span>02 / History</span>
              <p>Published archive record</p>
            </div>
            <div className="story-copy">
              <h2>Why it mattered</h2>
              {device.history ? <p>{device.history}</p> : <p>Historical notes are being prepared by the archive team.</p>}
            </div>
          </div>
        </section>

        <section className="spec-preview shell">
          <div className="section-heading">
            <div>
              <span className="eyebrow">03 / Technical record</span>
              <h2>Selected specifications</h2>
            </div>
            <Link href={`/devices/${device.slug}/specs`} className="text-link">Open complete record <ArrowIcon /></Link>
          </div>
          <SpecificationGroups groups={device.specificationGroups} limit={2} />
        </section>
      </article>
    </main>
  );
}
