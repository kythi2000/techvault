import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { RetryButton } from "@/components/retry-button";
import { TaxonomyDeviceList } from "@/components/taxonomy-device-list";
import { browseDevices, getCategory } from "@/lib/techvault-api";

export const dynamic = "force-dynamic";

export async function generateMetadata({ params }: PageProps<"/categories/[slug]">): Promise<Metadata> {
  const { slug } = await params;
  const [categoryResult, devicesResult] = await Promise.all([
    getCategory(slug),
    browseDevices("/devices", { category: slug, page: "1", pageSize: "1" }),
  ]);

  if (!categoryResult.ok) {
    return {
      title: categoryResult.status === 404 ? "Category not found" : "Category unavailable",
      robots: { index: false, follow: false },
    };
  }

  const category = categoryResult.data;
  return {
    title: `${category.name} archive`,
    description: category.description || `Explore ${category.name.toLowerCase()} in the TechVault archive.`,
    alternates: { canonical: `/categories/${category.slug}` },
    robots: !devicesResult.ok || devicesResult.data.pagination.total === 0
      ? { index: false, follow: true }
      : undefined,
  };
}

export default async function CategoryPage({ params, searchParams }: PageProps<"/categories/[slug]">) {
  const [{ slug }, query] = await Promise.all([params, searchParams]);
  const rawPage = Array.isArray(query.page) ? query.page[0] : query.page;
  const [categoryResult, devicesResult] = await Promise.all([
    getCategory(slug),
    browseDevices("/devices", { category: slug, page: rawPage ?? "1", pageSize: "12", sort: "release-asc" }),
  ]);

  if (!categoryResult.ok && categoryResult.status === 404) notFound();

  if (!categoryResult.ok) {
    return (
      <main className="shell status-page" role="alert">
        <span className="status-code">{categoryResult.error.code}</span>
        <h1>This category is temporarily unavailable.</h1>
        <p>{categoryResult.error.message}</p>
        <p className="trace">Reference: {categoryResult.error.traceId}</p>
        <RetryButton />
      </main>
    );
  }

  const category = categoryResult.data;
  return (
    <main>
      <nav className="shell breadcrumbs" aria-label="Breadcrumb">
        <Link href="/">Home</Link><span>/</span>
        <Link href="/categories">Categories</Link><span>/</span>
        <span aria-current="page">{category.name}</span>
      </nav>
      <header className="shell taxonomy-hero">
        <div>
          <span className="eyebrow">Classification / {category.slug}</span>
          <h1>{category.name}</h1>
        </div>
        <div className="taxonomy-hero-copy">
          <p>{category.description || "An archive classification awaiting further editorial context."}</p>
          <Link href={`/timeline?category=${category.slug}`} className="text-link">Explore this category through time</Link>
          {category.parentSlug && <Link href={`/categories/${category.parentSlug}`} className="text-link">View parent collection</Link>}
        </div>
      </header>
      <section className="shell taxonomy-collection" aria-labelledby="category-objects">
        <div className="section-heading">
          <div>
            <span className="eyebrow">Filed in this category</span>
            <h2 id="category-objects">Published objects</h2>
          </div>
          <Link href="/categories" className="text-link">All categories</Link>
        </div>
        <TaxonomyDeviceList
          result={devicesResult}
          path={`/categories/${category.slug}`}
          emptyTitle="This category has no published objects."
        />
      </section>
    </main>
  );
}
