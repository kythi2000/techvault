import { ArrowIcon } from "@/components/icons";

export function SearchForm({ query = "", pageSize, id = "search-query", invalid = false }: {
  query?: string;
  pageSize?: string;
  id?: string;
  invalid?: boolean;
}) {
  return (
    <form className="search-form discovery-form" action="/search" method="get" role="search" key={`${query}:${pageSize}`}>
      <label htmlFor={id}>Search the archive</label>
      <div className="search-input-row">
        <input
          id={id} name="q" type="search" defaultValue={query} required maxLength={200}
          placeholder="Try Nokia 3310, Macintosh, or a model number"
          aria-describedby={`${id}-help`} aria-invalid={invalid || undefined}
        />
        <button className="button button-dark" type="submit">Search <ArrowIcon /></button>
      </div>
      <p id={`${id}-help`} className="discovery-help">Use whole words from a device name, maker, model number, or description. Multiple words must all match.</p>
      {pageSize !== undefined && (
        <label className="search-page-size">Results per page
          <input name="pageSize" type="text" inputMode="numeric" pattern="[0-9]+" defaultValue={pageSize} required />
        </label>
      )}
    </form>
  );
}
