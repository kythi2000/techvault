import type { AdminReferenceKind } from "./admin-api.ts";

export type AdminReferenceField = {
  name: "name" | "slug" | "description" | "displayOrder" | "parentCategoryId" | "key" | "groupId" | "dataType" | "unit" | "isComparable";
  label: string;
  control: "text" | "textarea" | "number" | "checkbox" | "category" | "group" | "dataType";
  required?: boolean;
  immutable?: boolean;
};

export type AdminReferenceConfig = {
  singular: string;
  plural: string;
  description: string;
  fields: readonly AdminReferenceField[];
};

export const adminReferenceConfigs: Record<AdminReferenceKind, AdminReferenceConfig> = {
  brands: {
    singular: "brand",
    plural: "Brands",
    description: "Makers attached to device records and public search data.",
    fields: [
      { name: "name", label: "Name", control: "text", required: true },
      { name: "slug", label: "Slug", control: "text", required: true, immutable: true },
      { name: "description", label: "Description", control: "textarea" },
    ],
  },
  categories: {
    singular: "category",
    plural: "Categories",
    description: "Hierarchical catalog families and their editorial display order.",
    fields: [
      { name: "name", label: "Name", control: "text", required: true },
      { name: "slug", label: "Slug", control: "text", required: true, immutable: true },
      { name: "parentCategoryId", label: "Parent category", control: "category", immutable: true },
      { name: "displayOrder", label: "Display order", control: "number", required: true },
      { name: "description", label: "Description", control: "textarea" },
    ],
  },
  "specification-groups": {
    singular: "specification group",
    plural: "Specification groups",
    description: "Sections used to order and present typed technical fields.",
    fields: [
      { name: "name", label: "Name", control: "text", required: true },
      { name: "key", label: "Key", control: "text", required: true, immutable: true },
      { name: "displayOrder", label: "Display order", control: "number", required: true },
    ],
  },
  "specification-definitions": {
    singular: "specification definition",
    plural: "Specification definitions",
    description: "Typed fields available to device records and structured comparisons.",
    fields: [
      { name: "name", label: "Name", control: "text", required: true },
      { name: "key", label: "Key", control: "text", required: true, immutable: true },
      { name: "groupId", label: "Specification group", control: "group", required: true },
      { name: "dataType", label: "Data type", control: "dataType", required: true, immutable: true },
      { name: "unit", label: "Unit", control: "text", immutable: true },
      { name: "displayOrder", label: "Display order", control: "number", required: true },
      { name: "isComparable", label: "Comparable", control: "checkbox" },
    ],
  },
};

export function isAdminReferenceKind(value: string): value is AdminReferenceKind {
  return Object.hasOwn(adminReferenceConfigs, value);
}
