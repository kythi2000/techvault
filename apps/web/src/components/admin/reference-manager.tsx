"use client";

import Link from "next/link";
import { useActionState } from "react";
import { deleteReferenceAction, saveReferenceAction } from "@/app/admin/references/actions";
import type { AdminReferenceKind, AdminPaginatedData } from "@/lib/admin-api";
import type {
  AdminCategory,
  AdminReference,
  AdminSpecificationGroup,
} from "@/lib/admin-contracts";
import { adminReferenceConfigs, type AdminReferenceField } from "@/lib/admin-reference-config";
import { AdminActionMessage } from "./admin-action-message";

type Props = {
  kind: AdminReferenceKind;
  result: AdminPaginatedData<AdminReference>;
  categories: AdminCategory[];
  groups: AdminSpecificationGroup[];
};

function record(item?: AdminReference): Record<string, unknown> {
  return (item ?? {}) as unknown as Record<string, unknown>;
}

function value(item: AdminReference | undefined, name: string): string | number | boolean {
  const current = record(item)[name];
  return typeof current === "string" || typeof current === "number" || typeof current === "boolean" ? current : "";
}

function ReferenceControl({ field, item, categories, groups, submitted }: {
  field: AdminReferenceField;
  item?: AdminReference;
  categories: AdminCategory[];
  groups: AdminSpecificationGroup[];
  submitted?: Record<string, string>;
}) {
  const immutable = Boolean(item && field.immutable);
  const current = submitted ? submitted[field.name] ?? "" : value(item, field.name);
  const name = immutable ? undefined : field.name;
  let control;

  if (field.control === "textarea") {
    control = <textarea name={name} rows={4} required={field.required} disabled={immutable} defaultValue={String(current)} />;
  } else if (field.control === "checkbox") {
    control = <input name={name} type="checkbox" value="true" disabled={immutable} defaultChecked={current === true || current === "true" || current === "on"} />;
  } else if (field.control === "category" || field.control === "group" || field.control === "dataType") {
    const options = field.control === "category"
      ? categories.filter((category) => category.id !== record(item).id).map((category) => ({ value: category.id, label: category.name }))
      : field.control === "group"
        ? groups.map((group) => ({ value: group.id, label: group.name }))
        : ["text", "number", "boolean", "date"].map((type) => ({ value: type, label: type }));
    control = (
      <select name={name} required={field.required} disabled={immutable} defaultValue={String(current)}>
        {!field.required && <option value="">None</option>}
        {field.required && <option value="" disabled>Select {field.label.toLowerCase()}</option>}
        {options.map((option) => <option value={option.value} key={option.value}>{option.label}</option>)}
      </select>
    );
  } else {
    control = <input name={name} type={field.control === "number" ? "number" : "text"} min={field.control === "number" ? 0 : undefined} step={field.control === "number" ? 1 : undefined} required={field.required} disabled={immutable} defaultValue={String(current)} />;
  }

  return (
    <label className={field.control === "textarea" ? "admin-reference-wide" : undefined}>
      <span>{field.label}{immutable && <small>Immutable</small>}</span>
      {immutable && <input type="hidden" name={field.name} value={String(current)} />}
      {control}
    </label>
  );
}

function ReferenceForm({ kind, item, categories, groups }: {
  kind: AdminReferenceKind;
  item?: AdminReference;
  categories: AdminCategory[];
  groups: AdminSpecificationGroup[];
}) {
  const config = adminReferenceConfigs[kind];
  const action = saveReferenceAction.bind(null, kind, item?.id ?? null);
  const [state, formAction, pending] = useActionState(action, null, `/admin/references/${kind}`);
  const marker = item ? `reference-edit-${item.id}` : "reference-create";
  const submitted = state && !state.ok ? state.values : undefined;
  const formKey = submitted ? JSON.stringify(submitted) : "initial";
  return (
    <form action={formAction} className="admin-reference-form" key={formKey}>
      <input type="hidden" name="formKind" value={marker} />
      <div className="admin-reference-fields">
        {config.fields.map((field) => <ReferenceControl key={field.name} field={field} item={item} categories={categories} groups={groups} submitted={submitted} />)}
      </div>
      <AdminActionMessage state={state} />
      <button className={`admin-button ${item ? "" : "admin-button-primary"}`} type="submit" disabled={pending}>
        {pending ? "Saving…" : item ? `Update ${config.singular}` : `Create ${config.singular}`}
      </button>
    </form>
  );
}

function DeleteReferenceForm({ kind, item }: { kind: AdminReferenceKind; item: AdminReference }) {
  const action = deleteReferenceAction.bind(null, kind, item.id);
  const [state, formAction, pending] = useActionState(action, null, `/admin/references/${kind}`);
  return (
    <form action={formAction} className="admin-reference-delete">
      <input type="hidden" name="formKind" value={`reference-delete-${item.id}`} />
      <label className="admin-check"><input type="checkbox" name="confirmDelete" value="yes" /> Confirm physical deletion</label>
      <button type="submit" disabled={pending}>Delete</button>
      <AdminActionMessage state={state} />
    </form>
  );
}

export function ReferenceManager({ kind, result, categories, groups }: Props) {
  const config = adminReferenceConfigs[kind];
  const { page, totalPages } = result.pagination;
  return (
    <>
      <details className="admin-create-reference">
        <summary>Create {config.singular}</summary>
        <ReferenceForm kind={kind} categories={categories} groups={groups} />
      </details>
      <div className="admin-reference-list">
        {result.data.length === 0 ? <div className="admin-empty"><h2>No {config.plural.toLowerCase()}</h2></div> : result.data.map((item) => (
          <article className="admin-reference-card" key={item.id}>
            <header><div><h2>{String(record(item).name)}</h2><code>{String(record(item).slug ?? record(item).key)}</code></div><code>{item.id}</code></header>
            <ReferenceForm kind={kind} item={item} categories={categories} groups={groups} />
            <DeleteReferenceForm kind={kind} item={item} />
          </article>
        ))}
      </div>
      {totalPages > 1 && page <= totalPages && (
        <nav className="admin-pagination" aria-label={`${config.plural} pagination`}>
          {page > 1 ? <Link href={`/admin/references/${kind}?page=${page - 1}`}>← Previous</Link> : <span />}
          <span>Page {page} of {totalPages}</span>
          {page < totalPages ? <Link href={`/admin/references/${kind}?page=${page + 1}`}>Next →</Link> : <span />}
        </nav>
      )}
    </>
  );
}
