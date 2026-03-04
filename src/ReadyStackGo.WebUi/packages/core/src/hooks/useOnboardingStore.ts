import { useState, useEffect, useCallback } from 'react';
import {
  getOnboardingStatus,
  type OnboardingStatusResponse,
} from '../api/onboarding';
import {
  setRegistries,
  type RegistryInputDto,
} from '../api/wizard';

export interface UseOnboardingStoreReturn {
  step: number;
  isLoading: boolean;
  configuredEnv: boolean;
  configuredSources: boolean;
  configuredRegistries: boolean;
  isOrgAlreadyDone: boolean;
  goToStep: (step: number) => void;
  markEnvConfigured: () => void;
  markSourcesConfigured: () => void;
  submitRegistries: (registries: RegistryInputDto[]) => Promise<void>;
  checkIsOrgDone: () => Promise<boolean>;
}

export function useOnboardingStore(): UseOnboardingStoreReturn {
  const [step, setStep] = useState(1);
  const [isLoading, setIsLoading] = useState(true);
  const [configuredEnv, setConfiguredEnv] = useState(false);
  const [configuredSources, setConfiguredSources] = useState(false);
  const [configuredRegistries, setConfiguredRegistries] = useState(false);
  const [isOrgAlreadyDone, setIsOrgAlreadyDone] = useState(false);

  useEffect(() => {
    const checkStatus = async () => {
      try {
        const status: OnboardingStatusResponse = await getOnboardingStatus();
        if (status.organization.done) {
          setIsOrgAlreadyDone(true);
        }
      } catch {
        // Continue with onboarding on error
      } finally {
        setIsLoading(false);
      }
    };
    checkStatus();
  }, []);

  const goToStep = useCallback((nextStep: number) => {
    setStep(nextStep);
  }, []);

  const markEnvConfigured = useCallback(() => {
    setConfiguredEnv(true);
  }, []);

  const markSourcesConfigured = useCallback(() => {
    setConfiguredSources(true);
  }, []);

  const submitRegistries = useCallback(async (registries: RegistryInputDto[]) => {
    if (registries.length > 0) {
      await setRegistries({ registries });
    }
    setConfiguredRegistries(true);
    setStep(5);
  }, []);

  const checkIsOrgDone = useCallback(async (): Promise<boolean> => {
    try {
      const status = await getOnboardingStatus();
      return status.organization.done;
    } catch {
      // On error, let through — don't block the app
      return true;
    }
  }, []);

  return {
    step,
    isLoading,
    configuredEnv,
    configuredSources,
    configuredRegistries,
    isOrgAlreadyDone,
    goToStep,
    markEnvConfigured,
    markSourcesConfigured,
    submitRegistries,
    checkIsOrgDone,
  };
}
