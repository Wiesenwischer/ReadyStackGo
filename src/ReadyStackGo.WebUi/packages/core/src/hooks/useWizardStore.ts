import { useState, useEffect, useCallback } from 'react';
import {
  getWizardStatus,
  createAdmin,
  installStack,
  type WizardTimeoutInfo,
  type CreateAdminRequest,
  type CreateAdminResponse,
} from '../api/wizard';

export interface UseWizardStoreReturn {
  isLoading: boolean;
  timeout: WizardTimeoutInfo | null;
  isTimedOut: boolean;
  isLocked: boolean;
  reloadState: () => Promise<void>;
  submitAdmin: (data: CreateAdminRequest) => Promise<CreateAdminResponse>;
  completeWizard: () => Promise<void>;
  handleTimeout: () => void;
  checkIsCompleted: () => Promise<boolean>;
}

export function useWizardStore(): UseWizardStoreReturn {
  const [isLoading, setIsLoading] = useState(true);
  const [timeout, setTimeout] = useState<WizardTimeoutInfo | null>(null);
  const [isTimedOut, setIsTimedOut] = useState(false);
  const [isLocked, setIsLocked] = useState(false);

  const reloadState = useCallback(async () => {
    setIsLoading(true);
    setIsTimedOut(false);
    setIsLocked(false);
    try {
      const status = await getWizardStatus();
      setTimeout(status.timeout ?? null);

      if (status.timeout?.isLocked) {
        setIsLocked(true);
        setIsTimedOut(true);
        return;
      }
      if (status.timeout?.isTimedOut) {
        setIsTimedOut(true);
        return;
      }

      if (status.isCompleted) {
        return;
      }
    } catch (error) {
      console.error('Failed to load wizard state:', error);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    reloadState();
  }, [reloadState]);

  const handleTimeout = useCallback(() => {
    setIsTimedOut(true);
  }, []);

  const submitAdmin = useCallback(async (data: CreateAdminRequest): Promise<CreateAdminResponse> => {
    return createAdmin(data);
  }, []);

  const completeWizard = useCallback(async () => {
    await installStack();
  }, []);

  const checkIsCompleted = useCallback(async (): Promise<boolean> => {
    try {
      const status = await getWizardStatus();
      return status.isCompleted;
    } catch {
      return false;
    }
  }, []);

  return {
    isLoading,
    timeout,
    isTimedOut,
    isLocked,
    reloadState,
    submitAdmin,
    completeWizard,
    handleTimeout,
    checkIsCompleted,
  };
}
