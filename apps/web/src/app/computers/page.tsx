import type { Metadata } from "next";
import { Suspense } from "react";
import { CatalogPage } from "@/components/catalog-page";
import { LoadingState } from "@/components/loading-state";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Computer archive",
  description: "Explore desktop computers, laptops, all-in-ones and workstations through history.",
  alternates: { canonical: "/computers" },
};

export default function ComputersPage({ searchParams }: PageProps<"/computers">) {
  return (
    <Suspense fallback={<LoadingState />}>
      <CatalogPage
        route="/computers"
        eyebrow="Machines for thought"
        title="Computer archive"
        introduction="Personal machines that changed the shape of work, creativity and the connected home."
        searchParams={searchParams}
      />
    </Suspense>
  );
}
