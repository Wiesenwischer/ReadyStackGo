import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import {
  getOidcSettings,
  removeOidcProvider,
  setupApi,
  type OidcProviderSettingsDto,
  type SetupSessionDto,
} from "@rsgo/core";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { CopyField, TextField, Toggle } from "../../../components/ui/FormControls";
import { ProviderMark } from "../../../components/sso/SsoComponents";
import { Card, SettingsBreadcrumb } from "./oidcShared";
import { formatDate } from "./oidcFormat";
import { ConnectStep, TestPanel } from "./steps/SetupSteps";
import { redirectUriFor } from "./setupSteps";

// Settings › Single Sign-On › <provider> (design frame 176:4851): connection, Test, Reconnect,
// sign-in settings, and the lockout protection (plan E14).

const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback);

export default function OidcProviderPage() {
  const { name = "" } = useParams();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const [provider, setProvider] = useState<OidcProviderSettingsDto | null>(null);
  const [session, setSession] = useState<SetupSessionDto | null>(null);
  const [mode, setMode] = useState<"none" | "test" | "reconnect" | "connection">("none");
  const [displayName, setDisplayName] = useState("");
  const [trust, setTrust] = useState(false);
  const [enabled, setEnabled] = useState(false);
  const [authority, setAuthority] = useState("");
  const [clientId, setClientId] = useState("");
  const [clientSecret, setClientSecret] = useState("");
  const [scopes, setScopes] = useState("");
  const [confirmRemove, setConfirmRemove] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    const settings = await getOidcSettings();
    const p = settings.providers.find((x) => x.name.toLowerCase() === name.toLowerCase()) ?? null;
    setProvider(p);
    if (p) {
      setDisplayName(p.displayName);
      setTrust(p.trustUnverifiedEmail);
      setEnabled(p.enabled);
      setAuthority(p.authority);
      setClientId(p.clientId);
      setScopes(p.scopes);
    }
    return p;
  }, [name]);

  useEffect(() => {
    load().catch((err) => setError(errorText(err, "Failed to load the provider")));
  }, [load]);

  // A session in the address (Test/Reconnect from the list, or back from the provider).
  useEffect(() => {
    const id = params.get("session");
    if (!id || session?.id === id) return;
    setupApi
      .get(id)
      .then((s) => {
        setSession(s);
        const action = params.get("action");
        if (action === "reconnect" || s.registrationPending || (s.registrationError && !s.testSignIn)) setMode("reconnect");
        else setMode("test");
        if (s.testSignIn?.passed && s.testSignIn.current) {
          // A passed test sign-in with new credentials takes them over on the server.
          void load();
        }
      })
      .catch(() => setParams({}, { replace: true }));
  }, [params, session?.id, setParams, load]);

  const ensureSession = async () => {
    if (session) return session;
    const s = await setupApi.createForProvider(name);
    setSession(s);
    setParams({ session: s.id }, { replace: true });
    return s;
  };

  const startMode = async (next: "test" | "reconnect") => {
    setError(null);
    try {
      await ensureSession();
      setMode(next);
    } catch (err) {
      setError(errorText(err, "The provider could not be opened."));
    }
  };

  const applyConnection = async () => {
    setBusy(true);
    setError(null);
    try {
      let s = await ensureSession();
      if (authority.trim() !== (s.authority ?? "")) s = await setupApi.discovery(s.id, authority.trim());
      const result = await setupApi.register(s.id, { clientId, clientSecret: clientSecret || undefined, scopes });
      setSession(result.session);
      setClientSecret("");
      setMode("test");
    } catch (err) {
      setError(errorText(err, "The connection could not be changed."));
    } finally {
      setBusy(false);
    }
  };

  const save = async () => {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const s = await ensureSession();
      await setupApi.update(s.id, { displayName, trustUnverifiedEmail: trust });
      await setupApi.save(s.id, enabled);
      setSession(null);
      setParams({}, { replace: true });
      setMode("none");
      await load();
      setNotice("Changes saved.");
    } catch (err) {
      setError(errorText(err, "The changes could not be saved."));
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    setBusy(true);
    setError(null);
    try {
      if (session) await setupApi.cancel(session.id).catch(() => undefined);
      await removeOidcProvider(name);
      navigate("/settings/oidc", { replace: true });
    } catch (err) {
      setError(errorText(err, "The provider could not be removed."));
      setBusy(false);
    }
  };

  const onSession = useCallback(
    (s: SetupSessionDto) => {
      setSession(s);
      if (s.testSignIn?.passed && s.testSignIn.current) void load();
    },
    [load],
  );

  if (!provider) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <SettingsBreadcrumb trail={[{ label: "Single Sign-On", to: "/settings/oidc" }, name]} />
        {error ? <Alert tone="error" title="Not found">{error}</Alert> : <p className="text-sm text-fg-muted">Loading…</p>}
      </div>
    );
  }

  const locked = provider.isOnlySignInOfCurrentUser;
  const isPaired = provider.isPaired;
  const templateName = provider.templateName;

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <SettingsBreadcrumb trail={[{ label: "Single Sign-On", to: "/settings/oidc" }, provider.displayName]} />
      <div className="mb-6 flex max-w-[860px] flex-wrap items-end justify-between gap-4">
        <div className="flex items-center gap-3">
          <ProviderMark iconUrl={provider.iconUrl} />
          <div>
            <h2 className="text-[26px] font-bold leading-[34px] text-fg">{provider.displayName}</h2>
            <p className="text-sm text-fg-secondary">
              {templateName} template · {provider.enabled ? "enabled" : "disabled"}
              {provider.reconnectNeeded ? " · reconnect needed" : ""}
            </p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button variant="secondary" onClick={() => startMode("test")} data-testid="provider-test">
            Test
          </Button>
          {isPaired && (
            <Button variant="secondary" onClick={() => startMode("reconnect")} data-testid="provider-reconnect">
              Reconnect
            </Button>
          )}
        </div>
      </div>

      <div className="flex max-w-[860px] flex-col gap-6">
        {locked && (
          <Alert tone="warning" title="You sign in with this provider and have no local password" testId="lockout-warning">
            So you can't lock yourself out: this provider can't be disabled or removed, and new credentials from Reconnect or changed
            settings take effect only after a successful test sign-in. Set a local password in your profile to lift this.
          </Alert>
        )}
        {error && <Alert tone="error" title="Something went wrong">{error}</Alert>}
        {notice && <Alert tone="success" title={notice} />}

        <Card
          title="Connection"
          description={
            isPaired
              ? `Set up by the ${templateName} template. Reconnect replaces client ID and secret, for example after the connection was removed in ${templateName}.`
              : "Changes to the connection take effect after a successful test sign-in while the provider is enabled."
          }
          testId="connection-card"
        >
          {isPaired ? (
            <dl className="flex flex-col gap-2.5 text-[13px]">
              {[
                ["Provider address", `${provider.authority} (fixed by the template)`],
                ["Client ID", provider.clientId],
                ["Client secret", provider.hasClientSecret ? "Stored encrypted" : "Not set"],
                [
                  "Connected",
                  provider.pairedAt ? `${formatDate(provider.pairedAt)}${provider.pairedBy ? ` by ${provider.pairedBy}` : ""}` : "—",
                ],
              ].map(([key, value]) => (
                <div key={key} className="flex gap-3">
                  <dt className="w-[150px] shrink-0 font-medium text-fg-muted">{key}</dt>
                  <dd className={key === "Client ID" ? "font-mono text-fg" : "text-fg"}>{value}</dd>
                </div>
              ))}
            </dl>
          ) : (
            <>
              <TextField label="Provider address (authority)" value={authority} onChange={(e) => setAuthority(e.target.value)} testId="authority" />
              <div className="grid gap-4 sm:grid-cols-2">
                <TextField label="Client ID" value={clientId} onChange={(e) => setClientId(e.target.value)} testId="client-id" autoComplete="off" />
                <TextField
                  label="Client secret"
                  type="password"
                  value={clientSecret}
                  onChange={(e) => setClientSecret(e.target.value)}
                  placeholder={provider.hasClientSecret ? "•••••••• (unchanged)" : undefined}
                  hint="Empty keeps the stored secret."
                  testId="client-secret"
                  autoComplete="new-password"
                />
              </div>
              <TextField label="Scopes" value={scopes} onChange={(e) => setScopes(e.target.value)} testId="scopes" />
              <div>
                <Button variant="secondary" onClick={applyConnection} disabled={busy} data-testid="apply-connection">
                  Check and test the connection
                </Button>
              </div>
            </>
          )}
          {session?.baseUrl && <CopyField label="Redirect URI" value={redirectUriFor(session.baseUrl, provider.name)} />}
        </Card>

        {mode === "reconnect" && session && (
          <Card testId="reconnect-card">
            <ConnectStep
              session={session}
              onSession={onSession}
              onNext={() => setMode("test")}
              onCancel={() => setMode("none")}
              reconnect
            />
          </Card>
        )}

        {mode === "test" && session && (
          <Card title="Test" testId="test-card">
            <TestPanel session={session} onSession={onSession} />
            {provider.reconnectNeeded && isPaired && session.checks?.items.some((i) => i.code === "invalid_client") && (
              <div>
                <Button onClick={() => setMode("reconnect")}>Reconnect</Button>
              </div>
            )}
          </Card>
        )}

        <Card title="Sign-in" testId="sign-in-card">
          <div className="grid gap-4 sm:grid-cols-2">
            <TextField
              label="Name"
              value={provider.name}
              readOnly
              hint={`Used in the sign-in address /api/auth/oidc/${provider.name}/…`}
            />
            <TextField label="Display name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} testId="display-name" />
          </div>
          <Toggle
            checked={locked ? true : enabled}
            onChange={setEnabled}
            disabled={locked || (!provider.enabled && !provider.hasPassedTestSignIn && !session?.canEnable)}
            label="Enable provider"
            testId="enable-provider"
            description={
              locked
                ? undefined
                : !provider.enabled && !provider.hasPassedTestSignIn && !session?.canEnable
                  ? "Run the checks and a test sign-in before you enable this provider."
                  : undefined
            }
          />
          <Toggle checked={trust} onChange={setTrust} label="Trust unverified email addresses" testId="trust-unverified" />

          {confirmRemove && (
            <Alert tone="warning" title={`Remove ${provider.displayName}?`}>
              Users linked to this provider can no longer sign in with it, and their links are removed.
              {isPaired ? ` Remove the connection in ${templateName} as well.` : ""}
              <div className="mt-3 flex gap-2">
                <Button onClick={remove} disabled={busy} data-testid="confirm-remove">
                  Remove provider
                </Button>
                <Button variant="secondary" onClick={() => setConfirmRemove(false)} disabled={busy}>
                  Keep it
                </Button>
              </div>
            </Alert>
          )}

          <div className="flex flex-wrap items-center gap-3 border-t border-line pt-5">
            <button
              type="button"
              onClick={() => setConfirmRemove(true)}
              disabled={locked || busy}
              className="text-sm font-medium text-status-unhealthy hover:underline disabled:cursor-not-allowed disabled:text-fg-muted disabled:opacity-60 disabled:no-underline"
              data-testid="remove-provider"
            >
              Remove provider
            </button>
            <div className="flex-1" />
            <Button onClick={save} disabled={busy} data-testid="save-changes">
              Save changes
            </Button>
          </div>
        </Card>
      </div>
    </div>
  );
}
