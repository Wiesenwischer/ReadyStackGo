import { useState } from 'react';
import type { IdentityProviderTemplateDto, WizardSsoRunDto } from '@rsgo/core';
import { Alert } from '../../components/ui/Alert';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/FormControls';
import { CheckRow, ProviderButton } from '../../components/sso/SsoComponents';
import { ArrowLeftIcon, CheckCircleIcon, CheckIcon, ClockIcon, LoaderIcon, MinusCircleIcon, XCircleIcon } from '../../components/sso/icons';
import { normalizeBaseUrl, satisfiesHttpsRequirement } from '../../components/sso/baseUrl';
import { waitingRows, type WizardErrorContent } from './wizardFlow';

// The WYSCH path of the wizard (design frames 171:2624, 171:2717, 171:2802, 172:2728, 172:2794,
// 172:2859, 172:2923).

interface SsoAddressStepProps {
  template: IdentityProviderTemplateDto;
  initialBaseUrl: string;
  busy: boolean;
  error?: string | null;
  onConnect: (baseUrl: string) => void;
  onBack: () => void;
  onBuiltIn: () => void;
}

/** Confirm the address of this installation, then "Connect with WYSCH". */
export function SsoAddressStep({ template, initialBaseUrl, busy, error, onConnect, onBack, onBuiltIn }: SsoAddressStepProps) {
  const [baseUrl, setBaseUrl] = useState(initialBaseUrl);
  const normalized = normalizeBaseUrl(baseUrl);
  const httpsMissing = template.requireHttps && normalized !== null && !satisfiesHttpsRequirement(normalized);
  const invalid = normalized === null;
  const name = template.name;

  return (
    <div className="flex flex-col gap-6">
      <button
        type="button"
        onClick={onBack}
        className="inline-flex w-fit items-center gap-1.5 text-[13px] font-medium text-fg-secondary hover:text-fg"
      >
        <ArrowLeftIcon size={16} />
        Back
      </button>

      <div>
        <h2 className="mb-1.5 text-xl font-semibold text-fg">Connect with {name}</h2>
        <p className="text-sm text-fg-secondary">
          ReadyStackGo sends you to {name}. Sign in there and confirm the connection. You come back here automatically,
          and your {name} account becomes the first administrator.
        </p>
      </div>

      <TextField
        label="Address of this installation"
        value={baseUrl}
        onChange={(e) => setBaseUrl(e.target.value)}
        testId="sso-base-url"
        error={
          invalid
            ? 'Enter an absolute address such as https://rsgo.example.com.'
            : httpsMissing
              ? `${name} only connects installations that use HTTPS. http:// is allowed for localhost only.`
              : undefined
        }
        hint="Suggested from the address in your browser. Saved as the base URL of ReadyStackGo."
      />

      {httpsMissing ? (
        <Alert tone="warning" title="HTTPS required" testId="sso-https-required">
          Make ReadyStackGo reachable under an https:// address (for example behind a reverse proxy with a certificate)
          and reload this page. Or use built-in sign-in now and add {name} later under Settings › Single Sign-On.
        </Alert>
      ) : (
        <div className="flex flex-col gap-2 rounded-xl bg-raised px-4 py-3.5">
          <div className="text-[13px] font-semibold text-fg">What happens</div>
          {[
            `${name} shows the name and address of this installation and asks you to confirm.`,
            `ReadyStackGo receives its client ID and secret from ${name}. The secret is stored encrypted.`,
            `When users sign in, ${name} shares their name, email address and username.`,
          ].map((line) => (
            <div key={line} className="flex gap-2 text-[13px] text-fg-secondary">
              <CheckIcon size={16} className="mt-0.5 shrink-0 text-fg-brand" />
              <span>{line}</span>
            </div>
          ))}
        </div>
      )}

      {error && (
        <Alert tone="error" title="The connection could not be started">
          {error}
        </Alert>
      )}

      <ProviderButton
        label={busy ? `Connecting with ${name}…` : `Connect with ${name}`}
        iconUrl={template.iconUrl}
        disabled={busy || invalid || httpsMissing}
        onClick={() => normalized && onConnect(normalized)}
        className="w-full"
        testId="sso-connect"
      />

      <div className="flex justify-center">
        <button type="button" onClick={onBuiltIn} className="text-[13px] font-medium text-fg-brand hover:underline">
          Use built-in sign-in instead
        </button>
      </div>
    </div>
  );
}

