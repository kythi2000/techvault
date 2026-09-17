import type { Metadata } from "next";
import Link from "next/link";
import { ArrowIcon } from "@/components/icons";
import { RetryButton } from "@/components/retry-button";
import { getBrands } from "@/lib/techvault-api";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Technology brands",
  description: "Browse the makers represented in the TechVault device archive.",
  alternates: { canonical: "/brands" },
};

export default async function BrandsPage() {
  const result = await getBrands();

  return (
    <main className="shell taxonomy-index">
      <header className="taxonomy-index-header">
        <span className="eyebrow">Archive index / Makers</span>
        <h1>Brands</h1>
        <p>The companies behind the objects: their catalogued devices, context, and place in the archive.</p>
      </header>

      {!result.ok ? (
        <div className="error-state" role="alert">
          <span>{result.error.code}</span>
          <h2>The maker index could not be opened.</h2>
          <p>{result.error.message}</p>
          <p className="trace">Reference: {result.error.traceId}</p>
          <RetryButton />
        </div>
      ) : result.data.filter((brand) => brand.publishedDeviceCount > 0).length === 0 ? (
        <div className="empty-state">
          <span>COLLECTION IN PROGRESS</span>
          <h2>No makers are represented yet.</h2>
          <p>Published brands will appear here as objects enter the archive.</p>
        </div>
      ) : (
        <ol className="taxonomy-list">
          {result.data.filter((brand) => brand.publishedDeviceCount > 0).map((brand, index) => (
            <li key={brand.id}>
              <Link href={`/brands/${brand.slug}`}>
                <span className="taxonomy-number">{String(index + 1).padStart(2, "0")}</span>
                <span className="taxonomy-name">{brand.name}</span>
                <span className="taxonomy-count">
                  {brand.publishedDeviceCount} {brand.publishedDeviceCount === 1 ? "object" : "objects"}
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
