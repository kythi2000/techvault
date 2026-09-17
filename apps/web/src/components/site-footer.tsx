import Link from "next/link";
import { MarkIcon } from "@/components/icons";

export function SiteFooter() {
  return (
    <footer className="site-footer">
      <div className="shell footer-grid">
        <div className="footer-signature">
          <MarkIcon className="brand-mark" />
          <div>
            <strong>TechVault</strong>
            <p>Documenting the machines that shaped modern life.</p>
          </div>
        </div>

        <div className="footer-links">
          <div>
            <span>Browse</span>
            <Link href="/devices">Full archive</Link>
            <Link href="/phones">Phones</Link>
            <Link href="/computers">Computers</Link>
            <Link href="/brands">Brands</Link>
            <Link href="/categories">Categories</Link>
          </div>
          <div>
            <span>Project</span>
            <span className="muted-link">Timeline · planned</span>
            <span className="muted-link">Compare · planned</span>
            <span className="muted-link">Museum · planned</span>
          </div>
        </div>
      </div>
      <div className="shell footer-meta">
        <span>Open archive · Public preview</span>
        <span>© {new Date().getUTCFullYear()} TechVault</span>
      </div>
    </footer>
  );
}
