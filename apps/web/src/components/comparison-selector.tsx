"use client";

import { useRouter } from "next/navigation";
import { type FormEvent, useState, useTransition } from "react";
import type { DeviceCard } from "@/lib/contracts";
import { toComparisonQuery } from "@/lib/comparison-query";

interface ComparisonSelectorProps {
  devices: DeviceCard[];
  selectedSlugs: string[];
  differencesOnly: boolean;
  invalid?: boolean;
}

export function ComparisonSelector({ devices, selectedSlugs, differencesOnly, invalid = false }: ComparisonSelectorProps) {
  const router = useRouter();
  const [left, setLeft] = useState(selectedSlugs[0] ?? "");
  const [right, setRight] = useState(selectedSlugs[1] ?? "");
  const [onlyDifferences, setOnlyDifferences] = useState(differencesOnly);
  const [isPending, startTransition] = useTransition();

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const devicesValue = left && right ? `${left},${right}` : "";
    startTransition(() => router.push(`/compare${toComparisonQuery({
      ...(devicesValue ? { devices: devicesValue } : {}),
      ...(onlyDifferences ? { differencesOnly: "true" } : {}),
    })}`));
  }

  return (
    <form id="comparison-selector" className="comparison-selector discovery-form" onSubmit={submit} aria-describedby="comparison-help">
      <div className="comparison-selector-grid">
        <label>
          First object
          <select value={left} onChange={(event) => setLeft(event.target.value)} aria-invalid={invalid} required>
            <option value="">Choose an object</option>
            {devices.map((device) => (
              <option key={device.id} value={device.slug}>{device.name} · {device.brand.name}</option>
            ))}
          </select>
        </label>
        <span className="comparison-versus" aria-hidden="true">VS</span>
        <label>
          Second object
          <select value={right} onChange={(event) => setRight(event.target.value)} aria-invalid={invalid} required>
            <option value="">Choose an object</option>
            {devices.map((device) => (
              <option key={device.id} value={device.slug}>{device.name} · {device.brand.name}</option>
            ))}
          </select>
        </label>
      </div>
      <div className="comparison-selector-actions">
        <label className="comparison-checkbox">
          <input type="checkbox" checked={onlyDifferences} onChange={(event) => setOnlyDifferences(event.target.checked)} />
          Show differences only
        </label>
        <button className="button button-dark" type="submit" disabled={isPending || !left || !right}>
          {isPending ? "Comparing…" : "Compare objects"}
        </button>
      </div>
      <p id="comparison-help" className="discovery-help">
        Comparison is available only when both records share an editorial compatibility group. The archive does not score or rank devices.
      </p>
    </form>
  );
}

