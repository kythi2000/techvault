import { z } from "zod";
import { paginationSchema } from "./contracts.ts";

const idSchema = z.string().uuid();
const dateSchema = z.iso.date().nullable();
const timestampSchema = z.string().datetime({ offset: true });
const slugSchema = z.string().max(160).regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/);
const keySchema = z.string().max(100).regex(/^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$/);

export const adminStatusSchema = z.enum(["draft", "published", "archived"]);
export const specificationDataTypeSchema = z.enum(["text", "number", "boolean", "date"]);

export const adminDeviceInputSchema = z.object({
  name: z.string(),
  slug: slugSchema,
  brandId: idSchema,
  categoryId: idSchema,
  comparisonGroupId: idSchema.nullable(),
  shortDescription: z.string(),
  description: z.string(),
  history: z.string(),
  seoTitle: z.string(),
  seoDescription: z.string(),
  modelNumber: z.string().nullable(),
  aliases: z.array(z.string()),
  releaseYear: z.number().int().min(1).max(9999).nullable(),
  releaseDate: dateSchema,
  discontinuedDate: dateSchema,
  heightMm: z.number().positive().nullable(),
  widthMm: z.number().positive().nullable(),
  depthMm: z.number().positive().nullable(),
  weightGrams: z.number().positive().nullable(),
});

export const adminDeviceSummarySchema = z.object({
  id: idSchema,
  name: z.string(),
  slug: slugSchema,
  status: adminStatusSchema,
  brandId: idSchema,
  categoryId: idSchema,
  comparisonGroupId: idSchema.nullable(),
  updatedAt: timestampSchema,
});

export const adminDeviceStateSchema = z.object({
  id: idSchema,
  slug: slugSchema,
  status: adminStatusSchema,
  updatedAt: timestampSchema,
  publishedAt: timestampSchema.nullable(),
});

export const adminSpecificationSchema = z.object({
  definitionId: idSchema,
  key: keySchema,
  dataType: specificationDataTypeSchema,
  unit: z.string().nullable(),
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
}, "An admin specification must have exactly one value matching its dataType.");

export const adminDeviceDetailSchema = z.object({
  id: idSchema,
  status: adminStatusSchema,
  content: adminDeviceInputSchema,
  createdAt: timestampSchema,
  updatedAt: timestampSchema,
  publishedAt: timestampSchema.nullable(),
  specifications: z.array(adminSpecificationSchema),
});

export const adminBrandSchema = z.object({
  id: idSchema,
  name: z.string(),
  slug: slugSchema,
  description: z.string(),
});

export const adminCategorySchema = z.object({
  id: idSchema,
  name: z.string(),
  slug: slugSchema,
  description: z.string(),
  displayOrder: z.number().int().nonnegative(),
  parentCategoryId: idSchema.nullable(),
});

export const adminSpecificationGroupSchema = z.object({
  id: idSchema,
  name: z.string(),
  key: keySchema,
  displayOrder: z.number().int().nonnegative(),
});

export const adminSpecificationDefinitionSchema = z.object({
  id: idSchema,
  name: z.string(),
  key: keySchema,
  groupId: idSchema,
  dataType: specificationDataTypeSchema,
  displayOrder: z.number().int().nonnegative(),
  unit: z.string().nullable(),
  isComparable: z.boolean(),
});

export const adminComparisonGroupSchema = z.object({
  id: idSchema,
  name: z.string(),
  key: keySchema,
});

export const adminDeletedSchema = z.object({ id: idSchema });

export const adminApiResponseSchema = <T extends z.ZodType>(data: T) => z.object({ data });
export const adminPagedResponseSchema = <T extends z.ZodType>(item: T) => z.object({
  data: z.array(item),
  pagination: paginationSchema,
});

export type AdminStatus = z.infer<typeof adminStatusSchema>;
export type SpecificationDataType = z.infer<typeof specificationDataTypeSchema>;
export type AdminDeviceInput = z.infer<typeof adminDeviceInputSchema>;
export type AdminDeviceSummary = z.infer<typeof adminDeviceSummarySchema>;
export type AdminDeviceState = z.infer<typeof adminDeviceStateSchema>;
export type AdminSpecification = z.infer<typeof adminSpecificationSchema>;
export type AdminDeviceDetail = z.infer<typeof adminDeviceDetailSchema>;
export type AdminBrand = z.infer<typeof adminBrandSchema>;
export type AdminCategory = z.infer<typeof adminCategorySchema>;
export type AdminSpecificationGroup = z.infer<typeof adminSpecificationGroupSchema>;
export type AdminSpecificationDefinition = z.infer<typeof adminSpecificationDefinitionSchema>;
export type AdminComparisonGroup = z.infer<typeof adminComparisonGroupSchema>;
export type AdminDeleted = z.infer<typeof adminDeletedSchema>;

export type AdminReference =
  | AdminBrand
  | AdminCategory
  | AdminSpecificationGroup
  | AdminSpecificationDefinition;

export type AdminReferenceInput =
  | Omit<AdminBrand, "id">
  | Omit<AdminCategory, "id">
  | Omit<AdminSpecificationGroup, "id">
  | Omit<AdminSpecificationDefinition, "id">;
