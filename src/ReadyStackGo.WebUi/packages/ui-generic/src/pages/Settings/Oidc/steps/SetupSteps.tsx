import { useEffect, useRef, useState, type ReactNode } from "react";
import { followRegistrationStart, isHttpUrl, setupApi, type SetupSessionDto } from "@rsgo/core";
import { Alert } from "../../../../components/ui/Alert";
import { Button } from "../../../../components/ui/Button";
import { CopyField, TextField, Toggle } from "../../../../components/ui/FormControls";
import { StatusBadge } from "../../../../components/ui/StatusBadge";
import { CheckList, CheckRow, ClaimsTable, ProviderButton } from "../../../../components/sso/SsoComponents";
import { CheckCircleIcon, CheckIcon, ChevronDownIcon } from "../../../../components/sso/icons";
import { normalizeBaseUrl, satisfiesHttpsRequirement, suggestBaseUrl } from "../../../../components/sso/baseUrl";
import { canContinue, discoveryPassed, isValidProviderName, redirectUriFor, testBadge } from "../setupSteps";

// Steps of "Add provider" and the provider page (design frames 173:3483 … 176:4615).

type SessionUpdate = (session: SetupSessionDto) => void;

const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback);

/** Footer of a step: Back left, Cancel and the primary action right. */
export function StepFooter({
  onBack,
  onCancel,
  primaryLabel = "Continue",
  primaryDisabled,
  onPrimary,
  busy,
  left,
}: {
  onBack?: () => void;
  onCancel?: () => void;
  primaryLabel?: string;
  primaryDisabled?: boolean;
  onPrimary?: () => void;
  busy?: boolean;
  left?: ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-center gap-3 border-t border-line pt-5">
      {onBack && (
        <Button variant="secondary" onClick={onBack} disabled={busy}>
          Back
        </Button>
      )}
      {left}
      <div className="flex-1" />
      {onCancel && (
        <button type="button" onClick={onCancel} className="px-2 text-sm font-medium text-fg-secondary hover:text-fg">
          Cancel
        </button>
      )}
      {onPrimary && (
        <Button onClick={onPrimary} disabled={primaryDisabled || busy} data-testid="step-primary">
          {primaryLabel}
        </Button>
      )}
    </div>
  );
}

export function StepHeading({ title, description }: { title: string; description?: ReactNode }) {
  return (
    <div>
      <h3 className="text-xl font-semibold text-fg">{title}</h3>
      {description && <p className="mt-1.5 text-sm text-fg-secondary">{description}</p>}
    </div>
  );
}

interface StepProps {
  session: SetupSessionDto;
  onSession: SessionUpdate;
  onNext: () => void;
  onBack?: () => void;
  onCancel: () => void;
}

// ---------------------------------------------------------------- Provider address

