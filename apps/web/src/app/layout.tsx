import type { Metadata } from "next";
import { SiteFooter } from "@/components/site-footer";
import { SiteHeader } from "@/components/site-header";
import "./globals.css";

export const metadata: Metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000"),
  title: {
    default: "TechVault — Digital Technology Archive",
    template: "%s | TechVault",
  },
  description: "Explore the phones and computers that shaped how we communicate and compute.",
  applicationName: "TechVault",
  openGraph: {
    type: "website",
    siteName: "TechVault",
    title: "TechVault — Digital Technology Archive",
    description: "A curated archive of landmark phones and computers.",
  },
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en">
      <body>
        <a className="skip-link" href="#main-content">Skip to content</a>
        <SiteHeader />
        <div id="main-content">{children}</div>
        <SiteFooter />
      </body>
    </html>
  );
}

