import Link from "next/link";
import type { ComparisonResponse, ComparisonSpecification, ComparisonValue } from "@/lib/contracts";
import { formatDate } from "@/lib/format";

const numberFormatter = new Intl.NumberFormat("en", { maximumFractionDigits: 20 });

function formatValue(specification: ComparisonSpecification, value: ComparisonValue): string {
  if (value.isMissing) return "Unknown";
  switch (specification.dataType) {
    case "text": return value.valueText ?? "Unknown";
    case "number": return value.valueNumber === null
      ? "Unknown"
      : `${numberFormatter.format(value.valueNumber)}${specification.unit ? ` ${specification.unit}` : ""}`;
    case "boolean": return value.valueBoolean === null ? "Unknown" : value.valueBoolean ? "Yes" : "No";
    case "date": return formatDate(value.valueDate);
  }
}

export function ComparisonTable({ comparison }: { comparison: ComparisonResponse }) {
  const [left, right] = comparison.devices;

  if (comparison.specificationGroups.length === 0) {
    return (
      <div className="empty-state comparison-empty">
        <span>NO ROWS TO DISPLAY</span>
        <h2>No differing comparable values remain.</h2>
        <p>Both object headers stay visible. Turn off “differences only” to inspect all comparable fields.</p>
      </div>
    );
  }

  return (
    <div className="comparison-table-scroll" role="region" aria-label="Device specification comparison" tabIndex={0}>
      <table className="comparison-table">
        <caption>{comparison.comparisonGroup.name} · structured specification comparison</caption>
        <thead>
          <tr>
            <th scope="col">Technical record</th>
            <th scope="col"><Link href={`/devices/${left.slug}`}>{left.name}</Link><span>{left.brand.name}</span></th>
            <th scope="col"><Link href={`/devices/${right.slug}`}>{right.name}</Link><span>{right.brand.name}</span></th>
          </tr>
        </thead>
        {comparison.specificationGroups.map((group) => (
          <tbody key={group.id}>
            <tr className="comparison-group-row"><th colSpan={3} scope="colgroup">{group.name}</th></tr>
            {group.specifications.map((specification) => (
              <tr className={specification.isDifferent ? "is-different" : undefined} key={specification.id}>
                <th scope="row">
                  <span>{specification.name}</span>
                  {specification.isDifferent && <small>Different</small>}
                </th>
                {specification.values.map((value, index) => (
                  <td className={value.isMissing ? "is-missing" : undefined} key={`${specification.id}-${comparison.devices[index].id}`}>
                    {formatValue(specification, value)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        ))}
      </table>
    </div>
  );
}

