import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import OnboardingLayout from './OnboardingLayout';
import OnboardingOrgStep from './OnboardingOrgStep';
import OnboardingEnvStep from './OnboardingEnvStep';
import OnboardingSourcesStep from './OnboardingSourcesStep';
import OnboardingCompleteStep from './OnboardingCompleteStep';
import RegistriesStep from '../Wizard/RegistriesStep';
import { useOnboardingStore, type RegistryInputDto } from '@rsgo/core';

const TOTAL_STEPS = 5; // org, env, sources, registries, complete

export default function Onboarding() {
  const store = useOnboardingStore();
  const navigate = useNavigate();

  // Redirect to dashboard if org already configured
  useEffect(() => {
    if (!store.isLoading && store.isOrgAlreadyDone) {
      navigate('/', { replace: true });
    }
  }, [store.isLoading, store.isOrgAlreadyDone, navigate]);

  if (store.isLoading) {
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
    await store.submitRegistries(registries);
  };

  const renderStep = () => {
    switch (store.step) {
      case 1:
        return <OnboardingOrgStep onNext={() => store.goToStep(2)} />;
      case 2:
        return (
          <OnboardingEnvStep
            onNext={() => { store.markEnvConfigured(); store.goToStep(3); }}
            onSkip={() => store.goToStep(3)}
          />
        );
      case 3:
        return (
          <OnboardingSourcesStep
            onNext={() => { store.markSourcesConfigured(); store.goToStep(4); }}
            onSkip={() => store.goToStep(4)}
          />
        );
      case 4:
        return (
          <RegistriesStep onNext={handleRegistriesNext} onSkip={() => store.goToStep(5)} />
        );
      case 5:
        return (
          <OnboardingCompleteStep
            configuredOrg={true}
            configuredEnv={store.configuredEnv}
            configuredSources={store.configuredSources}
            configuredRegistries={store.configuredRegistries}
          />
        );
      default:
        return null;
    }
  };

  return (
    <OnboardingLayout step={store.step} totalSteps={TOTAL_STEPS}>
      {renderStep()}
    </OnboardingLayout>
  );
}
