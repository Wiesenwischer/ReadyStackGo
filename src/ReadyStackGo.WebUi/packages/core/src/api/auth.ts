import { apiGet, apiPost } from './client';

// --- OIDC providers (for login buttons) ---

export interface OidcProviderDto {
  name: string;
  displayName: string;
}

export async function getOidcProviders(): Promise<OidcProviderDto[]> {
  return apiGet<OidcProviderDto[]>('/api/auth/oidc/providers');
}

/** Full-page redirect to the provider's authorization endpoint. */
export function startOidcLogin(provider: string): void {
  window.location.href = `/api/auth/oidc/${encodeURIComponent(provider)}/challenge`;
}

// --- Invitations ---

export interface InvitationInfoResponse {
  valid: boolean;
  email?: string;
  roleId?: string;
  scopeType?: string;
}

export async function getInvitationInfo(token: string): Promise<InvitationInfoResponse> {
  return apiGet<InvitationInfoResponse>(`/api/auth/invitation?token=${encodeURIComponent(token)}`);
}

export interface AcceptInvitationRequest {
  token: string;
  password: string;
  username?: string;
}

export interface AcceptInvitationResponse {
  success: boolean;
  token?: string;
  username?: string;
  message?: string;
}

export async function acceptInvitation(request: AcceptInvitationRequest): Promise<AcceptInvitationResponse> {
  return apiPost<AcceptInvitationResponse>('/api/auth/accept-invitation', request);
}

// --- Email verification ---

export interface SimpleResult {
  success: boolean;
  message?: string;
}

export async function verifyEmail(token: string): Promise<SimpleResult> {
  return apiPost<SimpleResult>('/api/auth/verify-email', { token });
}

export async function requestEmailVerification(): Promise<SimpleResult> {
  return apiPost<SimpleResult>('/api/auth/request-email-verification');
}

// --- Password reset ---

export async function requestPasswordReset(identifier: string): Promise<SimpleResult> {
  return apiPost<SimpleResult>('/api/auth/request-password-reset', { identifier });
}

export async function resetPassword(token: string, newPassword: string): Promise<SimpleResult> {
  return apiPost<SimpleResult>('/api/auth/reset-password', { token, newPassword });
}

/**
 * Decodes the username and role from a ReadyStackGo JWT (used to seed auth state after an
 * OIDC login or invitation acceptance, where the server returns only the token).
 */
export function decodeAuthFromToken(token: string): { username: string; role: string } {
  try {
    const payloadSegment = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const payload = JSON.parse(atob(payloadSegment)) as Record<string, unknown>;
    const username = (payload.sub as string) ?? '';
    // TokenService writes a legacy role claim under the WS-Identity URI.
    const role =
      (payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] as string) ??
      (payload.role as string) ??
      'user';
    return { username, role };
  } catch {
    return { username: '', role: 'user' };
  }
}
