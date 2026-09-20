"use client";

import { useActionState } from "react";
import { removeSpecificationAction, setSpecificationAction } from "@/app/admin/devices/actions";
import type { AdminSpecification, AdminSpecificationDefinition } from "@/lib/admin-contracts";
import { AdminActionMessage } from "./admin-action-message";

function currentValue(definition: AdminSpecificationDefinition, value?: AdminSpecification) {
  if (!value) return "";
  if (definition.dataType === "text") return value.valueText ?? "";
  if (definition.dataType === "number") return value.valueNumber ?? "";
  if (definition.dataType === "boolean") return String(value.valueBoolean ?? false);
  return value.valueDate ?? "";
}

function SpecificationRow({ deviceId, definition, value }: {
  deviceId: string;
  definition: AdminSpecificationDefinition;
  value?: AdminSpecification;
}) {
  const save = setSpecificationAction.bind(null, deviceId, definition.id, definition.dataType);
  const remove = removeSpecificationAction.bind(null, deviceId, definition.id);
  const permalink = `/admin/devices/${deviceId}`;
  const [saveState, saveAction, saving] = useActionState(save, null, permalink);
  const [removeState, removeAction, removing] = useActionState(remove, null, permalink);
  const fieldName = `value${definition.dataType[0].toUpperCase()}${definition.dataType.slice(1)}`;

  return (
    <article className="admin-spec-row">
      <header>
        <div><h3>{definition.name}</h3><code>{definition.key}</code></div>
        <span>{definition.dataType}{definition.unit ? ` · ${definition.unit}` : ""}{definition.isComparable ? " · comparable" : ""}</span>
      </header>
      <form action={saveAction} className="admin-spec-form">
        <input type="hidden" name="definitionId" value={definition.id} />
        {definition.dataType === "boolean" ? (
          <select name={fieldName} defaultValue={currentValue(definition, value)} aria-label={`${definition.name} value`}>
            <option value="true">True</option><option value="false">False</option>
          </select>
        ) : (
          <input
            name={fieldName}
            type={definition.dataType === "number" ? "number" : definition.dataType === "date" ? "date" : "text"}
            step={definition.dataType === "number" ? "any" : undefined}
            defaultValue={currentValue(definition, value)}
            required
            aria-label={`${definition.name} value`}
          />
        )}
        <button className="admin-button" type="submit" disabled={saving}>{saving ? "Saving…" : "Save value"}</button>
        <AdminActionMessage state={saveState} />
      </form>
      {value && (
        <form action={removeAction} className="admin-remove-form">
          <input type="hidden" name="removeDefinitionId" value={definition.id} />
          <label className="admin-check"><input type="checkbox" name="confirmRemove" value="yes" /> Mark as unknown</label>
          <button type="submit" disabled={removing}>Remove value</button>
          <AdminActionMessage state={removeState} />
        </form>
      )}
    </article>
  );
}

export function SpecificationEditor({ deviceId, definitions, values, readOnly }: {
  deviceId: string;
  definitions: AdminSpecificationDefinition[];
  values: AdminSpecification[];
  readOnly: boolean;
}) {
  if (readOnly) {
    return (
      <div className="admin-spec-readonly">
        {values.length === 0 ? <p>No stored specification values.</p> : values.map((value) => (
          <div key={value.definitionId}><strong>{value.key}</strong><span>{String(value.valueText ?? value.valueNumber ?? value.valueBoolean ?? value.valueDate)}</span></div>
        ))}
      </div>
    );
  }
  return (
    <div className="admin-spec-list">
      {definitions.map((definition) => <SpecificationRow key={definition.id} deviceId={deviceId} definition={definition} value={values.find((item) => item.definitionId === definition.id)} />)}
    </div>
  );
}
