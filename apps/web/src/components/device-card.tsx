import Link from "next/link";
import { ArrowIcon } from "@/components/icons";
import { DeviceObject } from "@/components/device-object";
import type { DeviceCard as DeviceCardModel } from "@/lib/contracts";
import { archiveNumber } from "@/lib/format";

export function DeviceCard({ device }: { device: DeviceCardModel }) {
  return (
    <article className="catalog-card">
      <Link href={`/devices/${device.slug}`} className="card-visual" aria-label={`Explore ${device.name}`}>
        <DeviceObject
          name={device.name}
          releaseYear={device.releaseYear}
          categorySlug={device.category.slug}
          parentSlug={device.category.parentSlug}
        />
      </Link>
      <div className="card-meta-row">
        <Link href={`/brands/${device.brand.slug}`}>{device.brand.name}</Link>
        <span>TV–{archiveNumber(device.id)}</span>
      </div>
      <h3>
        <Link href={`/devices/${device.slug}`}>{device.name}</Link>
      </h3>
      <p>{device.shortDescription}</p>
      <div className="card-footer">
        <Link className="card-taxonomy-link" href={`/categories/${device.category.slug}`}>{device.category.name}</Link>
        <Link className="card-arrow-link" href={`/devices/${device.slug}`} aria-label={`View ${device.name}`}>
          <ArrowIcon />
        </Link>
      </div>
    </article>
  );
}
