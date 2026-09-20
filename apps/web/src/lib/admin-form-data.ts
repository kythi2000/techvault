import type {
  AdminDeviceInput,
  AdminReferenceInput,
  SpecificationDataType,
} from "./admin-contracts.ts";

export class AdminFormError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "AdminFormError";
  }
}

const deviceFields = [
  "name", "slug", "brandId", "categoryId", "comparisonGroupId", "modelNumber", "shortDescription",
  "description", "history", "seoTitle", "seoDescription", "aliases", "releaseYear", "releaseDate",
  "discontinuedDate", "heightMm", "widthMm", "depthMm", "weightGrams",
] as const;

const referenceFields: Record<string, readonly string[]> = {
  brands: ["name", "slug", "description"],
  categories: ["name", "slug", "description", "displayOrder", "parentCategoryId"],
  "specification-groups": ["name", "key", "displayOrder"],
  "specification-definitions": ["name", "key", "groupId", "dataType", "displayOrder", "unit", "isComparable"],
};

function retainValues(formData: FormData, fields: readonly string[]): Record<string, string> {
  return Object.fromEntries(fields.map((name) => {
    const value = formData.get(name);
    return [name, typeof value === "string" ? value : ""];
  }));
}

export function retainDeviceFormValues(formData: FormData): Record<string, string> {
  return retainValues(formData, deviceFields);
}

export function retainReferenceFormValues(kind: string, formData: FormData): Record<string, string> {
  return retainValues(formData, referenceFields[kind] ?? []);
}

function text(formData: FormData, name: string): string {
  const value = formData.get(name);
  if (typeof value !== "string") throw new AdminFormError(`${name} is required.`);
  return value.trim();
}

function nullableText(formData: FormData, name: string): string | null {
  const value = text(formData, name);
  return value === "" ? null : value;
}

function numberValue(formData: FormData, name: string, integer = false): number | null {
  const raw = nullableText(formData, name);
  if (raw === null) return null;
  const value = Number(raw);
  if (!Number.isFinite(value) || (integer && !Number.isInteger(value))) {
    throw new AdminFormError(`${name} must be a valid ${integer ? "whole " : ""}number.`);
  }
  return value;
}

function requiredNumber(formData: FormData, name: string, integer = false): number {
  const value = numberValue(formData, name, integer);
  if (value === null) throw new AdminFormError(`${name} is required.`);
  return value;
}

function positiveNumberValue(formData: FormData, name: string): number | null {
  const value = numberValue(formData, name);
  if (value !== null && value <= 0) throw new AdminFormError(`${name} must be greater than zero.`);
  return value;
}

export function parseDeviceForm(formData: FormData): AdminDeviceInput {
  return {
    name: text(formData, "name"),
    slug: text(formData, "slug"),
    brandId: text(formData, "brandId"),
    categoryId: text(formData, "categoryId"),
    comparisonGroupId: nullableText(formData, "comparisonGroupId"),
    shortDescription: text(formData, "shortDescription"),
    description: text(formData, "description"),
    history: text(formData, "history"),
    seoTitle: text(formData, "seoTitle"),
    seoDescription: text(formData, "seoDescription"),
    modelNumber: nullableText(formData, "modelNumber"),
    aliases: text(formData, "aliases").split(/\r?\n/).map((value) => value.trim()).filter(Boolean),
    releaseYear: numberValue(formData, "releaseYear", true),
    releaseDate: nullableText(formData, "releaseDate"),
    discontinuedDate: nullableText(formData, "discontinuedDate"),
    heightMm: positiveNumberValue(formData, "heightMm"),
    widthMm: positiveNumberValue(formData, "widthMm"),
    depthMm: positiveNumberValue(formData, "depthMm"),
    weightGrams: positiveNumberValue(formData, "weightGrams"),
  };
}

export function parseSpecificationForm(formData: FormData): Record<string, string | number | boolean> {
  const dataType = text(formData, "dataType") as SpecificationDataType;
  switch (dataType) {
    case "text": {
      const value = text(formData, "valueText");
      if (!value) throw new AdminFormError("Specification text cannot be empty.");
      return { valueText: value };
    }
    case "number": {
      const value = numberValue(formData, "valueNumber");
      if (value === null) throw new AdminFormError("Specification must be a valid number.");
      return { valueNumber: value };
    }
    case "boolean": {
      const value = text(formData, "valueBoolean");
      if (value !== "true" && value !== "false") {
        throw new AdminFormError("Specification must be true or false.");
      }
      return { valueBoolean: value === "true" };
    }
    case "date": {
      const value = text(formData, "valueDate");
      if (!value) throw new AdminFormError("Specification date is required.");
      return { valueDate: value };
    }
    default:
      throw new AdminFormError("Unsupported specification data type.");
  }
}

export function parseReferenceForm(kind: string, formData: FormData): AdminReferenceInput {
  switch (kind) {
    case "brands":
      return {
        name: text(formData, "name"),
        slug: text(formData, "slug"),
        description: text(formData, "description"),
      };
    case "categories":
      return {
        name: text(formData, "name"),
        slug: text(formData, "slug"),
        description: text(formData, "description"),
        displayOrder: requiredNumber(formData, "displayOrder", true),
        parentCategoryId: nullableText(formData, "parentCategoryId"),
      };
    case "specification-groups":
      return {
        name: text(formData, "name"),
        key: text(formData, "key"),
        displayOrder: requiredNumber(formData, "displayOrder", true),
      };
    case "specification-definitions": {
      const dataType = text(formData, "dataType");
      if (!["text", "number", "boolean", "date"].includes(dataType)) {
        throw new AdminFormError("Unsupported specification data type.");
      }
      return {
        name: text(formData, "name"),
        key: text(formData, "key"),
        groupId: text(formData, "groupId"),
        dataType: dataType as SpecificationDataType,
        displayOrder: requiredNumber(formData, "displayOrder", true),
        unit: nullableText(formData, "unit"),
        isComparable: formData.get("isComparable") === "true" || formData.get("isComparable") === "on",
      };
    }
    default:
      throw new AdminFormError("Unsupported admin resource.");
  }
}
