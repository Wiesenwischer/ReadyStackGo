import { useState, useEffect, useCallback } from 'react';
import { userApi, type UserProfile } from '../api/user';

export interface UseProfileStoreReturn {
  profile: UserProfile | null;
  isLoading: boolean;
  error: string | null;

  // Password change form state
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
  changing: boolean;
  changeError: string | null;
  changeSuccess: string | null;

  // Actions
  setCurrentPassword: (value: string) => void;
  setNewPassword: (value: string) => void;
  setConfirmPassword: (value: string) => void;
  changePassword: () => Promise<void>;

  // Computed
  roleLabel: string;
  canSubmitPasswordChange: boolean;
}

export function useProfileStore(): UseProfileStoreReturn {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [changing, setChanging] = useState(false);
  const [changeError, setChangeError] = useState<string | null>(null);
  const [changeSuccess, setChangeSuccess] = useState<string | null>(null);

  const loadProfile = useCallback(async () => {
    try {
      const data = await userApi.getProfile();
      setProfile(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load profile');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadProfile();
  }, [loadProfile]);

  const changePasswordAction = useCallback(async () => {
    setChangeError(null);
    setChangeSuccess(null);

    if (newPassword !== confirmPassword) {
      setChangeError('New passwords do not match');
      return;
    }

    if (newPassword.length < 8) {
      setChangeError('Password must be at least 8 characters');
      return;
    }

    setChanging(true);
    try {
      const result = await userApi.changePassword({
        currentPassword,
        newPassword,
      });

      if (result.success) {
        setChangeSuccess(result.message ?? 'Password changed successfully');
        setCurrentPassword('');
        setNewPassword('');
        setConfirmPassword('');
        loadProfile();
      } else {
        setChangeError(result.message ?? 'Failed to change password');
      }
    } catch (err) {
      setChangeError(
        err instanceof Error ? err.message : 'Failed to change password'
      );
    } finally {
      setChanging(false);
    }
  }, [currentPassword, newPassword, confirmPassword, loadProfile]);

  const roleLabel = profile?.role === 'admin' ? 'System Administrator' : 'User';
  const canSubmitPasswordChange = !changing && !!currentPassword && !!newPassword && !!confirmPassword;

  return {
    profile,
    isLoading,
    error,
    currentPassword,
    newPassword,
    confirmPassword,
    changing,
    changeError,
    changeSuccess,
    setCurrentPassword,
    setNewPassword,
    setConfirmPassword,
    changePassword: changePasswordAction,
    roleLabel,
    canSubmitPasswordChange,
  };
}