/** Waiting state after the return from the provider. */
export function SsoWaitingCard({ run }: { run: WizardSsoRunDto }) {
  const minutes = Math.round((new Date(run.expiresAt).getTime() - new Date(run.startedAt).getTime()) / 60000);
  const started = new Date(run.startedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  return (
    <div className="flex flex-col items-center gap-6" data-testid="sso-waiting">
      <LoaderIcon size={40} className="text-primary" />
      <div className="flex flex-col items-center gap-1.5 text-center">
        <h2 className="text-xl font-semibold text-fg">Finishing the connection with {run.templateName}…</h2>
        <p className="text-sm text-fg-secondary">This takes a few seconds. Keep this page open.</p>
      </div>
      <ul className="w-full">
        {waitingRows(run).map((row) => (
          <CheckRow key={row.title} result={row.result} title={row.title} />
        ))}
      </ul>
      <p className="text-xs text-fg-muted">
        The run started at {started} and stays valid for up to {minutes} minutes.
      </p>
    </div>
  );
}

/** Result "Signed in as …". */
export function SsoSignedInCard({ run, onContinue }: { run: WizardSsoRunDto; onContinue: () => void }) {
  const name = run.signedInDisplayName || run.signedInUsername || '';
  const initials =
    name
      .split(/[\s._-]+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('') || '?';
  return (
    <div className="flex flex-col gap-6" data-testid="sso-signed-in">
      <div className="flex items-center gap-4">
        <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary-subtle text-lg font-semibold text-fg-brand">
          {initials}
        </span>
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <CheckCircleIcon size={20} className="text-status-healthy" />
            <h2 className="text-xl font-semibold text-fg">Signed in as {name}</h2>
          </div>
          <p className="text-sm text-fg-secondary">
            {run.signedInEmail} · username {run.signedInUsername} · via {run.templateName}
          </p>
        </div>
      </div>
      <dl className="flex flex-col gap-2.5 rounded-xl bg-raised p-4 text-[13px]">
        {[
          ['Role', 'System administrator'],
          ['Sign-in', `${run.templateName} (provider “${run.providerName}”, enabled)`],
          ['Local password', 'Not set — you can set one later in your profile'],
        ].map(([key, value]) => (
          <div key={key} className="flex gap-3">
            <dt className="w-[120px] shrink-0 font-medium text-fg-muted">{key}</dt>
            <dd className="text-fg">{value}</dd>
          </div>
        ))}
      </dl>
      <Button className="w-full" onClick={onContinue}>
        Continue
      </Button>
    </div>
  );
}

/** Error card with the reason and the ways on. */
export function SsoErrorCard({
  content,
  busy,
  onPrimary,
  onBuiltIn,
}: {
  content: WizardErrorContent;
  busy: boolean;
  onPrimary: () => void;
  onBuiltIn: () => void;
}) {
  const Icon = content.tone === 'error' ? XCircleIcon : content.primary === 'restart' || content.primary === 'restartContainer' ? ClockIcon : MinusCircleIcon;
  return (
    <div className="flex flex-col items-center gap-6" data-testid="sso-error">
      <span
        className={`flex h-14 w-14 items-center justify-center rounded-full ${
          content.tone === 'error' ? 'bg-status-unhealthy-bg text-status-unhealthy' : 'bg-status-degraded-bg text-status-degraded'
        }`}
      >
        <Icon size={28} />
      </span>
      <div className="flex flex-col items-center gap-2 text-center">
        <h2 className="text-xl font-semibold text-fg">{content.title}</h2>
        <p className="text-sm text-fg-secondary">{content.body}</p>
      </div>
      {content.providerStaysDisabled && (
        <Alert tone="info" title="The provider stays set up, but disabled" className="w-full">
          This installation is already connected. The provider stays disabled under Settings › Single Sign-On until you
          test and enable it.
        </Alert>
      )}
      {content.primary === 'restartContainer' ? (
        <div className="rounded-lg bg-raised px-6 py-3 font-mono text-sm text-fg-secondary">docker restart readystackgo</div>
      ) : (
        <div className="flex w-full flex-col gap-3">
          <Button className="w-full" disabled={busy} onClick={onPrimary}>
            {content.primaryLabel}
          </Button>
          {content.builtIn && (
            <Button variant="secondary" className="w-full" disabled={busy} onClick={onBuiltIn}>
              Use built-in sign-in instead
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
