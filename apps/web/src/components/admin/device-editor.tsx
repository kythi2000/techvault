"use client";

import { useActionState } from "react";
import { saveDeviceAction } from "@/app/admin/devices/actions";
import type {
  AdminBrand,
  AdminCategory,
  AdminComparisonGroup,
  AdminDeviceDetail,
} from "@/lib/admin-contracts";
import { AdminActionMessage } from "./admin-action-message";

type Props = {
  device?: AdminDeviceDetail;
  brands: AdminBrand[];
  categories: AdminCategory[];
  comparisonGroups: AdminComparisonGroup[];
};

export function DeviceEditor({ device, brands, categories, comparisonGroups }: Props) {
  const readOnly = device?.status === "archived";
  const action = saveDeviceAction.bind(null, device?.id ?? null);
  const permalink = device ? `/admin/devices/${device.id}` : "/admin/devices/new";
  const [state, formAction, pending] = useActionState(action, null, permalink);
  const value = device?.content;
  const submitted = state && !state.ok ? state.values : undefined;
  const field = (name: string, fallback: string | number | null | undefined = "") => submitted
    ? submitted[name] ?? ""
    : fallback ?? "";
  const formKey = submitted ? JSON.stringify(submitted) : "initial";

  return (
    <form action={formAction} className="admin-editor-form" key={formKey}>
      <input type="hidden" name="formKind" value="device-editor" />
      <fieldset disabled={readOnly || pending}>
        <legend>Identity and classification</legend>
        <div className="admin-form-grid">
          <label>Name<input name="name" required maxLength={200} defaultValue={field("name", value?.name)} /></label>
          <label>Slug<input name="slug" required maxLength={160} pattern="[a-z0-9]+(?:-[a-z0-9]+)*" defaultValue={field("slug", value?.slug)} /></label>
          <label>Brand<select name="brandId" required defaultValue={field("brandId", value?.brandId)}><option value="" disabled>Select a brand</option>{brands.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
          <label>Category<select name="categoryId" required defaultValue={field("categoryId", value?.categoryId)}><option value="" disabled>Select a category</option>{categories.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
          <label>Comparison group<select name="comparisonGroupId" defaultValue={field("comparisonGroupId", value?.comparisonGroupId)}><option value="">Not comparable</option>{comparisonGroups.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
          <label>Model number<input name="modelNumber" maxLength={100} defaultValue={field("modelNumber", value?.modelNumber)} /></label>
        </div>
      </fieldset>

      <fieldset disabled={readOnly || pending}>
        <legend>Editorial content</legend>
        <label>Short description<textarea name="shortDescription" maxLength={500} rows={3} defaultValue={field("shortDescription", value?.shortDescription)} /></label>
        <label>Description<textarea name="description" maxLength={100000} rows={9} defaultValue={field("description", value?.description)} /></label>
        <label>History<textarea name="history" maxLength={100000} rows={9} defaultValue={field("history", value?.history)} /></label>
        <div className="admin-form-grid">
          <label>SEO title<input name="seoTitle" maxLength={200} defaultValue={field("seoTitle", value?.seoTitle)} /></label>
          <label>SEO description<textarea name="seoDescription" maxLength={500} rows={3} defaultValue={field("seoDescription", value?.seoDescription)} /></label>
        </div>
        <label>Aliases <small>One alias per line, maximum 20.</small><textarea name="aliases" rows={5} defaultValue={field("aliases", value?.aliases.join("\n"))} /></label>
      </fieldset>

      <fieldset disabled={readOnly || pending}>
        <legend>Dates and physical record</legend>
        <div className="admin-form-grid admin-form-grid-four">
          <label>Release year<input name="releaseYear" type="number" min={1} max={9999} step={1} defaultValue={field("releaseYear", value?.releaseYear)} /></label>
          <label>Release date<input name="releaseDate" type="date" defaultValue={field("releaseDate", value?.releaseDate)} /></label>
          <label>Discontinued date<input name="discontinuedDate" type="date" defaultValue={field("discontinuedDate", value?.discontinuedDate)} /></label>
          <span />
          <label>Height (mm)<input name="heightMm" type="number" min="0.000001" step="any" defaultValue={field("heightMm", value?.heightMm)} /></label>
          <label>Width (mm)<input name="widthMm" type="number" min="0.000001" step="any" defaultValue={field("widthMm", value?.widthMm)} /></label>
          <label>Depth (mm)<input name="depthMm" type="number" min="0.000001" step="any" defaultValue={field("depthMm", value?.depthMm)} /></label>
          <label>Weight (g)<input name="weightGrams" type="number" min="0.000001" step="any" defaultValue={field("weightGrams", value?.weightGrams)} /></label>
        </div>
      </fieldset>

      {!readOnly && <AdminActionMessage state={state} />}
      {!readOnly && (
        <div className="admin-form-actions">
          <button className="admin-button admin-button-primary" type="submit" disabled={pending}>{pending ? "Saving…" : device ? "Save device" : "Create draft"}</button>
          <span>Saving replaces all content fields. Specifications and status are managed separately.</span>
        </div>
      )}
    </form>
  );
}
