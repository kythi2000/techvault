import type { DeviceDetail, Specification } from "@/lib/contracts";

const dateFormatter = new Intl.DateTimeFormat("en", {
  day: "numeric",
  month: "short",
  year: "numeric",
  timeZone: "UTC",
});

const numberFormatter = new Intl.NumberFormat("en", {
  maximumFractionDigits: 20,
});

export function formatDate(value: string | null): string {
  if (!value) return "Unknown";
  return dateFormatter.format(new Date(`${value}T00:00:00Z`));
}

export function formatSpecification(specification: Specification): string {
  switch (specification.dataType) {
    case "text":
      return specification.valueText ?? "Unknown";
    case "number":
      return specification.valueNumber === null
        ? "Unknown"
        : `${numberFormatter.format(specification.valueNumber)}${
            specification.unit ? ` ${specification.unit}` : ""
          }`;
    case "boolean":
      return specification.valueBoolean === null
        ? "Unknown"
        : specification.valueBoolean
          ? "Yes"
          : "No";
    case "date":
      return formatDate(specification.valueDate);
  }
}

export function formatDimensions(device: DeviceDetail): string {
  const { heightMm, widthMm, depthMm } = device.physicalDetails;
  if (heightMm === null || widthMm === null || depthMm === null) return "Unknown";
  return `${numberFormatter.format(heightMm)} × ${numberFormatter.format(widthMm)} × ${numberFormatter.format(depthMm)} mm`;
}

export function archiveNumber(id: string): string {
  return id.replaceAll("-", "").slice(0, 6).toUpperCase();
}
