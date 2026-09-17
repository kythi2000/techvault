import type { Metadata } from "next";
import Link from "next/link";
import { ArrowIcon } from "@/components/icons";
import { RetryButton } from "@/components/retry-button";
import { getCategories } from "@/lib/techvault-api";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Device categories",
  description: "Browse the classifications used to organize the TechVault device archive.",
  alternates: { canonical: "/categories" },
};

export default async function CategoriesPage() {
  const result = await getCategories();
  const categories = result.ok
    ? [...result.data].sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name))
    : [];
  const names = new Map(categories.map((category) => [category.slug, category.name]));

  return (
    <main className="shell taxonomy-index">
      <header className="taxonomy-index-header">
        <span className="eyebrow">Archive index / Classification</span>
        <h1>Categories</h1>
        <p>Explore the device families used to organize objects across the archive.</p>
      </header>

      {!result.ok ? (
        <div className="error-state" role="alert">
          <span>{result.error.code}</span>
          <h2>The category index could not be opened.</h2>
          <p>{result.error.message}</p>
          <p className="trace">Reference: {result.error.traceId}</p>
          <RetryButton />
        </div>
      ) : categories.length === 0 ? (
        <div className="empty-state">
          <span>COLLECTION IN PROGRESS</span>
          <h2>No classifications are published yet.</h2>
          <p>Categories will appear here when the archive taxonomy is available.</p>
        </div>
      ) : (
        <ol className="taxonomy-list">
          {categories.map((category, index) => (
            <li key={category.id}>
              <Link href={`/categories/${category.slug}`}>
                <span className="taxonomy-number">{String(index + 1).padStart(2, "0")}</span>
                <span className="taxonomy-name">{category.name}</span>
                <span className="taxonomy-count">
                  {category.parentSlug ? `Within ${names.get(category.parentSlug) ?? category.parentSlug}` : "Root collection"}
                </span>
                <ArrowIcon />
              </Link>
            </li>
          ))}
        </ol>
      )}
    </main>
  );
}
