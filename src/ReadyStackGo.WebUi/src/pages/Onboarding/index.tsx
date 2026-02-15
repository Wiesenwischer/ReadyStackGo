import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import OnboardingLayout from './OnboardingLayout';
import OnboardingOrgStep from './OnboardingOrgStep';
import OnboardingEnvStep from './OnboardingEnvStep';
import OnboardingSourcesStep from './OnboardingSourcesStep';
import OnboardingCompleteStep from './OnboardingCompleteStep';
import RegistriesStep from '../Wizard/RegistriesStep';
import { getOnboardingStatus } from '../../api/onboarding';
import { setRegistries } from '../../api/wizard';
import type { RegistryInputDto } from '../../api/wizard';

const TOTAL_STEPS = 5; // org, env, sources, registries, complete

export default function Onboarding() {
  const [step, setStep] = useState(1);
  const [isLoading, setIsLoading] = useState(true);
  const [configuredEnv, setConfiguredEnv] = useState(false);
  const [configuredSources, setConfiguredSources] = useState(false);
  const [configuredRegistries, setConfiguredRegistries] = useState(false);
  const navigate = useNavigate();

  // Check if onboarding is already complete (org exists) → redirect to dashboard
  const checkStatus = useCallback(async () => {
    try {
      const status = await getOnboardingStatus();
      if (status.organization.done) {
        // Org already configured — redirect to app
        navigate('/', { replace: true });
        return;
      }
    } catch {
      // Continue with onboarding on error
    } finally {
      setIsLoading(false);
    }
  }, [navigate]);

  useEffect(() => {
    checkStatus();
  }, [checkStatus]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-white dark:bg-gray-900">
        <div className="text-center">
          <svg className="animate-spin h-12 w-12 mx-auto text-brand-600 dark:text-brand-400 mb-4" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <p className="text-gray-600 dark:text-gray-400">Loading...</p>
        </div>
      </div>
    );
  }

  const handleRegistriesNext = async (registries: RegistryInputDto[]) => {
    if (registries.length > 0) {
      await setRegistries({ registries });
    }
    setConfiguredRegistries(true);
    setStep(5);
  };

  const renderStep = () => {
    switch (step) {
      case 1:
        return <OnboardingOrgStep onNext={() => setStep(2)} />;
      case 2:
        return (
          <OnboardingEnvStep
            onNext={() => { setConfiguredEnv(true); setStep(3); }}
            onSkip={() => setStep(3)}
          />
        );
      case 3:
        return (
          <OnboardingSourcesStep
            onNext={() => { setConfiguredSources(true); setStep(4); }}
            onSkip={() => setStep(4)}
          />
        );
      case 4:
        return (
          <RegistriesStep onNext={handleRegistriesNext} onSkip={() => setStep(5)} />
        );
      case 5:
        return (
          <OnboardingCompleteStep
            configuredOrg={true}
            configuredEnv={configuredEnv}
            configuredSources={configuredSources}
            configuredRegistries={configuredRegistries}
          />
        );
      default:
        return null;
    }
  };

  return (
    <OnboardingLayout step={step} totalSteps={TOTAL_STEPS}>
      {renderStep()}
    </OnboardingLayout>
  );
}
