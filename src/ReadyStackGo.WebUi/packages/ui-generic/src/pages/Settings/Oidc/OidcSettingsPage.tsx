import { useEffect, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { getOidcSettings, getOidcTemplates, setupApi, type OidcProviderSettingsDto, type OidcSettingsDto } from "@rsgo/core";
import { Alert } from "../../../components/ui/Alert";
import { Button, ButtonLink } from "../../../components/ui/Button";
import { StatusBadge } from "../../../components/ui/StatusBadge";
import { ProviderMark } from "../../../components/sso/SsoComponents";
import { CheckCircleIcon, XCircleIcon } from "../../../components/sso/icons";
import { SettingsBreadcrumb, NoPasswordWarning } from "./oidcShared";
import { formatDate, relativeTime } from "./oidcFormat";

// Settings › Single Sign-On (design frames 173:2891 "no providers", 173:3055 "providers").

const LOST_ERRORS: Record<string, string> = {
  registration_lost: "The connection could not be assigned to a setup in this browser. Start it again.",
  test_sign_in_lost: "The test sign-in could not be assigned to a setup in this browser. Start it again.",
};

function meta(p: OidcProviderSettingsDto): string {
  const parts = [p.name, p.templateName];
  if (p.reconnectNeeded) {
    parts.push(`${p.templateName} rejected the client (invalid_client), the connection was probably removed`);
  } else if (p.isPaired && p.pairedAt) {
    parts.push(`connected on ${formatDate(p.pairedAt)}`);
  } else if (p.authority) {
    parts.push(p.authority);
  }
  return parts.join(" · ");
}

function LastResult({ provider }: { provider: OidcProviderSettingsDto }) {
  const r = provider.lastResult;
  if (!r) return <span className="text-[13px] text-fg-muted">Not tested yet</span>;
  const what = r.kind === "signIn" ? "Sign-in" : r.kind === "checks" ? "Checks" : "Test sign-in";
  const label = r.passed
    ? `${what} passed · ${relativeTime(r.at)}`
    : `${r.kind === "signIn" ? "Sign-in failed" : r.message || `${what} failed`} · ${relativeTime(r.at)}`;
  return (
    <span className="flex items-center gap-1.5 text-[13px] text-fg-secondary" title={r.message ?? undefined}>
      {r.passed ? (
        <CheckCircleIcon size={16} className="shrink-0 text-status-healthy" />
      ) : (
        <XCircleIcon size={16} className="shrink-0 text-status-unhealthy" />
      )}
      <span className="truncate">{label}</span>
    </span>
  );
}

export default function OidcSettingsPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [data, setData] = useState<OidcSettingsDto | null>(null);
  const [error, setError] = useState<string | null>(params.get("error") ? LOST_ERRORS[params.get("error")!] ?? null : null);
  const [busy, setBusy] = useState<string | null>(null);
  const [emptyIcon, setEmptyIcon] = useState<string | null>(null);

  useEffect(() => {
    getOidcSettings()
      .then((settings) => {
        setData(settings);
        if (settings.providers.length === 0) {
          getOidcTemplates()
            .then((templates) => setEmptyIcon(templates.find((t) => t.iconUrl)?.iconUrl ?? null))
            .catch(() => undefined);
        }
      })
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load providers"));
  }, []);

  /** "Test" and "Reconnect" open the provider's page with a fresh setup session. */
  const openWith = async (provider: OidcProviderSettingsDto, action: "test" | "reconnect") => {
    setBusy(`${provider.name}:${action}`);
    try {
      const session = await setupApi.createForProvider(provider.name);
      navigate(`/settings/oidc/providers/${encodeURIComponent(provider.name)}?session=${encodeURIComponent(session.id)}&action=${action}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to open the provider");
      setBusy(null);
    }
  };

  const providers = data?.providers ?? [];

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <SettingsBreadcrumb trail={["Single Sign-On"]} />
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 className="text-[26px] font-bold leading-[34px] text-fg">Single Sign-On</h2>
          <p className="mt-1 text-sm text-fg-secondary">
            Let users sign in with an identity provider. Users can sign in only if they already have an account or a pending
            invitation.
          </p>
        </div>
        {providers.length > 0 && (
          <ButtonLink to="/settings/oidc/add" data-testid="add-provider">
            Add provider
          </ButtonLink>
        )}
      </div>

      <div className="flex flex-col gap-6">
        {error && (
          <Alert tone="error" title="Something went wrong">
            {error}
          </Alert>
        )}

        {data?.noAdminWithPassword && (
          <NoPasswordWarning
            adminCount={data.systemAdminCount}
            providerName={providers.find((p) => p.isOnlySignInOfCurrentUser)?.displayName ?? "the identity provider"}
          />
        )}

        {data && providers.length === 0 && (
          <section
            className="flex flex-col items-center gap-3 rounded-2xl border border-line bg-surface px-6 py-12 text-center"
            data-testid="sso-empty"
          >
            <div className="flex -space-x-2">
              {emptyIcon && <ProviderMark iconUrl={emptyIcon} />}
              <ProviderMark />
            </div>
            <h3 className="text-base font-semibold text-fg">No identity providers yet</h3>
            <p className="text-sm text-fg-secondary">
              Add WYSCH or any OpenID Connect provider. A guided setup connects, tests and saves it.
            </p>
            <ButtonLink to="/settings/oidc/add" data-testid="add-provider">
              Add provider
            </ButtonLink>
          </section>
        )}

        {providers.length > 0 && (
          <section className="rounded-2xl border border-line bg-surface px-6 pb-4 pt-5" aria-labelledby="sso-providers">
            <h3 id="sso-providers" className="text-base font-semibold text-fg">
              Providers
            </h3>
            <p className="mt-1 text-[13px] text-fg-muted">Users see these providers on the sign-in page.</p>
            <ul className="mt-1">
              {providers.map((p, i) => (
                <li
                  key={p.name}
                  data-testid={`provider-${p.name}`}
                  className={`flex flex-wrap items-center gap-4 py-4 ${i < providers.length - 1 ? "border-b border-line" : ""}`}
                >
                  <ProviderMark iconUrl={p.iconUrl} />
                  <div className="min-w-[220px] flex-1">
                    <Link
                      to={`/settings/oidc/providers/${encodeURIComponent(p.name)}`}
                      className="text-[15px] font-semibold text-fg hover:underline"
                    >
                      {p.displayName}
                    </Link>
                    <div className="text-[13px] text-fg-muted">{meta(p)}</div>
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusBadge tone={p.enabled ? "healthy" : "unknown"}>{p.enabled ? "Enabled" : "Disabled"}</StatusBadge>
                    {p.reconnectNeeded && <StatusBadge tone="degraded">Reconnect needed</StatusBadge>}
                  </div>
                  <div className="w-[190px]">
                    <LastResult provider={p} />
                  </div>
                  <div className="flex gap-2">
                    <Button variant="secondary" disabled={busy !== null} onClick={() => openWith(p, "test")}>
                      Test
                    </Button>
                    {p.isPaired && (
                      <Button variant="secondary" disabled={busy !== null} onClick={() => openWith(p, "reconnect")}>
                        Reconnect
                      </Button>
                    )}
                    <ButtonLink variant="secondary" to={`/settings/oidc/providers/${encodeURIComponent(p.name)}`}>
                      Edit
                    </ButtonLink>
                  </div>
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>
    </div>
  );
}
