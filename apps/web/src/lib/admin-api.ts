import "server-only";
import type { z } from "zod";
import { apiErrorSchema, type ApiError, type Pagination } from "./contracts.ts";
import {
  adminApiResponseSchema,
  adminBrandSchema,
  adminCategorySchema,
  adminComparisonGroupSchema,
  adminDeletedSchema,
  adminDeviceDetailSchema,
  adminDeviceStateSchema,
  adminDeviceSummarySchema,
  adminPagedResponseSchema,
  adminSpecificationDefinitionSchema,
  adminSpecificationGroupSchema,
  type AdminComparisonGroup,
  type AdminDeleted,
  type AdminDeviceDetail,
  type AdminDeviceInput,
  type AdminDeviceState,
  type AdminDeviceSummary,
  type AdminReference,
  type AdminReferenceInput,
} from "./admin-contracts.ts";

const apiBaseUrl = (process.env.TECHVAULT_API_URL ?? "http://localhost:5078").replace(/\/$/, "");

export type AdminResult<T> =
  | { ok: true; data: T; traceId: string | null }
  | { ok: false; status: number; error: ApiError; retryAfterSeconds?: number };

export interface AdminPaginatedData<T> {
  data: T[];
  pagination: Pagination;
}

export type AdminReferenceKind =
  | "brands"
  | "categories"
  | "specification-groups"
  | "specification-definitions";

type RequestOptions = {
  method?: "GET" | "POST" | "PUT" | "DELETE";
  body?: unknown;
};

const unavailable = (message: string, traceId = "frontend"): ApiError => ({
  code: "ADMIN_UNAVAILABLE",
  message,
  traceId,
});

function parseRetryAfter(response: Response): number | undefined {
  const value = response.headers.get("retry-after");
  if (value === null) return undefined;
  if (/^\d{1,9}$/.test(value)) {
    const seconds = Number(value);
    return Number.isSafeInteger(seconds) ? seconds : undefined;
  }
  const date = Date.parse(value);
  if (!Number.isFinite(date)) return undefined;
  return Math.max(0, Math.ceil((date - Date.now()) / 1000));
}

async function adminRequest<T>(
  apiKey: string,
  path: string,
  schema: z.ZodType<T>,
  options: RequestOptions = {},
): Promise<AdminResult<T>> {
  let response: Response;
  const headers = new Headers({ Accept: "application/json", Authorization: `Bearer ${apiKey}` });
  if (options.body !== undefined) headers.set("Content-Type", "application/json");

  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      method: options.method ?? "GET",
      headers,
      cache: "no-store",
      signal: AbortSignal.timeout(8000),
      ...(options.body === undefined ? {} : { body: JSON.stringify(options.body) }),
    });
  } catch {
    return { ok: false, status: 503, error: unavailable("The admin service is temporarily unavailable.") };
  }

  const traceId = response.headers.get("x-trace-id");
  const body: unknown = await response.json().catch(() => null);
  if (!response.ok) {
    const parsedError = apiErrorSchema.safeParse(body);
    const retryAfterSeconds = parseRetryAfter(response);
    return {
      ok: false,
      status: response.status,
      ...(retryAfterSeconds === undefined ? {} : { retryAfterSeconds }),
      error: parsedError.success
        ? parsedError.data.error
        : unavailable("The admin service returned an unreadable error response.", traceId ?? undefined),
    };
  }

  const parsed = schema.safeParse(body);
  if (!parsed.success) {
    return {
      ok: false,
      status: 502,
      error: unavailable("The admin response does not match the content contract.", traceId ?? undefined),
    };
  }
  return { ok: true, data: parsed.data, traceId };
}

function listQuery(params: Record<string, string | undefined>, includeStatus = false): string {
  const query = new URLSearchParams();
  for (const key of includeStatus ? ["status", "page", "pageSize"] : ["page", "pageSize"]) {
    const value = params[key];
    if (value) query.set(key, value);
  }
  const value = query.toString();
  return value ? `?${value}` : "";
}

function invalidResource<T>(): AdminResult<T> {
  return {
    ok: false,
    status: 400,
    error: { code: "VALIDATION_ERROR", message: "Unsupported admin resource.", traceId: "frontend" },
  };
}

function referenceSchema(kind: string): z.ZodType<AdminReference> | null {
  switch (kind) {
    case "brands": return adminBrandSchema;
    case "categories": return adminCategorySchema;
    case "specification-groups": return adminSpecificationGroupSchema;
    case "specification-definitions": return adminSpecificationDefinitionSchema;
    default: return null;
  }
}

export async function adminListDevices(
  apiKey: string,
  params: Record<string, string | undefined> = {},
): Promise<AdminResult<AdminPaginatedData<AdminDeviceSummary>>> {
  return adminRequest(apiKey, `/api/v1/admin/devices${listQuery(params, true)}`,
    adminPagedResponseSchema(adminDeviceSummarySchema));
}

export async function adminGetDevice(apiKey: string, id: string): Promise<AdminResult<AdminDeviceDetail>> {
  const result = await adminRequest(apiKey, `/api/v1/admin/devices/${encodeURIComponent(id)}`,
    adminApiResponseSchema(adminDeviceDetailSchema));
  return result.ok ? { ok: true, data: result.data.data, traceId: result.traceId } : result;
}

