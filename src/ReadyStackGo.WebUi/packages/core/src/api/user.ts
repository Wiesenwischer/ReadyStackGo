import { apiGet, apiPost, apiDelete } from './client';

export interface UserProfile {
  username: string;
  email: string;
  role: string;
  createdAt: string;
  passwordChangedAt?: string;
  emailVerified: boolean;
  /** Whether SMTP is configured (so the "verify your email" prompt can be sent). */
  smtpEnabled: boolean;
  /** The user has a local password (accounts created through an identity provider may not). */
  hasPassword: boolean;
  /** A system administrator without a local password while no system administrator has one. */
  noAdminWithPassword: boolean;
  systemAdminCount: number;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ChangePasswordResponse {
  success: boolean;
  message?: string;
}

export interface ExternalIdentityDto {
  provider: string;
  linkedAt: string;
  /** Display name of the provider. */
  displayName: string;
  /** Icon of the provider's template, or null. */
  iconUrl?: string | null;
}

export const userApi = {
  getProfile: () => apiGet<UserProfile>('/api/user/profile'),
  changePassword: (request: ChangePasswordRequest) =>
    apiPost<ChangePasswordResponse>('/api/user/change-password', request),
  /** Sets a local password for a user that has none (409 if one exists). */
  setPassword: (newPassword: string) => apiPost<void>('/api/user/set-password', { newPassword }),
  getExternalIdentities: () => apiGet<ExternalIdentityDto[]>('/api/user/external-identities'),
  unlinkExternalIdentity: (provider: string) =>
    apiDelete<void>(`/api/user/external-identities/${encodeURIComponent(provider)}`),
};
