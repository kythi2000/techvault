import type { Metadata } from "next";
import { Suspense } from "react";
import { CatalogPage } from "@/components/catalog-page";
import { LoadingState } from "@/components/loading-state";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Device archive",
  description: "Browse every published phone and computer in the TechVault archive.",
  alternates: { canonical: "/devices" },
};

export default function DevicesPage({ searchParams }: PageProps<"/devices">) {
  return (
    <Suspense fallback={<LoadingState />}>
      <CatalogPage
        route="/devices"
        eyebrow="The complete catalog"
        title="Device archive"
        introduction="Every published object in one index. Filter the collection by maker, category or era."
        searchParams={searchParams}
      />
    </Suspense>
  );
}
