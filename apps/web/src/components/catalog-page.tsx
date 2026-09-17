import Link from "next/link";
import { ArrowIcon, GridIcon } from "@/components/icons";
import { DeviceCard } from "@/components/device-card";
import { RetryButton } from "@/components/retry-button";
import { normalizeSearchParams, pageHref, toQuery, type BrowseRoute, type RawSearchParams } from "@/lib/browse-query";
import {
  browseDevices,
  getBrands,
  getCategories,
} from "@/lib/techvault-api";

interface CatalogPageProps {
  route: BrowseRoute;
  eyebrow: string;
  title: string;
  introduction: string;
  searchParams: Promise<RawSearchParams>;
}

export async function CatalogPage({
  route,
  eyebrow,
  title,
  introduction,
  searchParams,
}: CatalogPageProps) {
  const params = normalizeSearchParams(await searchParams);
  const [devicesResult, brandsResult, categoriesResult] = await Promise.all([
    browseDevices(route, { ...params, pageSize: params.pageSize ?? "12" }),
    getBrands(),
    getCategories(),
  ]);
  const brands = brandsResult.ok ? brandsResult.data : [];
  const categories = categoriesResult.ok ? categoriesResult.data : [];

  return (
    <main>
      <section className="catalog-intro shell">
        <div>
          <span className="eyebrow">{eyebrow}</span>
          <h1>{title}</h1>
        </div>
        <p>{introduction}</p>
      </section>

      <section className="catalog-workspace shell" aria-label="Device catalog">
        <form className="catalog-filters" method="get" action={route} key={toQuery(params)}>
          <div className="filter-heading">
            <span>Refine archive</span>
            <Link href={route}>Reset</Link>
          </div>

          {(!brandsResult.ok || !categoriesResult.ok) && (
            <p className="filter-notice" role="status">Some filter choices could not be loaded. Your current filters are preserved.</p>
          )}

          <label>
            Brand
            <select name="brand" defaultValue={params.brand ?? ""}>
              <option value="">All brands</option>
              {params.brand && !brands.some((brand) => brand.slug === params.brand) && (
                <option value={params.brand}>{params.brand} (current filter)</option>
              )}
              {brands.map((brand) => (
                <option value={brand.slug} key={brand.id}>
                  {brand.name} ({brand.publishedDeviceCount})
                </option>
              ))}
            </select>
          </label>

          <label>
            Category
            <select name="category" defaultValue={params.category ?? ""}>
              <option value="">All categories</option>
              {params.category && !categories.some((category) => category.slug === params.category) && (
                <option value={params.category}>{params.category} (current filter)</option>
              )}
              {categories.map((category) => (
                <option value={category.slug} key={category.id}>
                  {category.parentSlug ? "— " : ""}{category.name}
                </option>
              ))}
            </select>
          </label>

          <label>
            Decade
            <input name="decade" type="number" min="10" max="9990" step="10" defaultValue={params.decade ?? ""} placeholder="e.g. 2000" />
          </label>

          <label>
            Sort by
            <select name="sort" defaultValue={params.sort ?? "release-desc"}>
              {params.sort && !["release-desc", "release-asc", "name-asc", "name-desc"].includes(params.sort) && (
                <option value={params.sort}>{params.sort} (unsupported)</option>
              )}
              <option value="release-desc">Newest first</option>
              <option value="release-asc">Oldest first</option>
              <option value="name-asc">Name A–Z</option>
              <option value="name-desc">Name Z–A</option>
            </select>
          </label>

          <details className="advanced-filters" open={Boolean(params.type || params.year || params.fromYear || params.toYear || params.pageSize)}>
            <summary>More filters</summary>
            <label>
              Device type
              <select name="type" defaultValue={params.type ?? ""}>
                <option value="">{route === "/devices" ? "All types" : route === "/phones" ? "Phones" : "Computers"}</option>
                {params.type && !["phones", "computers"].includes(params.type) && <option value={params.type}>{params.type} (unsupported)</option>}
                <option value="phones">Phones</option>
                <option value="computers">Computers</option>
              </select>
            </label>
            <label>Exact year<input name="year" type="number" min="1" max="9999" defaultValue={params.year ?? ""} /></label>
            <label>From year<input name="fromYear" type="number" min="1" max="9999" defaultValue={params.fromYear ?? ""} /></label>
            <label>To year<input name="toYear" type="number" min="1" max="9999" defaultValue={params.toYear ?? ""} /></label>
            <label>Records per page<input name="pageSize" type="number" min="1" max="100" defaultValue={params.pageSize ?? "12"} /></label>
          </details>

          <button className="button button-dark" type="submit">
            Apply filters <ArrowIcon />
          </button>
        </form>

        <div className="catalog-results">
          {devicesResult.ok ? (
            <>
              <div className="results-heading">
                <div>
                  <span className="eyebrow" id="catalog-results-heading">Catalogued objects</span>
                  <strong>{devicesResult.data.pagination.total} {devicesResult.data.pagination.total === 1 ? "record" : "records"}</strong>
                </div>
                <span className="view-indicator"><GridIcon /> Grid view</span>
              </div>

              {devicesResult.data.data.length > 0 ? (
                <div className="device-grid">
                  {devicesResult.data.data.map((device) => (
                    <DeviceCard device={device} key={device.id} />
                  ))}
                </div>
              ) : (
                <div className="empty-state">
                  <span>NO MATCHING OBJECTS</span>
                  <h2>{devicesResult.data.pagination.total > 0 ? "You’re past the last page." : "This cabinet is still empty."}</h2>
                  <p>{devicesResult.data.pagination.total > 0 ? "There are matching records on earlier pages." : "No published records match this view. Try a wider era or check back as the archive grows."}</p>
                  <Link href={devicesResult.data.pagination.total > 0 ? pageHref(route, params, 1) : route} className="text-link">
                    {devicesResult.data.pagination.total > 0 ? "Go to first page" : "Clear all filters"} <ArrowIcon />
                  </Link>
                </div>
              )}

              {devicesResult.data.pagination.totalPages > 1 && devicesResult.data.pagination.page <= devicesResult.data.pagination.totalPages && (
                <nav className="pagination" aria-label="Catalog pagination">
                  {devicesResult.data.pagination.page > 1 ? (
                    <Link href={pageHref(route, params, devicesResult.data.pagination.page - 1)}>← Previous</Link>
                  ) : <span />}
                  <span>
                    Page {devicesResult.data.pagination.page} of {devicesResult.data.pagination.totalPages}
                  </span>
                  {devicesResult.data.pagination.page < Math.min(10000, devicesResult.data.pagination.totalPages) ? (
                    <Link href={pageHref(route, params, devicesResult.data.pagination.page + 1)}>Next →</Link>
                  ) : <span />}
                </nav>
              )}
            </>
          ) : (
            <div className="error-state" role="alert">
              <span>{devicesResult.error.code}</span>
              <h2>The archive could not be opened.</h2>
              <p>{devicesResult.error.message}</p>
              <p className="trace">Reference: {devicesResult.error.traceId}</p>
              <RetryButton />
            </div>
          )}
        </div>
      </section>
    </main>
  );
}
