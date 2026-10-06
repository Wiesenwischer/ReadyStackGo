import { apiDelete, apiGet, apiPatch, apiPost } from './client';

// --- Templates ---

export type RegistrationInteraction = 'manualEntry' | 'connect';

export interface IdentityProviderTemplateDto {
  id: string;
  name: string;
  description: string;
  /** Text of the tile in the setup wizard. */
  setupDescription: string;
  /** Icon URL (SVG), or null (the UI shows a key). */
  iconUrl?: string | null;
  hasFixedAuthority: boolean;
  authorityUrl?: string | null;
  authorityHint?: string | null;
  authorityExample?: string | null;
  registrationKind: string;
  interaction: RegistrationInteraction;
  requireHttps: boolean;
  helpUrl?: string | null;
}

export async function getOidcTemplates(): Promise<IdentityProviderTemplateDto[]> {
  return apiGet<IdentityProviderTemplateDto[]>('/api/settings/oidc/templates');
}

// --- Checks and test sign-in ---

export type CheckStatus = 'passed' | 'failed' | 'skipped';

export interface CheckItemDto {
  id: 'discovery' | 'issuer' | 'endpoints' | 'par' | 'client' | string;
  status: CheckStatus;
  title: string;
  detail?: string | null;
  code?: string | null;
}

export interface CheckReportDto {
  items: CheckItemDto[];
  issuer?: string | null;
  executedCount: number;
  failedCount: number;
  passed: boolean;
  /** False when the connection changed after the checks ran. */
  current: boolean;
}

export type TestSignInClaimStatus = 'received' | 'verified' | 'notVerified' | 'missing';

export interface TestSignInClaimDto {
  detail: string;
  claim: string;
  value?: string | null;
  status: TestSignInClaimStatus;
}

export interface TestSignInDto {
  passed: boolean;
  current: boolean;
  at: string;
  signedInAs?: string | null;
  claims: TestSignInClaimDto[];
  warningTitle?: string | null;
  warningBody?: string | null;
  error?: string | null;
  errorDetail?: string | null;
}

// --- Setup sessions ("Add provider", provider page) ---

export interface SetupSessionDto {
  id: string;
  isNew: boolean;
  existingProvider?: string | null;
  template?: IdentityProviderTemplateDto | null;
  templateId: string;
  registrationKind: string;
  interaction: RegistrationInteraction;
  requireHttps: boolean;
  authority?: string | null;
  name: string;
  displayName: string;
  scopes: string;
  baseUrl?: string | null;
  /** True if the base URL of the installation was set before. */
  baseUrlConfigured: boolean;
  redirectUri?: string | null;
  clientId?: string | null;
  hasClientSecret: boolean;
  pairedAt?: string | null;
  pairedBy?: string | null;
  /** Credentials were obtained by a registration in this session (connect or reconnect). */
  registeredInSession: boolean;
  registrationError?: string | null;
  registrationErrorDescription?: string | null;
  /** The pairing came back with a code; complete the registration next. */
  registrationPending: boolean;
  discovery?: CheckReportDto | null;
  checks?: CheckReportDto | null;
  testSignIn?: TestSignInDto | null;
  trustUnverifiedEmail: boolean;
  /** Checks and test sign-in passed for the current connection. */
  canEnable: boolean;
}

export interface RegistrationStartDto {
  kind: 'formPost' | 'redirect' | 'completed';
  url?: string | null;
  fields?: Record<string, string> | null;
}

export interface RegistrationResponse {
  session: SetupSessionDto;
  start?: RegistrationStartDto | null;
}

