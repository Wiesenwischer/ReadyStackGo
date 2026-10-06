import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import WizardLayout from './WizardLayout';
import AdminStep from './AdminStep';
import SmtpStep from './SmtpStep';
import SignInMethodStep from './SignInMethodStep';
import { SsoAddressStep, SsoErrorCard, SsoSignedInCard, SsoWaitingCard } from './SsoAdminStep';
import {
  BUILT_IN,
  currentStepIndex,
  errorContent,
  nextRunAction,
  showCountdown,
  viewForRun,
  wizardSteps,
  type WizardView,
} from './wizardFlow';
import { suggestBaseUrl } from '../../components/sso/baseUrl';
import {
  ApiError,
  decodeAuthFromToken,
  followRegistrationStart,
  useWizardStore,
  wizardSsoApi,
  wizardSsoSignInUrl,
  type IdentityProviderTemplateDto,
  type WizardSsoRunDto,
} from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';

const SUBTITLE: Record<WizardView, (name: string) => string> = {
  method: () => 'Choose how the first administrator signs in',
  admin: () => 'Create your admin account to get started',
  ssoAddress: (name) => `Sign in with ${name}`,
  ssoWaiting: (name) => `Sign in with ${name}`,
  ssoSignedIn: (name) => `Sign in with ${name}`,
  ssoError: (name) => `Sign in with ${name}`,
  smtp: () => 'Configure email for your installation',
};

