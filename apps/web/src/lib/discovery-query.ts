import type { RawSearchParams } from "./browse-query.ts";

const searchKeys = ["q", "page", "pageSize"] as const;
const timelineKeys = ["brand", "category", "type", "year", "fromYear", "toYear", "era", "page", "pageSize"] as const;

export type SearchParams = Partial<Record<typeof searchKeys[number], string>>;
export type TimelineParams = Partial<Record<typeof timelineKeys[number], string>>;
export type DiscoveryRoute = "/search" | "/timeline";

function pick<K extends string>(raw: RawSearchParams, keys: readonly K[]): Partial<Record<K, string>> {
  const params: Partial<Record<K, string>> = {};
  for (const key of keys) {
    const rawValue = raw[key];
    const value = Array.isArray(rawValue) ? rawValue[0] : rawValue;
    // Preserve an explicitly empty q so a submitted invalid search is not an initial visit.
    if (value !== undefined && (value !== "" || key === "q")) params[key] = value;
  }
  return params;
}

export const normalizeSearchQuery = (raw: RawSearchParams): SearchParams => pick(raw, searchKeys);
export const normalizeTimelineQuery = (raw: RawSearchParams): TimelineParams => pick(raw, timelineKeys);

function serialize(params: RawSearchParams, keys: readonly string[]): string {
  const query = new URLSearchParams(pick(params, keys) as Record<string, string>);
  return query.size ? `?${query}` : "";
}

export const toSearchQuery = (params: SearchParams): string => serialize(params, searchKeys);
export const toTimelineQuery = (params: TimelineParams): string => serialize(params, timelineKeys);

export function discoveryPageHref(route: DiscoveryRoute, params: SearchParams | TimelineParams, page: number): string {
  const serializeQuery = route === "/search" ? toSearchQuery : toTimelineQuery;
  return `${route}${serializeQuery({ ...params, page: String(page) })}`;
}