// POSTs send an empty JSON object: FastEndpoints only matches endpoints with a request DTO
// (the session id from the route) when the request carries a JSON body.
export const setupApi = {
  createForTemplate: (templateId: string) =>
    apiPost<SetupSessionDto>('/api/settings/oidc/setup', { templateId }),
  createForProvider: (provider: string) =>
    apiPost<SetupSessionDto>('/api/settings/oidc/setup', { provider }),
  get: (id: string) => apiGet<SetupSessionDto>(`/api/settings/oidc/setup/${encodeURIComponent(id)}`),
  update: (id: string, changes: { displayName?: string; trustUnverifiedEmail?: boolean }) =>
    apiPatch<SetupSessionDto>(`/api/settings/oidc/setup/${encodeURIComponent(id)}`, changes),
  cancel: (id: string) => apiDelete<void>(`/api/settings/oidc/setup/${encodeURIComponent(id)}`),
  discovery: (id: string, authority: string) =>
    apiPost<SetupSessionDto>(`/api/settings/oidc/setup/${encodeURIComponent(id)}/discovery`, { authority }),
  installation: (id: string, baseUrl: string, name?: string) =>
    apiPost<SetupSessionDto>(`/api/settings/oidc/setup/${encodeURIComponent(id)}/installation`, { baseUrl, name }),
  register: (id: string, credentials?: { clientId?: string; clientSecret?: string; scopes?: string }) =>
    apiPost<RegistrationResponse>(`/api/settings/oidc/setup/${encodeURIComponent(id)}/registration`, credentials ?? {}),
  completeRegistration: (id: string) =>
    apiPost<SetupSessionDto>(`/api/settings/oidc/setup/${encodeURIComponent(id)}/registration/complete`, {}),
  runChecks: (id: string) =>
    apiPost<SetupSessionDto>(`/api/settings/oidc/setup/${encodeURIComponent(id)}/checks`, {}),
  startTestSignIn: (id: string) =>
    apiPost<{ url: string }>(`/api/settings/oidc/setup/${encodeURIComponent(id)}/test-sign-in`, {}),
  save: (id: string, enabled: boolean) =>
    apiPost<{ name: string; enabled: boolean }>(`/api/settings/oidc/setup/${encodeURIComponent(id)}/save`, { enabled }),
};

export async function removeOidcProvider(name: string): Promise<void> {
  return apiDelete<void>(`/api/settings/oidc/providers/${encodeURIComponent(name)}`);
}

/**
 * Continues a registration in the browser: posts the form to the provider (pairing) or
 * navigates there. Leaves the page.
 */
export function followRegistrationStart(start: RegistrationStartDto): void {
  if (!start.url) {
    return;
  }
  if (start.kind === 'redirect') {
    window.location.href = start.url;
    return;
  }
  if (start.kind === 'formPost') {
    const form = document.createElement('form');
    form.method = 'POST';
    form.action = start.url;
    form.style.display = 'none';
    for (const [name, value] of Object.entries(start.fields ?? {})) {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = name;
      input.value = value;
      form.appendChild(input);
    }
    document.body.appendChild(form);
    form.submit();
  }
}

// --- Wizard run with an identity provider ---

export type WizardSsoRunState = 'started' | 'registered' | 'signedIn' | 'failed';

export interface WizardSsoRunDto {
  state: WizardSsoRunState;
  failureReason?: string | null;
  failureDetail?: string | null;
  startedAt: string;
  expiresAt: string;
  templateId: string;
  templateName: string;
  iconUrl?: string | null;
  baseUrl: string;
  providerName?: string | null;
  registrationReturned: boolean;
  signedInUsername?: string | null;
  signedInEmail?: string | null;
  signedInDisplayName?: string | null;
}

export interface WizardSsoStartResponse {
  run: WizardSsoRunDto;
  start?: RegistrationStartDto | null;
  /** Retry continues with the sign-in (navigate to wizardSsoSignInUrl). */
  signIn: boolean;
}

/** Browser navigation target of the wizard sign-in. */
export const wizardSsoSignInUrl = '/api/wizard/sso/sign-in';

export const wizardSsoApi = {
  templates: () => apiGet<IdentityProviderTemplateDto[]>('/api/wizard/sso/templates'),
  start: (templateId: string, baseUrl: string) =>
    apiPost<WizardSsoStartResponse>('/api/wizard/sso/start', { templateId, baseUrl }),
  status: () => apiGet<WizardSsoRunDto>('/api/wizard/sso/status'),
  continue: () => apiPost<WizardSsoRunDto>('/api/wizard/sso/continue'),
  retry: () => apiPost<WizardSsoStartResponse>('/api/wizard/sso/retry'),
  end: () => apiDelete<void>('/api/wizard/sso'),
};
