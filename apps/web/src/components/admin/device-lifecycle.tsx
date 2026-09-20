"use client";

import { useActionState } from "react";
import { lifecycleAction } from "@/app/admin/devices/actions";
import type { AdminStatus } from "@/lib/admin-contracts";
import { AdminActionMessage } from "./admin-action-message";

export function DeviceLifecycle({ deviceId, status }: { deviceId: string; status: AdminStatus }) {
  const [state, formAction, pending] = useActionState(lifecycleAction.bind(null, deviceId), null, `/admin/devices/${deviceId}`);
  if (status === "archived") return <p className="admin-readonly-note">Archived records cannot be restored or edited through the current API.</p>;
  return (
    <form action={formAction} className="admin-lifecycle-form">
      <input type="hidden" name="formKind" value="lifecycle" />
      <div>
        {status === "draft"
          ? <button className="admin-button admin-button-primary" name="intent" value="publish" disabled={pending}>Publish</button>
          : <button className="admin-button" name="intent" value="unpublish" disabled={pending}>Return to draft</button>}
        <button className="admin-button admin-button-danger" name="intent" value="archive" disabled={pending}>Archive</button>
      </div>
      <label className="admin-check"><input type="checkbox" name="confirmArchive" value="yes" /> I understand archive has no restore route.</label>
      <AdminActionMessage state={state} />
    </form>
  );
}
