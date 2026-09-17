import Link from "next/link";
import { ArrowIcon } from "@/components/icons";

export default function NotFound() {
  return (
    <main className="shell status-page">
      <span className="status-code">404 / NOT CATALOGUED</span>
      <h1>This object is not in the vault.</h1>
      <p>It may be unpublished, archived, or the address may be incorrect.</p>
      <Link href="/devices" className="button button-dark">Return to the archive <ArrowIcon /></Link>
    </main>
  );
}

