export const browseKeys = [
  "brand", "category", "type", "year", "fromYear", "toYear", "decade", "sort", "page", "pageSize",
] as const;

export type BrowseParams = Partial<Record<typeof browseKeys[number], string>>;
export type BrowseRoute = "/devices" | "/phones" | "/computers";
export type RawSearchParams = Record<string, string | string[] | undefined>;

export function normalizeSearchParams(raw: RawSearchParams): BrowseParams {
  const params: BrowseParams = {};
  for (const key of browseKeys) {
    const rawValue = raw[key];
    const value = Array.isArray(rawValue) ? rawValue[0] : rawValue;
    // Empty GET form controls mean no filter; other input is validated by the API.
    if (value !== undefined && value !== "") params[key] = value;
  }
  return params;
}

export function toQuery(params: BrowseParams): string {
  const query = new URLSearchParams();
  for (const key of browseKeys) {
    const value = params[key];
    if (value !== undefined && value !== "") query.set(key, value);
  }
  const serialized = query.toString();
  return serialized ? `?${serialized}` : "";
}

export function pageHref(route: BrowseRoute, params: BrowseParams, page: number): string {
  return `${route}${toQuery({ ...params, page: String(page) })}`;
}
