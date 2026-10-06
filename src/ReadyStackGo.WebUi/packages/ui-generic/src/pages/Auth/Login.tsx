import { useState, useEffect, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { getOidcProviders, startOidcLogin, type OidcProviderDto } from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';
import { useTheme } from '../../context/ThemeContext';
import { Logo } from '../../components/brand/Logo';
import { ProviderButton } from '../../components/sso/SsoComponents';

// Sign-in page (design frames 177:4768, 177:5892): provider buttons show the symbol of their template.

const OIDC_ERROR_MESSAGES: Record<string, string> = {
  oidc_failed: 'Single sign-on failed. Please try again.',
  oidc_state: 'Single sign-on session expired. Please try again.',
  oidc_provider: 'The selected sign-on provider is no longer available.',
  oidc_token: 'Single sign-on could not be completed.',
  oidc_no_account: 'No account exists for this identity. Ask an administrator to invite you.',
  oidc_email_unverified:
    'Your email address is not confirmed at the sign-in provider. Confirm it there, or ask an administrator.',
  oidc_email_missing: 'The sign-in provider sent no email address. Ask an administrator.',
  oidc_subject_mismatch:
    'This account is already linked to a different identity at this provider. Ask an administrator.',
  oidc_account_disabled: 'Your account is disabled.',
  oidc_provider_rejected:
    'The sign-in provider rejected ReadyStackGo. An administrator needs to reconnect it under Settings › Single Sign-On.',
  oidc_unreachable: 'The sign-in provider is not reachable. Try again later or sign in with your password.',
  oidc_email_invalid: 'ReadyStackGo cannot use the email address of this account. Ask an administrator.',
};

export default function Login() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [providers, setProviders] = useState<OidcProviderDto[]>([]);

  const { login } = useAuth();
  const { colorTheme } = useTheme();
  const navigate = useNavigate();

  useEffect(() => {
    // Surface an OIDC error passed back as ?error=... by the callback redirect.
    const params = new URLSearchParams(window.location.search);
    const errorCode = params.get('error');
    if (errorCode) {
      setError(OIDC_ERROR_MESSAGES[errorCode] ?? 'Sign-in failed. Please try again.');
    }

    getOidcProviders()
      .then(setProviders)
      .catch(() => setProviders([]));
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      await login(username, password);
      navigate('/');
    } catch {
      setError('Invalid credentials');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="relative bg-surface p-6 sm:p-0">
      <div className="relative flex h-screen w-full flex-col justify-center lg:flex-row">
        {/* Left side - Login Form */}
        <div className="flex flex-col flex-1">
          <div className="flex flex-col justify-center flex-1 w-full max-w-md mx-auto">
            <div>
              <div className="mb-5 sm:mb-8">
                <h1 className="mb-1.5 text-[28px] font-bold leading-9 text-fg">
                  Sign In to ReadyStackGo
                </h1>
                <p className="text-sm text-fg-secondary">
                  Enter your credentials to manage your Docker stacks
                </p>
              </div>

              <form onSubmit={handleSubmit}>
                <div className="space-y-6">
                  {error && (
                    <div role="alert" className="rounded-xl border border-status-unhealthy/35 bg-status-unhealthy-bg px-4 py-3 text-sm text-fg">
                      {error}
                    </div>
                  )}

                  <div>
                    <label className="mb-1.5 block text-sm font-medium text-fg">
                      Email or username <span className="text-status-unhealthy">*</span>
                    </label>
                    <input
                      type="text"
                      value={username}
                      onChange={(e) => setUsername(e.target.value)}
                      placeholder="admin@example.com"
                      required
                      className="h-11 w-full rounded-[10px] border border-line-strong/55 bg-surface px-3.5 text-sm text-fg outline-none placeholder:text-fg-muted focus:border-primary focus:ring-1 focus:ring-primary"
                    />
                  </div>

                  <div>
                    <label className="mb-1.5 block text-sm font-medium text-fg">
                      Password <span className="text-status-unhealthy">*</span>
                    </label>
                    <div className="relative">
                      <input
                        type={showPassword ? 'text' : 'password'}
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        placeholder="Enter your password"
                        required
                        className="h-11 w-full rounded-[10px] border border-line-strong/55 bg-surface px-3.5 text-sm text-fg outline-none placeholder:text-fg-muted focus:border-primary focus:ring-1 focus:ring-primary"
                      />
                      <button
                        type="button"
                        onClick={() => setShowPassword(!showPassword)}
                        className="absolute z-30 -translate-y-1/2 cursor-pointer right-4 top-1/2"
                      >
                        {showPassword ? (
                          <svg
                            className="w-5 h-5 fill-fg-muted"
                            viewBox="0 0 20 20"
                            fill="currentColor"
                          >
                            <path d="M10 12a2 2 0 100-4 2 2 0 000 4z" />
                            <path fillRule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clipRule="evenodd" />
                          </svg>
                        ) : (
                          <svg
                            className="w-5 h-5 fill-fg-muted"
                            viewBox="0 0 20 20"
                            fill="currentColor"
                          >
                            <path fillRule="evenodd" d="M3.707 2.293a1 1 0 00-1.414 1.414l14 14a1 1 0 001.414-1.414l-1.473-1.473A10.014 10.014 0 0019.542 10C18.268 5.943 14.478 3 10 3a9.958 9.958 0 00-4.512 1.074l-1.78-1.781zm4.261 4.26l1.514 1.515a2.003 2.003 0 012.45 2.45l1.514 1.514a4 4 0 00-5.478-5.478z" clipRule="evenodd" />
                            <path d="M12.454 16.697L9.75 13.992a4 4 0 01-3.742-3.741L2.335 6.578A9.98 9.98 0 00.458 10c1.274 4.057 5.065 7 9.542 7 .847 0 1.669-.105 2.454-.303z" />
                          </svg>
                        )}
                      </button>
                    </div>
                  </div>

                  <div>
                    <button
                      type="submit"
                      disabled={isLoading}
                      className="inline-flex h-11 w-full items-center justify-center rounded-[10px] bg-primary px-7 text-sm font-semibold text-on-primary transition-colors hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus disabled:cursor-not-allowed disabled:opacity-45"
                    >
                      {isLoading ? 'Signing in...' : 'Sign in'}
                    </button>
                  </div>

                  <div className="text-center">
                    <Link to="/forgot-password" className="text-sm font-medium text-fg-brand hover:underline">
                      Forgot password?
                    </Link>
                  </div>
                </div>
              </form>

              {providers.length > 0 && (
                <div className="mt-6">
                  <div className="relative flex items-center justify-center mb-4">
                    <div className="absolute inset-0 flex items-center">
                      <div className="w-full border-t border-line" />
                    </div>
                    <span className="relative bg-surface px-3 text-xs text-fg-muted">
                      or continue with
                    </span>
                  </div>
                  <div className="space-y-3">
                    {providers.map((p) => (
                      <ProviderButton
                        key={p.name}
                        label={`Sign in with ${p.displayName}`}
                        iconUrl={p.iconUrl}
                        onClick={() => startOidcLogin(p.name)}
                        className="w-full"
                        testId={`sign-in-with-${p.name}`}
                      />
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Right side - branding on the dark navigation color of the theme */}
        <div data-theme={colorTheme ?? undefined} data-mode="dark" className="hidden h-full w-full items-center bg-nav lg:grid lg:w-1/2">
          <div className="relative z-1 flex items-center justify-center">
            <div className="relative flex max-w-xs flex-col items-center gap-4">
              <Logo context="nav" height={36} />
              <p className="text-center text-sm text-fg-secondary">
                Deploy and manage Docker stacks with ease. A modern, lightweight platform for container orchestration.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
