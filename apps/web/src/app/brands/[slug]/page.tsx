import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { RetryButton } from "@/components/retry-button";
import { TaxonomyDeviceList } from "@/components/taxonomy-device-list";
import { browseDevices, getBrand } from "@/lib/techvault-api";

export const dynamic = "force-dynamic";

export async function generateMetadata({ params }: PageProps<"/brands/[slug]">): Promise<Metadata> {
  const { slug } = await params;
  const result = await getBrand(slug);

  if (!result.ok) {
    return {
      title: result.status === 404 ? "Brand not found" : "Brand unavailable",
      robots: { index: false, follow: false },
    };
  }

  const brand = result.data.data;
  return {
    title: `${brand.name} devices`,
    description: brand.description || `Explore published ${brand.name} devices in the TechVault archive.`,
    alternates: { canonical: `/brands/${brand.slug}` },
    robots: brand.publishedDeviceCount === 0 ? { index: false, follow: true } : undefined,
  };
}

export default async function BrandPage({ params, searchParams }: PageProps<"/brands/[slug]">) {
  const [{ slug }, query] = await Promise.all([params, searchParams]);
  const rawPage = Array.isArray(query.page) ? query.page[0] : query.page;
  const [brandResult, devicesResult] = await Promise.all([
    getBrand(slug),
    browseDevices("/devices", { brand: slug, page: rawPage ?? "1", pageSize: "12", sort: "release-asc" }),
  ]);

  if (!brandResult.ok && brandResult.status === 404) notFound();

  if (!brandResult.ok) {
    return (
      <main className="shell status-page" role="alert">
        <span className="status-code">{brandResult.error.code}</span>
        <h1>This maker record is temporarily unavailable.</h1>
        <p>{brandResult.error.message}</p>
        <p className="trace">Reference: {brandResult.error.traceId}</p>
        <RetryButton />
      </main>
    );
  }

  const brand = brandResult.data.data;
  return (
    <main>
      <nav className="shell breadcrumbs" aria-label="Breadcrumb">
        <Link href="/">Home</Link><span>/</span>
        <Link href="/brands">Brands</Link><span>/</span>
        <span aria-current="page">{brand.name}</span>
      </nav>
      <header className="shell taxonomy-hero">
        <div>
          <span className="eyebrow">Maker record / {brand.slug}</span>
          <h1>{brand.name}</h1>
        </div>
        <div className="taxonomy-hero-copy">
          <p>{brand.description || "An archival maker record awaiting further editorial context."}</p>
          <span>{brand.publishedDeviceCount} {brand.publishedDeviceCount === 1 ? "published object" : "published objects"}</span>
        </div>
      </header>
      <section className="shell taxonomy-collection" aria-labelledby="brand-objects">
        <div className="section-heading">
          <div>
            <span className="eyebrow">From the archive</span>
            <h2 id="brand-objects">Objects by {brand.name}</h2>
          </div>
          <Link href="/brands" className="text-link">All brands</Link>
        </div>
        <TaxonomyDeviceList
          result={devicesResult}
          path={`/brands/${brand.slug}`}
          emptyTitle="This maker has no published objects."
        />
      </section>
    </main>
  );
}