export default function Wizard() {
  const { isLoading, timeout, isTimedOut, isLocked, reloadState, submitAdmin, completeWizard, handleTimeout } =
    useWizardStore();
  const navigate = useNavigate();
  const { setAuthDirectly } = useAuth();

  const [view, setView] = useState<WizardView | null>(null);
  const [templates, setTemplates] = useState<IdentityProviderTemplateDto[]>([]);
  const [selection, setSelection] = useState<string | null>(null);
  const [run, setRun] = useState<WizardSsoRunDto | null>(null);
  const [failure, setFailure] = useState<{ reason: string; detail?: string | null } | null>(null);
  const [busy, setBusy] = useState(false);
  const [startError, setStartError] = useState<string | null>(null);
  const handled = useRef(false);

  const hasMethodStep = templates.length > 0;
  const template = templates.find((t) => t.id === (run?.templateId ?? selection)) ?? templates[0];
  const templateName = run?.templateName ?? template?.name ?? '';

  /** Continues a run without user interaction: redeem the pairing code, then sign in. */
  const advance = useCallback(async (current: WizardSsoRunDto) => {
    let next = current;
    if (nextRunAction(next) === 'continue') {
      try {
        next = await wizardSsoApi.continue();
        setRun(next);
      } catch (err) {
        setFailure({ reason: 'registration_failed', detail: err instanceof Error ? err.message : null });
        setView('ssoError');
        return;
      }
    }
    if (nextRunAction(next) === 'signIn') {
      window.location.href = wizardSsoSignInUrl;
      return;
    }
    setView(viewForRun(next));
  }, []);

  // Initial state: offered templates, a run of this browser (after the return from the provider)
  // and the token of a finished sign-in in the URL fragment.
  useEffect(() => {
    if (handled.current) return;
    handled.current = true;

    const params = new URLSearchParams(window.location.search);
    const ssoReturn = params.get('sso');
    const tokenMatch = window.location.hash.match(/token=([^&]+)/);
    if (tokenMatch) {
      const token = decodeURIComponent(tokenMatch[1]);
      const { username, role } = decodeAuthFromToken(token);
      setAuthDirectly(token, username, role);
    }
    if (ssoReturn || tokenMatch) {
      window.history.replaceState(null, '', '/wizard');
    }

    (async () => {
      const offered = await wizardSsoApi.templates().catch(() => [] as IdentityProviderTemplateDto[]);
      setTemplates(offered);

      let current: WizardSsoRunDto | null = null;
      try {
        current = await wizardSsoApi.status();
      } catch {
        current = null;
      }

      if (current) {
        setRun(current);
        setSelection(current.templateId);
        await advance(current);
        return;
      }

      if (ssoReturn === 'foreign' || ssoReturn === 'returned') {
        setFailure({ reason: 'foreign' });
        setView('ssoError');
        return;
      }
      setView(offered.length > 0 ? 'method' : 'admin');
    })();
  }, [advance, setAuthDirectly]);

  const handleAdminCreated = async (data: { username: string; email: string; password: string }) => {
    const response = await submitAdmin(data);
    if (response.token && response.username && response.role) {
      setAuthDirectly(response.token, response.username, response.role);
    }
    // The admin is now authenticated; offer the optional SMTP step before completing.
    setView('smtp');
  };

  const completeAndContinue = async () => {
    // Mark wizard as installed (completes the wizard state machine)
    await completeWizard();
    // Redirect to dashboard where the onboarding checklist will guide further setup
    navigate('/', { replace: true });
  };

  const chooseMethod = (choice: string) => {
    setSelection(choice);
    setStartError(null);
    setView(choice === BUILT_IN ? 'admin' : 'ssoAddress');
  };

  const connect = async (baseUrl: string) => {
    if (!template) return;
    setBusy(true);
    setStartError(null);
    try {
      const result = await wizardSsoApi.start(template.id, baseUrl);
      setRun(result.run);
      if (result.start) followRegistrationStart(result.start);
    } catch (err) {
      setBusy(false);
      if (err instanceof ApiError && err.code === 'unreachable') {
        setFailure({ reason: 'unreachable', detail: null });
        setView('ssoError');
      } else if (err instanceof ApiError && err.code === 'completed_elsewhere') {
        setFailure({ reason: 'completed_elsewhere' });
        setView('ssoError');
      } else if (err instanceof ApiError && err.status === 403) {
        handleTimeout();
      } else {
        setStartError(err instanceof Error ? err.message : 'The connection could not be started.');
      }
    }
  };

  const switchToBuiltIn = async () => {
    await wizardSsoApi.end().catch(() => undefined);
    setRun(null);
    setFailure(null);
    setSelection(BUILT_IN);
    setView('admin');
  };

  const windowOpen = !isTimedOut && !isLocked;
  const reason = run?.state === 'failed' ? run.failureReason : failure?.reason;
  const content = errorContent(reason, {
    templateName,
    authority: template?.authorityUrl,
    detail: run?.failureDetail ?? failure?.detail,
    windowOpen,
    providerSaved: !!run?.providerName,
  });

  const onErrorPrimary = async () => {
    setBusy(true);
    try {
      switch (content.primary) {
        case 'retry': {
          if (!run) {
            setFailure(null);
            setView('ssoAddress');
            return;
          }
          const result = await wizardSsoApi.retry();
          setRun(result.run);
          if (result.start) {
            followRegistrationStart(result.start);
            return;
          }
          if (result.signIn) {
            window.location.href = wizardSsoSignInUrl;
            return;
          }
          setView(viewForRun(result.run));
          return;
        }
        case 'restart':
          await wizardSsoApi.end().catch(() => undefined);
          setRun(null);
          setFailure(null);
          await reloadState();
          setView('ssoAddress');
          return;
        case 'signIn':
          navigate('/login', { replace: true });
          return;
      }
    } catch (err) {
      setFailure({ reason: 'registration_failed', detail: err instanceof Error ? err.message : null });
      setView('ssoError');
    } finally {
      setBusy(false);
    }
  };

  // A run in progress continues even after the setup window ran out (E20).
  const runKeepsWizardOpen = view === 'ssoWaiting' || view === 'ssoSignedIn' || view === 'smtp' || view === 'ssoError';

  if (isLoading || view === null) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-page">
        <div className="text-center">
          <svg className="mx-auto mb-4 h-12 w-12 animate-spin text-primary" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <p className="text-fg-secondary">Loading...</p>
        </div>
      </div>
    );
  }

  // Show timeout/locked message
  if (isTimedOut && !runKeepsWizardOpen) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-page">
        <div className="max-w-md p-8 text-center">
          <div className="mb-6">
            <svg className={`mx-auto h-16 w-16 ${isLocked ? 'text-status-unhealthy' : 'text-status-degraded'}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
              {isLocked ? (
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
              ) : (
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
              )}
            </svg>
          </div>
          <h2 className="mb-4 text-2xl font-bold text-fg">{isLocked ? 'Setup Locked' : 'Setup Window Expired'}</h2>
          <p className="mb-6 text-fg-secondary">
            {isLocked ? (
              <>
                The 5-minute setup window has expired and the wizard is now locked.
                <br />
                <br />
                <strong>To try again, restart the container.</strong>
              </>
            ) : (
              'The 5-minute setup window has expired.'
            )}
          </p>
          {isLocked ? (
            <div className="rounded-lg bg-raised px-6 py-3 font-mono text-sm text-fg-secondary">docker restart readystackgo</div>
          ) : (
            <button
              onClick={reloadState}
              className="rounded-[10px] bg-primary px-6 py-3 font-semibold text-on-primary transition-colors hover:bg-primary-hover"
            >
              Refresh Status
            </button>
          )}
        </div>
      </div>
    );
  }

  return (
    <WizardLayout
      subtitle={SUBTITLE[view](templateName)}
      timeout={timeout}
      onTimeout={handleTimeout}
      showCountdown={showCountdown(view)}
      steps={wizardSteps(hasMethodStep)}
      currentStep={currentStepIndex(view, hasMethodStep)}
    >
      {view === 'method' && <SignInMethodStep templates={templates} initialSelection={selection} onContinue={chooseMethod} />}
      {view === 'admin' && <AdminStep onNext={handleAdminCreated} />}
      {view === 'ssoAddress' && template && (
        <SsoAddressStep
          template={template}
          initialBaseUrl={run?.baseUrl ?? suggestBaseUrl(window.location.origin)}
          busy={busy}
          error={startError}
          onConnect={connect}
          onBack={() => setView('method')}
          onBuiltIn={switchToBuiltIn}
        />
      )}
      {view === 'ssoWaiting' && run && <SsoWaitingCard run={run} />}
      {view === 'ssoSignedIn' && run && <SsoSignedInCard run={run} onContinue={() => setView('smtp')} />}
      {view === 'ssoError' && <SsoErrorCard content={content} busy={busy} onPrimary={onErrorPrimary} onBuiltIn={switchToBuiltIn} />}
      {view === 'smtp' && <SmtpStep onComplete={completeAndContinue} />}
    </WizardLayout>
  );
}
