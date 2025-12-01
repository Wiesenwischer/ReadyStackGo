import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import WizardLayout from './WizardLayout';
import AdminStep from './AdminStep';
import OrganizationStep from './OrganizationStep';
import EnvironmentStep from './EnvironmentStep';
import InstallStep from './InstallStep';
import { createAdmin, setOrganization, installStack, getWizardStatus, type WizardTimeoutInfo } from '../../api/wizard';
import { createEnvironment, setDefaultEnvironment } from '../../api/environments';

export default function Wizard() {
  const [currentStep, setCurrentStep] = useState(1);
  const [isLoading, setIsLoading] = useState(true);
  const [timeout, setTimeout] = useState<WizardTimeoutInfo | null>(null);
  const [isTimedOut, setIsTimedOut] = useState(false);
  const navigate = useNavigate();

  // Handle wizard timeout - show timeout message
  const handleTimeout = useCallback(() => {
    setIsTimedOut(true);
  }, []);

  // Reload wizard state (after timeout or refresh)
  const reloadWizardState = useCallback(async () => {
    setIsLoading(true);
    setIsTimedOut(false);
    try {
      const status = await getWizardStatus();
      setTimeout(status.timeout ?? null);

      // Check if timed out on server side
      if (status.timeout?.isTimedOut) {
        setIsTimedOut(true);
        return;
      }

      // Map backend wizard state to frontend step number
      // v0.4.1: Added optional Environment step (4 steps total)
      // Admin -> Organization -> Environment (optional) -> Install
      switch (status.wizardState) {
        case 'NotStarted':
          setCurrentStep(1);
          break;
        case 'AdminCreated':
          setCurrentStep(2);
          break;
        case 'OrganizationSet':
          setCurrentStep(3);
          break;
        case 'Installed':
          // Wizard completed, redirect to login
          navigate('/login');
          return;
        default:
          setCurrentStep(1);
      }
    } catch (error) {
      console.error('Failed to load wizard state:', error);
      setCurrentStep(1);
    } finally {
      setIsLoading(false);
    }
  }, [navigate]);

  // Load wizard state on mount
  useEffect(() => {
    reloadWizardState();
  }, [reloadWizardState]);

  const handleAdminNext = async (data: { username: string; password: string }) => {
    await createAdmin(data);
    setCurrentStep(2);
  };

  const handleOrganizationNext = async (data: { id: string; name: string }) => {
    await setOrganization(data);
    setCurrentStep(3);
  };

  const handleEnvironmentNext = async (data: { name: string; socketPath: string } | null) => {
    if (data) {
      // User wants to create an environment
      const response = await createEnvironment({ name: data.name, socketPath: data.socketPath });
      if (!response.success) {
        throw new Error(response.message || 'Failed to create environment');
      }
      // Set it as default using the ID from the response (server generates IDs)
      const environmentId = response.environment?.id;
      if (environmentId) {
        await setDefaultEnvironment(environmentId);
      }
    }
    // Move to install step (whether environment was created or skipped)
    setCurrentStep(4);
  };

  const handleInstall = async () => {
    const result = await installStack();
    if (result.success) {
      // Installation successful, redirect to login
      navigate('/login');
    } else {
      throw new Error(result.errors.join(', ') || 'Installation failed');
    }
  };

  // Show loading state while checking wizard status
  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-white dark:bg-gray-900">
        <div className="text-center">
          <svg className="animate-spin h-12 w-12 mx-auto text-brand-600 dark:text-brand-400 mb-4" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <p className="text-gray-600 dark:text-gray-400">Loading wizard...</p>
        </div>
      </div>
    );
  }

  // Show timeout message
  if (isTimedOut) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-white dark:bg-gray-900">
        <div className="text-center max-w-md p-8">
          <div className="mb-6">
            <svg className="w-16 h-16 mx-auto text-amber-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <h2 className="text-2xl font-bold text-gray-800 dark:text-white mb-4">
            Setup Window Expired
          </h2>
          <p className="text-gray-600 dark:text-gray-400 mb-6">
            The 5-minute setup window has expired. Any partial configuration has been reset.
            Click the button below to start a new setup window.
          </p>
          <button
            onClick={reloadWizardState}
            className="px-6 py-3 bg-brand-600 text-white font-medium rounded-lg hover:bg-brand-700 transition-colors"
          >
            Start New Setup
          </button>
        </div>
      </div>
    );
  }

  return (
    <WizardLayout
      currentStep={currentStep}
      totalSteps={4}
      timeout={timeout}
      onTimeout={handleTimeout}
    >
      {currentStep === 1 && <AdminStep onNext={handleAdminNext} />}
      {currentStep === 2 && <OrganizationStep onNext={handleOrganizationNext} />}
      {currentStep === 3 && <EnvironmentStep onNext={handleEnvironmentNext} />}
      {currentStep === 4 && <InstallStep onInstall={handleInstall} />}
    </WizardLayout>
  );
}