export async function adminSaveDevice(
  apiKey: string,
  input: AdminDeviceInput,
  id?: string,
): Promise<AdminResult<AdminDeviceState>> {
  const path = id ? `/api/v1/admin/devices/${encodeURIComponent(id)}` : "/api/v1/admin/devices";
  const result = await adminRequest(apiKey, path, adminApiResponseSchema(adminDeviceStateSchema), {
    method: id ? "PUT" : "POST",
    body: input,
  });
  return result.ok ? { ok: true, data: result.data.data, traceId: result.traceId } : result;
}

export async function adminChangeDeviceStatus(
  apiKey: string,
  id: string,
  action: "publish" | "unpublish" | "archive",
): Promise<AdminResult<AdminDeviceState>> {
  const result = await adminRequest(apiKey,
    `/api/v1/admin/devices/${encodeURIComponent(id)}/${action}`,
    adminApiResponseSchema(adminDeviceStateSchema), { method: "POST" });
  return result.ok ? { ok: true, data: result.data.data, traceId: result.traceId } : result;
}

export async function adminSetSpecification(
  apiKey: string,
  deviceId: string,
  definitionId: string,
  input: Record<string, string | number | boolean>,
): Promise<AdminResult<AdminDeviceState>> {
  const result = await adminRequest(apiKey,
    `/api/v1/admin/devices/${encodeURIComponent(deviceId)}/specifications/${encodeURIComponent(definitionId)}`,
    adminApiResponseSchema(adminDeviceStateSchema), { method: "PUT", body: input });
  return result.ok ? { ok: true, data: result.data.data, traceId: result.traceId } : result;
}

export async function adminRemoveSpecification(
  apiKey: string,
  deviceId: string,
  definitionId: string,
): Promise<AdminResult<AdminDeviceState>> {
  const result = await adminRequest(apiKey,
    `/api/v1/admin/devices/${encodeURIComponent(deviceId)}/specifications/${encodeURIComponent(definitionId)}`,
    adminApiResponseSchema(adminDeviceStateSchema), { method: "DELETE" });
  return result.ok ? { ok: true, data: result.data.data, traceId: result.traceId } : result;
}

export async function adminListReferences(
  apiKey: string,
  kind: string,
  params: Record<string, string | undefined> = {},
): Promise<AdminResult<AdminPaginatedData<AdminReference>>> {
  const schema = referenceSchema(kind);
  if (!schema) return invalidResource();
  return adminRequest(apiKey, `/api/v1/admin/${kind}${listQuery(params)}`, adminPagedResponseSchema(schema));
}

export async function adminGetReference(
  apiKey: string,
  kind: string,
  id: string,
): Promise<AdminResult<AdminReference>> {
  const schema = referenceSchema(kind);
  if (!schema) return invalidResource();
  const result = await adminRequest(apiKey, `/api/v1/admin/${kind}/${encodeURIComponent(id)}`,
    adminApiResponseSchema(schema));
  return result.ok ? { ok: true, data: result.data.data, traceId: result.traceId } : result;
}

export async function adminSaveReference(
  apiKey: string,
  kind: string,
  input: AdminReferenceInput,
  id?: string,
): Promise<AdminResult<AdminReference>> {
  const schema = referenceSchema(kind);
  if (!schema) return invalidResource();
  const path = id ? `/api/v1/admin/${kind}/${encodeURIComponent(id)}` : `/api/v1/admin/${kind}`;
  const result = await adminRequest(apiKey, path, adminApiResponseSchema(schema), {
    method: id ? "PUT" : "POST",
    body: input,
  });
  return result.ok ? { ok: true, data: result.data.data, traceId: result.traceId } : result;
}

export async function adminDeleteReference(
  apiKey: string,
  kind: string,
  id: string,
): Promise<AdminResult<AdminDeleted>> {
  if (!referenceSchema(kind)) return invalidResource();
  const result = await adminRequest(apiKey, `/api/v1/admin/${kind}/${encodeURIComponent(id)}`,
    adminApiResponseSchema(adminDeletedSchema), { method: "DELETE" });
  return result.ok ? { ok: true, data: result.data.data, traceId: result.traceId } : result;
}

export async function adminListComparisonGroups(
  apiKey: string,
  params: Record<string, string | undefined> = {},
): Promise<AdminResult<AdminPaginatedData<AdminComparisonGroup>>> {
  return adminRequest(apiKey, `/api/v1/admin/comparison-groups${listQuery(params)}`,
    adminPagedResponseSchema(adminComparisonGroupSchema));
}

export async function adminGetAllReferences(
  apiKey: string,
  kind: AdminReferenceKind,
): Promise<AdminResult<AdminReference[]>> {
  const items: AdminReference[] = [];
  let traceId: string | null = null;
  for (let page = 1; page <= 10000; page++) {
    const result = await adminListReferences(apiKey, kind, { page: String(page), pageSize: "100" });
    if (!result.ok) return result;
    items.push(...result.data.data);
    traceId = result.traceId;
    if (page >= result.data.pagination.totalPages) return { ok: true, data: items, traceId };
  }
  return { ok: false, status: 502, error: unavailable("The admin reference index is too large to load.") };
}

export async function adminGetAllComparisonGroups(apiKey: string): Promise<AdminResult<AdminComparisonGroup[]>> {
  const items: AdminComparisonGroup[] = [];
  let traceId: string | null = null;
  for (let page = 1; page <= 10000; page++) {
    const result = await adminListComparisonGroups(apiKey, { page: String(page), pageSize: "100" });
    if (!result.ok) return result;
    items.push(...result.data.data);
    traceId = result.traceId;
    if (page >= result.data.pagination.totalPages) return { ok: true, data: items, traceId };
  }
  return { ok: false, status: 502, error: unavailable("The comparison group index is too large to load.") };
}
