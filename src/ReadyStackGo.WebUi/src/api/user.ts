import { apiGet, apiPost } from './client';

export interface UserProfile {
  username: string;
  role: string;
  createdAt: string;
  passwordChangedAt?: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ChangePasswordResponse {
  success: boolean;
  message?: string;
}

export const userApi = {
  getProfile: () => apiGet<UserProfile>('/api/user/profile'),
  changePassword: (request: ChangePasswordRequest) =>
    apiPost<ChangePasswordResponse>('/api/user/change-password', request),
};
