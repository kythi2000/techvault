import { redirect } from "next/navigation";
import { AdminLoginForm } from "@/components/admin/admin-login-form";
import { readAdminSession } from "@/lib/admin-session";

export default async function AdminLoginPage({ searchParams }: PageProps<"/admin/login">) {
  const query = await searchParams;
  const reauthenticate = query.reauth === "1";
  if (!reauthenticate && await readAdminSession()) redirect("/admin");

  return (
    <main className="admin-login-screen">
      <section className="admin-login-card" aria-labelledby="admin-login-title">
        <div className="admin-wordmark"><span>TV</span> TechVault editorial</div>
        <p className="admin-kicker">Private content workspace</p>
        <h1 id="admin-login-title">Unlock the editorial workspace</h1>
        <p className="admin-lede">
          Enter the protected catalog API key. It is verified server-side and retained only in an encrypted,
          expiring HttpOnly session.
        </p>
        {reauthenticate && <div className="admin-banner admin-banner-error" role="alert"><strong>SESSION_REJECTED</strong><span>The API no longer accepts this session. Enter the current admin API key.</span></div>}
        <AdminLoginForm />
        <p className="admin-security-note">Session expires after 8 hours. The key is never stored in browser JavaScript.</p>
      </section>
    </main>
  );
}
