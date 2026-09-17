interface DeviceObjectProps {
  name: string;
  releaseYear: number | null;
  categorySlug: string;
  parentSlug: string | null;
  large?: boolean;
}

const computerCategories = new Set([
  "computers",
  "desktop-computers",
  "laptops",
  "all-in-one-computers",
  "workstations",
]);

export function DeviceObject({
  name,
  releaseYear,
  categorySlug,
  parentSlug,
  large = false,
}: DeviceObjectProps) {
  const isComputer =
    parentSlug === "computers" || computerCategories.has(categorySlug);
  const initials = name
    .split(/\s+/)
    .map((part) => part[0])
    .join("")
    .slice(0, 3)
    .toUpperCase();

  return (
    <div className={`device-object ${large ? "device-object-large" : ""}`} aria-hidden="true">
      <span className="object-index">{releaseYear ?? "YEAR —"}</span>
      {isComputer ? (
        <div className="computer-silhouette">
          <div className="computer-screen">
            <span>{initials}</span>
          </div>
          <div className="computer-neck" />
          <div className="computer-base" />
        </div>
      ) : (
        <div className="phone-silhouette">
          <div className="phone-speaker" />
          <div className="phone-screen">
            <span>{initials}</span>
          </div>
          <div className="phone-control" />
          <div className="phone-keypad">
            {Array.from({ length: 12 }, (_, index) => (
              <i key={index} />
            ))}
          </div>
        </div>
      )}
      <span className="object-caption">ILLUSTRATIVE PLACEHOLDER</span>
    </div>
  );
}
