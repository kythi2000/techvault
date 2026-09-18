import { z } from "zod";

const idSchema = z.string().uuid();
const dateSchema = z.iso.date().nullable();
const slugSchema = z.string().max(160).regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/);
const timestampSchema = z.string().datetime({ offset: true });

export const brandReferenceSchema = z.object({
  id: idSchema,
  name: z.string(),
  slug: slugSchema,
});

export const categoryReferenceSchema = z.object({
  id: idSchema,
  name: z.string(),
  slug: slugSchema,
  parentCategoryId: idSchema.nullable(),
  parentSlug: slugSchema.nullable(),
});

export const deviceCardSchema = z.object({
  id: idSchema,
  name: z.string(),
  slug: slugSchema,
  shortDescription: z.string(),
  brand: brandReferenceSchema,
  category: categoryReferenceSchema,
  releaseYear: z.number().int().min(1).max(9999).nullable(),
  releaseDate: dateSchema,
});

// Unlike browse/search, the timeline contract guarantees a known release year.
export const timelineDeviceSchema = deviceCardSchema.extend({
  releaseYear: z.number().int().min(1).max(9999),
});

export const specificationSchema = z.object({
  id: idSchema,
  key: z.string(),
  name: z.string(),
  dataType: z.enum(["text", "number", "boolean", "date"]),
  unit: z.string().nullable(),
  displayOrder: z.number().int(),
  valueText: z.string().nullable(),
  valueNumber: z.number().nullable(),
  valueBoolean: z.boolean().nullable(),
  valueDate: dateSchema,
}).refine((specification) => {
  const values = {
    text: specification.valueText,
    number: specification.valueNumber,
    boolean: specification.valueBoolean,
    date: specification.valueDate,
  };
  return values[specification.dataType] !== null &&
    Object.values(values).filter((value) => value !== null).length === 1;
}, "A specification must have exactly one value matching its dataType.");

export const specificationGroupSchema = z.object({
  id: idSchema,
  key: z.string(),
  name: z.string(),
  displayOrder: z.number().int(),
  specifications: z.array(specificationSchema),
});

export const deviceDetailSchema = deviceCardSchema.extend({
  description: z.string(),
  history: z.string(),
  seoTitle: z.string(),
  seoDescription: z.string(),
  discontinuedDate: dateSchema,
  physicalDetails: z.object({
    heightMm: z.number().nullable(),
    widthMm: z.number().nullable(),
    depthMm: z.number().nullable(),
    weightGrams: z.number().nullable(),
  }),
  createdAt: timestampSchema,
  updatedAt: timestampSchema,
  publishedAt: timestampSchema.nullable(),
  specificationGroups: z.array(specificationGroupSchema),
});

export const brandSchema = brandReferenceSchema.extend({
  description: z.string(),
  createdAt: timestampSchema,
  updatedAt: timestampSchema,
  publishedDeviceCount: z.number().int().nonnegative(),
});

export const categorySchema = z.object({
  id: idSchema,
  name: z.string(),
  slug: slugSchema,
  description: z.string(),
  parentCategoryId: idSchema.nullable(),
  parentSlug: slugSchema.nullable(),
  displayOrder: z.number().int(),
});

export const paginationSchema = z.object({
  page: z.number().int().min(1).max(10000),
  pageSize: z.number().int().min(1).max(100),
  total: z.number().int().nonnegative(),
  totalPages: z.number().int().nonnegative(),
});

export const apiErrorSchema = z.object({
  error: z.object({
    code: z.string(),
    message: z.string(),
    traceId: z.string(),
  }),
});

export const apiResponseSchema = <T extends z.ZodType>(data: T) =>
  z.object({ data });

export const paginatedResponseSchema = <T extends z.ZodType>(item: T) =>
  z.object({
    data: z.array(item),
    pagination: paginationSchema,
  });

export type DeviceCard = z.infer<typeof deviceCardSchema>;
export type TimelineDevice = z.infer<typeof timelineDeviceSchema>;
export type DeviceDetail = z.infer<typeof deviceDetailSchema>;
export type Specification = z.infer<typeof specificationSchema>;
export type SpecificationGroup = z.infer<typeof specificationGroupSchema>;
export type Brand = z.infer<typeof brandSchema>;
export type Category = z.infer<typeof categorySchema>;
export type Pagination = z.infer<typeof paginationSchema>;
export type ApiError = z.infer<typeof apiErrorSchema>["error"];
