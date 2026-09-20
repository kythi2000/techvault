import "server-only";
import { cache } from "react";
import type { z } from "zod";
import {
  apiErrorSchema,
  apiResponseSchema,
  brandSchema,
  categorySchema,
  comparisonResponseSchema,
  deviceCardSchema,
  deviceDetailSchema,
  paginatedResponseSchema,
  timelineDeviceSchema,
  type ApiError,
  type Brand,
  type Category,
  type ComparisonResponse,
  type DeviceCard,
  type Pagination,
  type TimelineDevice,
} from "./contracts.ts";
import { toQuery, type BrowseParams, type BrowseRoute } from "./browse-query.ts";
import { toSearchQuery, toTimelineQuery, type SearchParams, type TimelineParams } from "./discovery-query.ts";
import { toComparisonQuery, type ComparisonParams } from "./comparison-query.ts";

const apiBaseUrl = (
  process.env.TECHVAULT_API_URL ?? "http://localhost:5078"
).replace(/\/$/, "");

export type ApiResult<T> =
  | { ok: true; data: T; traceId: string | null }
  | { ok: false; status: number; error: ApiError; retryAfterSeconds?: number };

export interface PaginatedData<T> {
  data: T[];
  pagination: Pagination;
}

const unavailableError = (message: string, traceId = "frontend"): ApiError => ({
  code: "CATALOG_UNAVAILABLE",
  message,
  traceId,
});

function retryAfterSeconds(response: Response): number | undefined {
  const value = response.headers.get("retry-after");
  if (value === null) return undefined;
  if (/^\d{1,9}$/.test(value)) {
    const seconds = Number(value);
    return Number.isSafeInteger(seconds) ? seconds : undefined;
  }
  if (!/^(?:Mon|Tue|Wed|Thu|Fri|Sat|Sun), \d{2} (?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) \d{4} \d{2}:\d{2}:\d{2} GMT$/.test(value)) return undefined;
  const date = Date.parse(value);
  if (!Number.isFinite(date) || new Date(date).toUTCString() !== value) return undefined;
  const seconds = Math.max(0, Math.ceil((date - Date.now()) / 1000));
  return Number.isSafeInteger(seconds) ? seconds : undefined;
}

async function request<T>(path: string, schema: z.ZodType<T>): Promise<ApiResult<T>> {
  let response: Response;

  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      headers: { Accept: "application/json" },
      cache: "no-store",
      signal: AbortSignal.timeout(8000),
    });
  } catch {
    return {
      ok: false,
      status: 503,
      error: unavailableError("The archive service is temporarily unavailable."),
    };
  }

  const traceId = response.headers.get("x-trace-id");
  const body: unknown = await response.json().catch(() => null);

  if (!response.ok) {
    const parsedError = apiErrorSchema.safeParse(body);
    return {
      ok: false,
      status: response.status,
      ...(retryAfterSeconds(response) === undefined ? {} : { retryAfterSeconds: retryAfterSeconds(response) }),
      error: parsedError.success
        ? parsedError.data.error
        : unavailableError("The archive returned an unreadable error response.", traceId ?? undefined),
    };
  }

  const parsed = schema.safeParse(body);
  if (!parsed.success) {
    return {
      ok: false,
      status: 502,
      error: unavailableError("The archive response does not match the public contract.", traceId ?? undefined),
    };
  }

  return { ok: true, data: parsed.data, traceId };
}

export function browseDevices(
  route: BrowseRoute,
  params: BrowseParams = {},
): Promise<ApiResult<PaginatedData<DeviceCard>>> {
  return request(
    `/api/v1${route}${toQuery(params)}`,
    paginatedResponseSchema(deviceCardSchema),
  );
}

export function searchDevices(params: SearchParams): Promise<ApiResult<PaginatedData<DeviceCard>>> {
  return request(`/api/v1/search${toSearchQuery(params)}`, paginatedResponseSchema(deviceCardSchema));
}

export function getTimeline(params: TimelineParams = {}): Promise<ApiResult<PaginatedData<TimelineDevice>>> {
  return request(`/api/v1/timeline${toTimelineQuery(params)}`, paginatedResponseSchema(timelineDeviceSchema));
}

export async function compareDevices(params: ComparisonParams): Promise<ApiResult<ComparisonResponse>> {
  const result = await request(
    `/api/v1/compare${toComparisonQuery(params)}`,
    apiResponseSchema(comparisonResponseSchema),
  );
  return result.ok
    ? { ok: true, data: result.data.data, traceId: result.traceId }
    : result;
}

export async function getAllDevices(pageSize = 100): Promise<ApiResult<DeviceCard[]>> {
  const items: DeviceCard[] = [];
  for (let page = 1; page <= 10000; page++) {
    const result = await browseDevices("/devices", { page: String(page), pageSize: String(pageSize) });
    if (!result.ok) return result;
    items.push(...result.data.data);
    if (page >= result.data.pagination.totalPages) {
      return { ok: true, data: items, traceId: result.traceId };
    }
  }
  return { ok: false, status: 502, error: unavailableError("The device index is too large to load.") };
}

export const getDevice = cache((slug: string) =>
  request(`/api/v1/devices/${encodeURIComponent(slug)}`, apiResponseSchema(deviceDetailSchema)),
);

export const getBrand = cache((slug: string) =>
  request(`/api/v1/brands/${encodeURIComponent(slug)}`, apiResponseSchema(brandSchema)),
);

async function getReferences<T>(path: string, itemSchema: z.ZodType<T>): Promise<ApiResult<T[]>> {
  const items: T[] = [];
  const schema = paginatedResponseSchema(itemSchema);
  // Read every page: parents and selected references may be beyond the first 100 items.
  for (let page = 1; page <= 10000; page++) {
    const result = await request(`${path}?page=${page}&pageSize=100`, schema);
    if (!result.ok) return result;
    items.push(...result.data.data);
    if (page >= result.data.pagination.totalPages) {
      return { ok: true, data: items, traceId: result.traceId };
    }
  }
  return { ok: false, status: 502, error: unavailableError("The reference index is too large to load.") };
}

export function getBrands(): Promise<ApiResult<Brand[]>> {
  return getReferences("/api/v1/brands", brandSchema);
}

export function getCategories(): Promise<ApiResult<Category[]>> {
  return getReferences("/api/v1/categories", categorySchema);
}

export const getCategory = cache(async (slug: string): Promise<ApiResult<Category>> => {
  const result = await getCategories();
  if (!result.ok) return result;
  const category = result.data.find((item) => item.slug === slug);
  return category
    ? { ok: true, data: category, traceId: result.traceId }
    : {
        ok: false,
        status: 404,
        error: { code: "CATEGORY_NOT_FOUND", message: "Category was not found.", traceId: result.traceId ?? "frontend" },
      };
});
