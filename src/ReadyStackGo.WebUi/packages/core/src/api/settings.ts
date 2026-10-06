import { apiGet, apiPost, apiPut } from './client';

// --- SMTP settings ---

export interface SmtpSettingsDto {
  enabled: boolean;
  host: string;
  port: number;
  useStartTls: boolean;
  username?: string;
  fromAddress: string;
  fromName: string;
  /** Write-only. Empty keeps the stored password. Always null on read. */
  password?: string;
  /** Read-only. True if a password is stored. */
  hasPassword: boolean;
}

export interface EmailSendResult {
  success: boolean;
  error?: string;
}

export async function getSmtpSettings(): Promise<SmtpSettingsDto> {
  return apiGet<SmtpSettingsDto>('/api/settings/smtp');
}

export async function saveSmtpSettings(settings: SmtpSettingsDto): Promise<void> {
  return apiPut<void>('/api/settings/smtp', settings);
}

export async function testSmtpSettings(settings: SmtpSettingsDto & { toAddress: string }): Promise<EmailSendResult> {
  return apiPost<EmailSendResult>('/api/settings/smtp/test', settings);
}

// --- OIDC settings ---

export interface OidcLastResultDto {
  at: string;
  kind: 'checks' | 'testSignIn' | 'signIn' | string;
  passed: boolean;
  message?: string | null;
}

/** OIDC provider as listed under Settings › Single Sign-On. Secrets are never returned. */
export interface OidcProviderSettingsDto {
  name: string;
  displayName: string;
  authority: string;
  clientId: string;
  /** Read-only. True if a secret is stored. */
  hasClientSecret: boolean;
  scopes: string;
  enabled: boolean;
  template: string;
  templateName: string;
  iconUrl?: string | null;
  registration: string;
  isPaired: boolean;
  pairedAt?: string | null;
  pairedBy?: string | null;
  trustUnverifiedEmail: boolean;
  reconnectNeeded: boolean;
  lastResult?: OidcLastResultDto | null;
  hasPassedTestSignIn: boolean;
  /** The current user signs in only through this provider (cannot disable or remove it). */
  isOnlySignInOfCurrentUser: boolean;
}

export interface OidcSettingsDto {
  providers: OidcProviderSettingsDto[];
  /** True while no system administrator has a local password. */
  noAdminWithPassword: boolean;
  systemAdminCount: number;
}

export async function getOidcSettings(): Promise<OidcSettingsDto> {
  return apiGet<OidcSettingsDto>('/api/settings/oidc');
}
