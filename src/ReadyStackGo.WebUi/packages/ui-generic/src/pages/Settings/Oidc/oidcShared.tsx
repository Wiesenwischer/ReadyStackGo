import { Fragment, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { Alert } from "../../../components/ui/Alert";

// Shared parts of the single sign-on settings pages.

/** Breadcrumb "Settings / …" like Settings › Appearance; every entry but the last links. */
export function SettingsBreadcrumb({ trail }: { trail: (string | { label: string; to: string })[] }) {
  return (
    <nav aria-label="Breadcrumb" className="mb-4 flex flex-wrap items-center gap-1.5 text-[13px]">
      <Link to="/settings" className="font-medium text-fg-brand hover:underline">
        Settings
      </Link>
      {trail.map((entry, i) => (
        <Fragment key={typeof entry === "string" ? entry : entry.label}>
          <span className="text-fg-muted">/</span>
          {typeof entry === "string" || i === trail.length - 1 ? (
            <span className="text-fg-muted">{typeof entry === "string" ? entry : entry.label}</span>
          ) : (
            <Link to={entry.to} className="font-medium text-fg-brand hover:underline">
              {entry.label}
            </Link>
          )}
        </Fragment>
      ))}
    </nav>
  );
}

/** Warning while no system administrator has a local password (plan E22). */
export function NoPasswordWarning({ adminCount, providerName }: { adminCount: number; providerName: string }) {
  return (
    <Alert
      tone="warning"
      testId="no-password-warning"
      title={
        adminCount > 1
          ? "No system administrator has a local password"
          : "The only system administrator has no local password"
      }
    >
      If {providerName} is unavailable, nobody can sign in. Set a local password in your{" "}
      <Link to="/profile" className="font-medium text-fg-brand hover:underline">
        profile
      </Link>
      . Emergency access from the server:{" "}
      <code className="font-mono text-[12px]">docker compose exec readystackgo rsgo admin set-password &lt;username&gt;</code>
    </Alert>
  );
}

/** Card section of the settings pages. */
export function Card({ title, description, children, testId }: { title?: ReactNode; description?: ReactNode; children: ReactNode; testId?: string }) {
  return (
    <section className="flex flex-col gap-[18px] rounded-2xl border border-line bg-surface px-6 pb-6 pt-5" data-testid={testId}>
      {title && (
        <div>
          <h3 className="text-base font-semibold text-fg">{title}</h3>
          {description && <p className="mt-1 text-[13px] text-fg-muted">{description}</p>}
        </div>
      )}
      {children}
    </section>
  );
}
