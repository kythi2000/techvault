import type { SpecificationGroup } from "@/lib/contracts";
import { formatSpecification } from "@/lib/format";

interface SpecificationGroupsProps {
  groups: SpecificationGroup[];
  limit?: number;
}

export function SpecificationGroups({ groups, limit }: SpecificationGroupsProps) {
  const visibleGroups = limit === undefined ? groups : groups.slice(0, limit);

  if (visibleGroups.length === 0) {
    return <p className="empty-copy">No specifications have been catalogued yet.</p>;
  }

  return (
    <div className="spec-groups">
      {visibleGroups.map((group, index) => (
        <section className="spec-group" key={group.id}>
          <div className="spec-group-heading">
            <span>{String(index + 1).padStart(2, "0")}</span>
            <h2>{group.name}</h2>
          </div>
          <dl>
            {group.specifications.map((specification) => (
              <div key={specification.id}>
                <dt>{specification.name}</dt>
                <dd>{formatSpecification(specification)}</dd>
              </div>
            ))}
          </dl>
        </section>
      ))}
    </div>
  );
}

