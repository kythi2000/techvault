import type { Metadata } from "next";
import { Suspense } from "react";
import { CatalogPage } from "@/components/catalog-page";
import { LoadingState } from "@/components/loading-state";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Phone archive",
  description: "Explore landmark mobile phones by brand, category and decade.",
  alternates: { canonical: "/phones" },
};

export default function PhonesPage({ searchParams }: PageProps<"/phones">) {
  return (
    <Suspense fallback={<LoadingState />}>
      <CatalogPage
        route="/phones"
        eyebrow="Portable communication"
        title="Phone archive"
        introduction="The handsets that made conversations portable, from simple icons to pocket computers."
        searchParams={searchParams}
      />
    </Suspense>
  );
}
