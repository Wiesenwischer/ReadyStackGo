import { useState, useEffect, useCallback } from 'react';
import { systemApi } from '../api/system';

export interface UseSystemSettingsStoreReturn {
  // Health notification settings
  cooldownMinutes: number;
  cooldownLoading: boolean;
  cooldownSaving: boolean;
  cooldownSuccess: string | null;
  cooldownError: string | null;
  setCooldownMinutes: (value: number) => void;
  saveCooldown: () => Promise<void>;
}

export function useSystemSettingsStore(): UseSystemSettingsStoreReturn {
  const [cooldownMinutes, setCooldownMinutes] = useState(5);
  const [cooldownLoading, setCooldownLoading] = useState(true);
  const [cooldownSaving, setCooldownSaving] = useState(false);
  const [cooldownSuccess, setCooldownSuccess] = useState<string | null>(null);
  const [cooldownError, setCooldownError] = useState<string | null>(null);

  useEffect(() => {
    systemApi.getHealthNotificationSettings()
      .then((settings) => setCooldownMinutes(Math.round(settings.cooldownSeconds / 60)))
      .catch(() => setCooldownError('Failed to load health notification settings'))
      .finally(() => setCooldownLoading(false));
  }, []);

  const saveCooldown = useCallback(async () => {
    setCooldownSaving(true);
    setCooldownSuccess(null);
    setCooldownError(null);
    try {
      const response = await systemApi.updateHealthNotificationSettings({
        cooldownSeconds: cooldownMinutes * 60,
      });
      if (response.success) {
        setCooldownSuccess(response.message ?? 'Settings saved.');
        setTimeout(() => setCooldownSuccess(null), 5000);
      } else {
        setCooldownError(response.message ?? 'Failed to save settings.');
      }
    } catch {
      setCooldownError('Failed to save health notification settings.');
    } finally {
      setCooldownSaving(false);
    }
  }, [cooldownMinutes]);

  return {
    cooldownMinutes,
    cooldownLoading,
    cooldownSaving,
    cooldownSuccess,
    cooldownError,
    setCooldownMinutes,
    saveCooldown,
  };
}
