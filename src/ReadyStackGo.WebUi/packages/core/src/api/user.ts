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
}

export const userApi = {
  getProfile: () => apiGet<UserProfile>('/api/user/profile'),
  changePassword: (request: ChangePasswordRequest) =>
    apiPost<ChangePasswordResponse>('/api/user/change-password', request),
  getExternalIdentities: () => apiGet<ExternalIdentityDto[]>('/api/user/external-identities'),
  unlinkExternalIdentity: (provider: string) =>
    apiDelete<void>(`/api/user/external-identities/${encodeURIComponent(provider)}`),
};
