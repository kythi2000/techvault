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

  return (
    <form action={formAction} className="admin-editor-form">
      <input type="hidden" name="formKind" value="device-editor" />
      <fieldset disabled={readOnly || pending}>
        <legend>Identity and classification</legend>
        <div className="admin-form-grid">
          <label>Name<input name="name" required maxLength={200} defaultValue={value?.name} /></label>
          <label>Slug<input name="slug" required maxLength={160} pattern="[a-z0-9]+(?:-[a-z0-9]+)*" defaultValue={value?.slug} /></label>
          <label>Brand<select name="brandId" required defaultValue={value?.brandId ?? ""}><option value="" disabled>Select a brand</option>{brands.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
          <label>Category<select name="categoryId" required defaultValue={value?.categoryId ?? ""}><option value="" disabled>Select a category</option>{categories.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
          <label>Comparison group<select name="comparisonGroupId" defaultValue={value?.comparisonGroupId ?? ""}><option value="">Not comparable</option>{comparisonGroups.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
          <label>Model number<input name="modelNumber" maxLength={100} defaultValue={value?.modelNumber ?? ""} /></label>
        </div>
      </fieldset>

      <fieldset disabled={readOnly || pending}>
        <legend>Editorial content</legend>
        <label>Short description<textarea name="shortDescription" maxLength={500} rows={3} defaultValue={value?.shortDescription} /></label>
        <label>Description<textarea name="description" maxLength={100000} rows={9} defaultValue={value?.description} /></label>
        <label>History<textarea name="history" maxLength={100000} rows={9} defaultValue={value?.history} /></label>
        <div className="admin-form-grid">
          <label>SEO title<input name="seoTitle" maxLength={200} defaultValue={value?.seoTitle} /></label>
          <label>SEO description<textarea name="seoDescription" maxLength={500} rows={3} defaultValue={value?.seoDescription} /></label>
        </div>
        <label>Aliases <small>One alias per line, maximum 20.</small><textarea name="aliases" rows={5} defaultValue={value?.aliases.join("\n")} /></label>
      </fieldset>

      <fieldset disabled={readOnly || pending}>
        <legend>Dates and physical record</legend>
        <div className="admin-form-grid admin-form-grid-four">
          <label>Release year<input name="releaseYear" type="number" min={1} max={9999} step={1} defaultValue={value?.releaseYear ?? ""} /></label>
          <label>Release date<input name="releaseDate" type="date" defaultValue={value?.releaseDate ?? ""} /></label>
          <label>Discontinued date<input name="discontinuedDate" type="date" defaultValue={value?.discontinuedDate ?? ""} /></label>
          <span />
          <label>Height (mm)<input name="heightMm" type="number" min="0" step="any" defaultValue={value?.heightMm ?? ""} /></label>
          <label>Width (mm)<input name="widthMm" type="number" min="0" step="any" defaultValue={value?.widthMm ?? ""} /></label>
          <label>Depth (mm)<input name="depthMm" type="number" min="0" step="any" defaultValue={value?.depthMm ?? ""} /></label>
          <label>Weight (g)<input name="weightGrams" type="number" min="0" step="any" defaultValue={value?.weightGrams ?? ""} /></label>
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
