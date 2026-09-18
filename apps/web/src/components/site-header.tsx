import Link from "next/link";
import { MarkIcon, MenuIcon } from "@/components/icons";

const navigation = [
  { href: "/", label: "Explore" },
  { href: "/devices", label: "Archive" },
  { href: "/phones", label: "Phones" },
  { href: "/computers", label: "Computers" },
  { href: "/timeline", label: "Timeline" },
  { href: "/brands", label: "Brands" },
  { href: "/categories", label: "Categories" },
  { href: "/search", label: "Search" },
] as const;

export function SiteHeader() {
  return (
    <header className="site-header">
      <div className="shell header-inner">
        <Link href="/" className="brand-lockup" aria-label="TechVault home">
          <MarkIcon className="brand-mark" />
          <span>
            <strong>TechVault</strong>
            <small>Digital technology archive</small>
          </span>
        </Link>

        <nav className="primary-nav" aria-label="Primary navigation">
          {navigation.map((item) => (
            <Link href={item.href} key={item.href}>
              {item.label}
            </Link>
          ))}
        </nav>

        <details className="mobile-nav">
          <summary aria-label="Open navigation">
            <MenuIcon />
          </summary>
          <nav aria-label="Mobile navigation">
            {navigation.map((item) => (
              <Link href={item.href} key={item.href}>
                {item.label}
              </Link>
            ))}
          </nav>
        </details>
      </div>
    </header>
  );
}
