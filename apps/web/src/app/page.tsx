import type { Metadata } from "next";
import Link from "next/link";
import { ArrowIcon } from "@/components/icons";
import { DeviceCard } from "@/components/device-card";
import { DeviceObject } from "@/components/device-object";
import { RetryButton } from "@/components/retry-button";
import { SearchForm } from "@/components/search-form";
import { browseDevices } from "@/lib/techvault-api";

export const dynamic = "force-dynamic";

export const metadata: Metadata = {
  title: "Explore the evolution of technology",
  description: "Discover landmark phones and computers through their specifications, design and history.",
  alternates: { canonical: "/" },
};

export default async function Home() {
  const featuredResult = await browseDevices("/devices", {
    sort: "release-asc",
    pageSize: "6",
  });
  const devices = featuredResult.ok ? featuredResult.data.data : [];
  const leadDevice = devices[0];
  const years = [...new Set(devices.flatMap((device) => device.releaseYear === null ? [] : [device.releaseYear]))].slice(0, 4);

  return (
    <main>
      <section className="home-hero shell">
        <div className="hero-copy">
          <span className="eyebrow">A living archive of machines</span>
          <h1>Technology has a <em>memory.</em></h1>
          <p>
            Explore the phones that connected the world and the computers that changed how we work, create and think.
          </p>
          <div className="hero-actions">
            <Link href="/phones" className="button button-dark">Explore phones <ArrowIcon /></Link>
            <Link href="/computers" className="button button-light">Explore computers <ArrowIcon /></Link>
          </div>
          <div className="hero-footnote">
            <span>01</span>
            <p>Objects are documented from public historical sources and structured technical records.</p>
          </div>
        </div>

        <div className="hero-object-wrap">
          <span className="vertical-note">OBJECT STUDY / ARCHIVE 001</span>
          {leadDevice ? (
            <Link href={`/devices/${leadDevice.slug}`} aria-label={`Open ${leadDevice.name}`}>
              <DeviceObject
                name={leadDevice.name}
                releaseYear={leadDevice.releaseYear}
                categorySlug={leadDevice.category.slug}
                parentSlug={leadDevice.category.parentSlug}
                large
              />
            </Link>
          ) : (
            <DeviceObject
              name="TechVault"
              releaseYear={null}
              categorySlug="feature-phones"
              parentSlug="phones"
              large
            />
          )}
          <div className="object-annotation annotation-one">A<br /><span>Form</span></div>
          <div className="object-annotation annotation-two">B<br /><span>Interface</span></div>
        </div>
      </section>

      <section className="manifesto-band">
        <div className="shell">
          <span>01 — Our archive</span>
          <p>Not just specifications. <em>Context, design, and the stories behind the objects.</em></p>
        </div>
      </section>

      <section className="featured-section shell">
        <div className="home-search"><SearchForm id="home-search-query" /></div>
        <div className="section-heading">
          <div>
            <span className="eyebrow">From the archive</span>
            <h2>Inside the vault</h2>
          </div>
          <Link href="/devices" className="text-link">View full archive <ArrowIcon /></Link>
        </div>

        {devices.length > 0 ? (
          <div className="device-grid featured-grid">
            {devices.slice(0, 3).map((device) => <DeviceCard device={device} key={device.id} />)}
          </div>
        ) : (
          <div className="archive-offline-note">
            <span>{featuredResult.ok ? "COLLECTION IN PROGRESS" : "COLLECTION UNAVAILABLE"}</span>
            <p>{featuredResult.ok ? "No objects have been published yet. Check back as the archive grows." : "The collection cannot be loaded right now. Please try again shortly."}</p>
            {featuredResult.ok ? <Link href="/devices" className="text-link">Browse archive <ArrowIcon /></Link> : <RetryButton />}
          </div>
        )}
      </section>

      {years.length > 0 && <section className="era-section">
        <div className="shell">
          <div className="section-heading era-title">
            <div>
              <span className="eyebrow">Browse by release year</span>
              <h2>Explore these years</h2>
            </div>
            <p>Follow a year represented in this selection to discover more objects from the same time.</p>
          </div>
          <ol className="era-line">
            {years.map((year, index) => (
              <li key={year}>
                <span className="era-index">0{index + 1}</span>
                <Link href={`/timeline?year=${year}`}><strong>{year}</strong><p>Explore objects released in {year} →</p></Link>
              </li>
            ))}
          </ol>
          <Link href="/timeline" className="text-link era-timeline-link">Explore the full timeline <ArrowIcon /></Link>
        </div>
      </section>}

      <section className="category-portals shell">
        <Link href="/phones" className="category-portal portal-phones">
          <span>Collection 01</span>
          <h2>Phones</h2>
          <p>From durable feature phones to the computers in our pockets.</p>
          <span className="portal-link">Enter collection <ArrowIcon /></span>
        </Link>
        <Link href="/computers" className="category-portal portal-computers">
          <span>Collection 02</span>
          <h2>Computers</h2>
          <p>The machines that transformed desks, studios and homes.</p>
          <span className="portal-link">Enter collection <ArrowIcon /></span>
        </Link>
      </section>
    </main>
  );
}
