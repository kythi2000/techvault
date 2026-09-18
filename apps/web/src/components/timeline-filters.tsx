import Link from "next/link";
import { ArrowIcon } from "@/components/icons";
import type { Brand, Category } from "@/lib/contracts";
import { toTimelineQuery, type TimelineParams } from "@/lib/discovery-query";

export function TimelineFilters({ params, brands, categories, referencesUnavailable }: {
  params: TimelineParams;
  brands: Brand[];
  categories: Category[];
  referencesUnavailable: boolean;
}) {
  return (
    <form action="/timeline" method="get" className="discovery-form timeline-filters" id="timeline-filters" tabIndex={-1} key={toTimelineQuery(params)}>
      <div className="filter-heading"><span>Refine the timeline</span><Link href="/timeline">Reset</Link></div>
      {referencesUnavailable && <p className="filter-notice" role="status">Some filter choices could not be loaded. Your current filters are preserved.</p>}
      <div className="timeline-filter-grid">
        <label>Device type
          <select name="type" defaultValue={params.type ?? ""}>
            <option value="">All devices</option>
            {params.type && !["phones", "computers"].includes(params.type) && <option value={params.type}>{params.type} (current filter)</option>}
            <option value="phones">Phones</option><option value="computers">Computers</option>
          </select>
        </label>
        <label>Brand
          <select name="brand" defaultValue={params.brand ?? ""}>
            <option value="">All brands</option>
            {params.brand && !brands.some((brand) => brand.slug === params.brand) && <option value={params.brand}>{params.brand} (current filter)</option>}
            {brands.map((brand) => <option value={brand.slug} key={brand.id}>{brand.name}</option>)}
          </select>
        </label>
        <label>Category
          <select name="category" defaultValue={params.category ?? ""}>
            <option value="">All categories</option>
            {params.category && !categories.some((category) => category.slug === params.category) && <option value={params.category}>{params.category} (current filter)</option>}
            {categories.map((category) => <option value={category.slug} key={category.id}>{category.parentSlug ? "— " : ""}{category.name}</option>)}
          </select>
        </label>
        <label>Era
          <input name="era" type="text" defaultValue={params.era ?? ""} placeholder="e.g. 1990s" aria-describedby="era-help" />
        </label>
      </div>
      <p className="discovery-help" id="era-help">An era covers one decade: 1990s means 1990–1999. Use the year fields below for other periods. All filters apply together.</p>
      <details className="timeline-advanced" open={Boolean(params.year || params.fromYear || params.toYear || params.pageSize)}>
        <summary>Year range and page size</summary>
        <div className="timeline-filter-grid">
          <label>Exact year<input name="year" type="text" inputMode="numeric" pattern="[0-9]+" defaultValue={params.year ?? ""} placeholder="1–9999" /></label>
          <label>From year<input name="fromYear" type="text" inputMode="numeric" pattern="[0-9]+" defaultValue={params.fromYear ?? ""} placeholder="e.g. 1980" /></label>
          <label>To year<input name="toYear" type="text" inputMode="numeric" pattern="[0-9]+" defaultValue={params.toYear ?? ""} placeholder="e.g. 2000" /></label>
          <label>Records per page<input name="pageSize" type="text" inputMode="numeric" pattern="[0-9]+" defaultValue={params.pageSize ?? "12"} required /></label>
        </div>
      </details>
      <button className="button button-dark" type="submit">Apply filters <ArrowIcon /></button>
    </form>
  );
}