export function ProviderAddressStep({ session, onSession, onNext, onBack, onCancel }: StepProps) {
  const [authority, setAuthority] = useState(session.authority ?? "");
  const [checkedAuthority, setCheckedAuthority] = useState<string | null>(session.discovery ? (session.authority ?? null) : null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const report = session.discovery;
  const failed = !!report && !discoveryPassed(report) && checkedAuthority === authority.trim();
  const failure = failed ? report!.items.find((i) => i.status === "failed") : undefined;

  const check = async () => {
    setBusy(true);
    setError(null);
    try {
      const updated = await setupApi.discovery(session.id, authority.trim());
      onSession(updated);
      setCheckedAuthority(authority.trim());
      if (discoveryPassed(updated.discovery)) onNext();
    } catch (err) {
      setError(errorText(err, "The provider address could not be checked."));
    } finally {
      setBusy(false);
    }
  };

  const passed = !!report && discoveryPassed(report) && checkedAuthority === authority.trim();

  return (
    <>
      <StepHeading title="Provider address" description="The issuer URL of your OpenID Connect provider. ReadyStackGo checks it right away." />
      <TextField
        label="Provider address (authority)"
        value={authority}
        onChange={(e) => setAuthority(e.target.value)}
        placeholder={session.template?.authorityExample ?? "https://login.example.com/realms/main"}
        testId="authority"
        error={failed ? `Check the address. ${session.template?.authorityHint ?? ""}`.trim() : undefined}
        hint={session.template?.authorityExample ? `For example ${session.template.authorityExample}` : undefined}
      />
      {failed && failure && <ul><CheckRow result="failed" title={failure.title} detail={failure.detail} testId="discovery-result" /></ul>}
      {passed && (
        <ul>
          <CheckRow
            result="passed"
            title="Discovery document found"
            detail={`Issuer ${report.issuer} · authorization, token and JWKS endpoints present`}
            testId="discovery-result"
          />
        </ul>
      )}
      {error && <Alert tone="error" title="Check failed">{error}</Alert>}
      <StepFooter
        onBack={onBack}
        onCancel={onCancel}
        busy={busy}
        primaryLabel={busy ? "Checking…" : "Continue"}
        primaryDisabled={!authority.trim() || !canContinue("provider", session, { authorityUnchangedSinceCheck: checkedAuthority === authority.trim() })}
        onPrimary={passed ? onNext : check}
      />
    </>
  );
}

// ---------------------------------------------------------------- This installation

export function InstallationStep({ session, onSession, onNext, onBack, onCancel }: StepProps) {
  const [baseUrl, setBaseUrl] = useState(session.baseUrl ?? suggestBaseUrl(window.location.origin));
  const [name, setName] = useState(session.name);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const normalized = normalizeBaseUrl(baseUrl);
  const httpsMissing = session.requireHttps && normalized !== null && !satisfiesHttpsRequirement(normalized);
  const nameValid = !session.isNew || isValidProviderName(name);
  const providerName = session.template?.name ?? "This provider";

  const save = async () => {
    if (!normalized) return;
    setBusy(true);
    setError(null);
    try {
      onSession(await setupApi.installation(session.id, normalized, session.isNew ? name : undefined));
      onNext();
    } catch (err) {
      setError(errorText(err, "The address could not be saved."));
    } finally {
      setBusy(false);
    }
  };

  const changedConfigured = session.baseUrlConfigured && session.baseUrl && normalized && normalized !== session.baseUrl;
  return (
    <>
      <StepHeading title="Address of this installation" description="The provider sends users back to this address after they sign in." />
      <TextField
        label="Address of this installation"
        value={baseUrl}
        onChange={(e) => setBaseUrl(e.target.value)}
        testId="base-url"
        error={
          normalized === null
            ? "Enter an absolute address such as https://rsgo.example.com."
            : httpsMissing
              ? `${providerName} only connects installations that use HTTPS. http:// is allowed for localhost only.`
              : undefined
        }
        hint={
          changedConfigured
            ? "Changing the address also changes the redirect URIs of existing providers and the links in emails."
            : session.baseUrlConfigured
              ? "The base URL of ReadyStackGo."
              : "No base URL is set yet. Suggested from the address in your browser and saved as the base URL when you continue."
        }
      />
      {httpsMissing && (
        <Alert tone="warning" title="HTTPS required">
          Make ReadyStackGo reachable under an https:// address (for example behind a reverse proxy with a certificate) and
          reload this page.
        </Alert>
      )}
      {session.isNew && (
        <TextField
          label="Name"
          value={name}
          onChange={(e) => setName(e.target.value.toLowerCase())}
          testId="provider-name"
          error={nameValid ? undefined : "Use lowercase letters, digits and dashes (up to 40), starting with a letter or digit."}
          hint={`Used in the sign-in address /api/auth/oidc/${name || "<name>"}/… and cannot be changed later.`}
        />
      )}
      {normalized && <CopyField label="Redirect URI" value={redirectUriFor(normalized, session.isNew ? name : session.name)} testId="redirect-uri" />}
      {error && <Alert tone="error" title="Not saved">{error}</Alert>}
      <StepFooter
        onBack={onBack}
        onCancel={onCancel}
        busy={busy}
        primaryDisabled={!canContinue("installation", session, { baseUrlValid: normalized !== null && !httpsMissing, nameValid })}
        onPrimary={save}
      />
    </>
  );
}

// ---------------------------------------------------------------- Connect (pairing)

export function ConnectStep({ session, onSession, onNext, onBack, onCancel, reconnect }: StepProps & { reconnect?: boolean }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const completing = useRef(false);
  const name = session.template?.name ?? "the provider";

  // Back from the provider with a code: redeem it right away.
  useEffect(() => {
    if (!session.registrationPending || completing.current) return;
    completing.current = true;
    setBusy(true);
    setupApi
      .completeRegistration(session.id)
      .then(onSession)
      .catch(async (err) => {
        setError(errorText(err, "The connection could not be completed."));
        onSession(await setupApi.get(session.id));
      })
      .finally(() => {
        setBusy(false);
        completing.current = false;
      });
  }, [session.id, session.registrationPending, onSession]);

  const connect = async () => {
    setBusy(true);
    setError(null);
    try {
      const result = await setupApi.register(session.id);
      if (result.start && result.start.kind !== "completed") {
        followRegistrationStart(result.start);
        return;
      }
      onSession(result.session);
      setBusy(false);
    } catch (err) {
      setError(errorText(err, "The connection could not be started."));
      setBusy(false);
    }
  };

  const connected = session.registeredInSession && !session.registrationPending && !session.registrationError;

  return (
    <>
      <StepHeading
        title={reconnect ? `Reconnect with ${name}` : `Connect with ${name}`}
        description={
          connected
            ? undefined
            : `You sign in to ${name} and confirm the connection. ReadyStackGo then receives its client ID and secret, nothing to copy or type.`
        }
      />
      {connected ? (
        <>
          <Alert tone="success" title={`Connected with ${name}`} testId="connected">
            {session.pairedBy ? `Confirmed by ${session.pairedBy} in ${name}. ` : ""}Client ID {session.clientId} received, the secret is
            stored encrypted.
          </Alert>
          <div className="flex gap-1.5 text-[13px]">
            <span className="text-fg-muted">Connected the wrong account?</span>
            <button type="button" onClick={connect} disabled={busy} className="font-medium text-fg-brand hover:underline">
              Connect again
            </button>
          </div>
        </>
      ) : (
        <>
          {session.registrationError && (
            <Alert tone={session.registrationError === "access_denied" ? "warning" : "error"} title="Not connected" testId="registration-error">
              {session.registrationErrorDescription ?? session.registrationError}
            </Alert>
          )}
          <div className="flex flex-col gap-2 rounded-xl bg-raised px-4 py-3.5">
            <div className="text-[13px] font-semibold text-fg">What happens</div>
            {[
              `${name} shows the name and address of this installation (${session.baseUrl}) and asks you to confirm.`,
              "ReadyStackGo receives its client ID and secret. The secret is stored encrypted and never shown.",
              `When users sign in, ${name} shares their name, email address and username.`,
            ].map((line) => (
              <div key={line} className="flex gap-2 text-[13px] text-fg-secondary">
                <CheckIcon size={16} className="mt-0.5 shrink-0 text-fg-brand" />
                <span>{line}</span>
              </div>
            ))}
          </div>
          <ProviderButton
            label={busy ? `Connecting with ${name}…` : `Connect with ${name}`}
            iconUrl={session.template?.iconUrl}
            onClick={connect}
            disabled={busy}
            className="w-[280px]"
            testId="connect"
          />
        </>
      )}
      {error && <Alert tone="error" title="Not connected">{error}</Alert>}
      <StepFooter onBack={onBack} onCancel={onCancel} busy={busy} primaryDisabled={!canContinue("connect", session, {})} onPrimary={onNext} />
    </>
  );
}

// ---------------------------------------------------------------- Register (manual)

export function RegisterStep({ session, onSession, onNext, onBack, onCancel }: StepProps) {
  const [clientId, setClientId] = useState(session.clientId ?? "");
  const [clientSecret, setClientSecret] = useState("");
  const [scopes, setScopes] = useState(session.scopes);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const result = await setupApi.register(session.id, { clientId, clientSecret: clientSecret || undefined, scopes });
      onSession(result.session);
      onNext();
    } catch (err) {
      setError(errorText(err, "The client could not be saved."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <StepHeading
        title="Register ReadyStackGo at your provider"
        description="Create a confidential client (authorization code with PKCE) at your provider, enter the redirect URI there, then paste the client ID and secret here."
      />
      {session.redirectUri && <CopyField label="Redirect URI" value={session.redirectUri} testId="redirect-uri" />}
      <div className="grid gap-4 sm:grid-cols-2">
        <TextField label="Client ID" value={clientId} onChange={(e) => setClientId(e.target.value)} testId="client-id" autoComplete="off" />
        <TextField
          label="Client secret"
          type="password"
          value={clientSecret}
          onChange={(e) => setClientSecret(e.target.value)}
          placeholder={session.hasClientSecret ? "•••••••• (unchanged)" : undefined}
          testId="client-secret"
          autoComplete="new-password"
        />
      </div>
      <TextField label="Scopes" value={scopes} onChange={(e) => setScopes(e.target.value)} hint="Separated by spaces. openid is required." testId="scopes" />
      {error && <Alert tone="error" title="Not saved">{error}</Alert>}
      <StepFooter
        onBack={onBack}
        onCancel={onCancel}
        busy={busy}
        primaryDisabled={!canContinue("register", session, { clientId })}
        onPrimary={save}
      />
    </>
  );
}

// ---------------------------------------------------------------- Test

/** Checks without sign-in and the test sign-in (frames 175:3875, 175:4103, 175:4351, 175:4606). */
export function TestPanel({ session, onSession, autoRun = true }: { session: SetupSessionDto; onSession: SessionUpdate; autoRun?: boolean }) {
  const [busy, setBusy] = useState<"checks" | "signIn" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState(false);
  const started = useRef(false);
  const checks = session.checks?.current ? session.checks : null;
  const test = session.testSignIn?.current ? session.testSignIn : null;
  // Templates with a fixed provider (WYSCH) name it; Generic OIDC says "your provider".
  const name = session.template?.hasFixedAuthority ? session.template.name : session.displayName || "your provider";

  const runChecks = async () => {
    setBusy("checks");
    setError(null);
    try {
      onSession(await setupApi.runChecks(session.id));
    } catch (err) {
      setError(errorText(err, "The checks could not run."));
    } finally {
      setBusy(null);
    }
  };

  useEffect(() => {
    if (!autoRun || started.current || checks || !session.clientId) return;
    started.current = true;
    void runChecks();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoRun, checks, session.clientId]);

  const startTestSignIn = async () => {
    setBusy("signIn");
    setError(null);
    try {
      const { url } = await setupApi.startTestSignIn(session.id);
      if (!isHttpUrl(url)) {
        throw new Error("The provider returned a sign-in address that is not http or https.");
      }
      window.location.href = url;
    } catch (err) {
      setError(errorText(err, "The test sign-in could not start."));
      setBusy(null);
    }
  };

  const showChecks = !test || !test.passed || expanded;

  return (
    <div className="flex flex-col gap-[22px]" data-testid="test-panel">
      {checks && !checks.passed && (
        <Alert tone="error" title={`${checks.failedCount} of ${checks.executedCount} checks failed`} testId="checks-failed">
          Fix the problems below and run the checks again. Without a passing test you can only save the provider disabled.
        </Alert>
      )}

      {test && checks?.passed && !showChecks ? (
        <div className="flex items-center gap-2.5 rounded-xl bg-raised px-4 py-3" data-testid="checks-summary">
          <CheckCircleIcon size={20} className="text-status-healthy" />
          <span className="flex-1 text-sm font-medium text-fg">All {checks.executedCount} checks without sign-in passed</span>
          <button type="button" onClick={() => setExpanded(true)} className="flex items-center gap-1 text-[13px] font-medium text-fg-brand">
            Show <ChevronDownIcon size={16} />
          </button>
        </div>
      ) : (
        <>
          <div className="flex flex-wrap items-center gap-4">
            <div className="min-w-0 flex-1">
              <h4 className="text-base font-semibold text-fg">Checks without sign-in</h4>
              <p className="mt-1 text-[13px] text-fg-muted">ReadyStackGo checks the provider from the server.</p>
            </div>
            <Button variant="secondary" onClick={runChecks} disabled={busy !== null} data-testid="run-checks">
              {busy === "checks" ? "Checking…" : checks ? "Run again" : "Run checks"}
            </Button>
          </div>
          {checks ? <CheckList items={checks.items} testId="checks" /> : busy === "checks" && <ul><CheckRow result="running" title="Running the checks…" /></ul>}
        </>
      )}

      <div className="flex flex-wrap items-center gap-4">
        <div className="min-w-0 flex-1">
          <h4 className="text-base font-semibold text-fg">Test sign-in</h4>
          <p className="mt-1 text-[13px] text-fg-muted">
            {test?.passed
              ? `Test sign-in passed. Signed in at ${name}${test.signedInAs ? ` as ${test.signedInAs}` : ""}. You are not signed in to ReadyStackGo with this, and no account was linked.`
              : checks?.passed
                ? `Required before you enable the provider. Sign in once at ${name} to see which details arrive. This does not sign you in to ReadyStackGo and does not link an account.`
                : "Available when all checks pass."}
          </p>
        </div>
        <Button variant="secondary" onClick={startTestSignIn} disabled={busy !== null || !checks?.passed} data-testid="run-test-sign-in">
          {test ? "Run again" : "Run test sign-in"}
        </Button>
      </div>

      {test && test.passed && !test.warningTitle && (
        <Alert tone="success" title="These details are enough to sign in" testId="test-result">
          Users with an account or an invitation for {test.signedInAs} can sign in with {name}.
        </Alert>
      )}
      {test && test.passed && test.warningTitle && (
        <Alert tone="warning" title={test.warningTitle} testId="test-result">
          {test.warningBody}
        </Alert>
      )}
      {test && !test.passed && (
        <Alert tone="error" title={test.error ?? "The test sign-in failed"} testId="test-result">
          {test.errorDetail}
        </Alert>
      )}
      {test && test.claims.length > 0 && <ClaimsTable claims={test.claims} />}
      {test?.claims.some((c) => c.status === "missing" && c.detail === "Username") && (
        <p className="text-xs text-fg-muted">
          Username missing: ReadyStackGo forms the username from the email address
          {test.signedInAs?.includes("@") ? ` (${test.signedInAs.split("@")[0].replace(/[^a-zA-Z0-9_]/g, "_")})` : ""}.
        </p>
      )}
      {error && <Alert tone="error" title="Something went wrong">{error}</Alert>}
    </div>
  );
}

export function TestStep({ session, onSession, onNext, onBack, onCancel }: StepProps) {
  return (
    <>
      <TestPanel session={session} onSession={onSession} />
      <StepFooter onBack={onBack} onCancel={onCancel} primaryDisabled={!canContinue("test", session, {})} onPrimary={onNext} />
    </>
  );
}

// ---------------------------------------------------------------- Save

export function SaveStep({ session, onSession, onBack, onCancel, onSaved }: Omit<StepProps, "onNext"> & { onSaved: (name: string) => void }) {
  const [displayName, setDisplayName] = useState(session.displayName || session.name);
  const [enabled, setEnabled] = useState(session.canEnable);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const badge = testBadge(session);

  const setTrust = async (trust: boolean) => {
    try {
      onSession(await setupApi.update(session.id, { trustUnverifiedEmail: trust }));
    } catch (err) {
      setError(errorText(err, "The setting could not be changed."));
    }
  };

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      await setupApi.update(session.id, { displayName });
      const saved = await setupApi.save(session.id, enabled && session.canEnable);
      onSaved(saved.name);
    } catch (err) {
      setError(errorText(err, "The provider could not be saved."));
      setBusy(false);
    }
  };

  return (
    <>
      <StepHeading title="Save the provider" />
      <div className="grid gap-4 sm:grid-cols-2">
        <TextField
          label="Name"
          value={session.name}
          readOnly
          hint={`Used in the sign-in address /api/auth/oidc/${session.name}/… Letters, digits and dashes.`}
          testId="save-name"
        />
        <TextField label="Display name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} hint="Shown on the sign-in page." testId="display-name" />
      </div>
      <Toggle
        checked={enabled && session.canEnable}
        onChange={setEnabled}
        disabled={!session.canEnable}
        label="Enable provider"
        testId="enable-provider"
        extra={<StatusBadge tone={badge.tone}>{badge.label}</StatusBadge>}
        description={
          session.canEnable
            ? `Users see “Sign in with ${displayName}” on the sign-in page.`
            : "Run the checks and a test sign-in before you enable this provider. You can save it disabled now and test it later from the provider list."
        }
      />
      <Toggle
        checked={session.trustUnverifiedEmail}
        onChange={setTrust}
        label="Trust unverified email addresses"
        testId="trust-unverified"
        description="Match sign-ins to existing users by email even if the provider has not confirmed the address. Off for new providers."
      />
      <div className="flex flex-col gap-2.5 rounded-xl bg-raised p-4">
        <div className="text-xs font-semibold text-fg-muted">On the sign-in page</div>
        <ProviderButton label={`Sign in with ${displayName}`} iconUrl={session.template?.iconUrl} className="w-[360px] max-w-full" />
      </div>
      {error && <Alert tone="error" title="Not saved">{error}</Alert>}
      <StepFooter
        onBack={onBack}
        onCancel={onCancel}
        busy={busy}
        primaryLabel={enabled && session.canEnable ? "Save provider" : "Save disabled"}
        onPrimary={save}
      />
    </>
  );
}
