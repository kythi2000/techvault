import type { RawSearchParams } from "./browse-query.ts";

export const comparisonKeys = ["devices", "differencesOnly"] as const;

export type ComparisonParams = Partial<Record<typeof comparisonKeys[number], string>>;

export function normalizeComparisonQuery(raw: RawSearchParams): ComparisonParams {
  const params: ComparisonParams = {};
  for (const key of comparisonKeys) {
    const rawValue = raw[key];
    const value = Array.isArray(rawValue) ? rawValue[0] : rawValue;
    if (value !== undefined && value !== "") params[key] = value;
  }
  return params;
}

export function toComparisonQuery(params: ComparisonParams): string {
  const query = new URLSearchParams();
  for (const key of comparisonKeys) {
    const value = params[key];
    if (value !== undefined && value !== "") query.set(key, value);
  }
  const serialized = query.toString();
  return serialized ? `?${serialized}` : "";
}

