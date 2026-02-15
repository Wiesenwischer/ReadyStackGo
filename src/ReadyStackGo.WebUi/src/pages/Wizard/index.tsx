import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import WizardLayout from './WizardLayout';
import AdminStep from './AdminStep';
import { createAdmin, installStack, getWizardStatus, type WizardTimeoutInfo } from '../../api/wizard';
import { useAuth } from '../../context/AuthContext';

export default function Wizard() {
  const [isLoading, setIsLoading] = useState(true);
  const [timeout, setTimeout] = useState<WizardTimeoutInfo | null>(null);
  const [isTimedOut, setIsTimedOut] = useState(false);
  const [isLocked, setIsLocked] = useState(false);
  const navigate = useNavigate();
  const { setAuthDirectly } = useAuth();

  // Handle wizard timeout - show timeout message
  const handleTimeout = useCallback(() => {
    setIsTimedOut(true);
  }, []);

  // Reload wizard state (after timeout or refresh)
  const reloadWizardState = useCallback(async () => {
    setIsLoading(true);
    setIsTimedOut(false);
    setIsLocked(false);
    try {
      const status = await getWizardStatus();
      setTimeout(status.timeout ?? null);

      // Check if locked or timed out on server side
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
        // Admin already exists, redirect to dashboard
        navigate('/', { replace: true });
        return;
      }
    } catch (error) {
      console.error('Failed to load wizard state:', error);
    } finally {
      setIsLoading(false);
    }
  }, [navigate]);

  // Load wizard state on mount
  useEffect(() => {
    reloadWizardState();
  }, [reloadWizardState]);

  const handleAdminCreated = async (data: { username: string; password: string }) => {
    const response = await createAdmin(data);
    if (response.token && response.username && response.role) {
      setAuthDirectly(response.token, response.username, response.role);
    }

    // Mark wizard as installed (completes the wizard state machine)
    await installStack();

    // Redirect to dashboard where the onboarding checklist will guide further setup
    navigate('/', { replace: true });
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
          <p className="text-gray-600 dark:text-gray-400">Loading...</p>
        </div>
      </div>
    );
  }

  // Show timeout/locked message
  if (isTimedOut) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-white dark:bg-gray-900">
        <div className="text-center max-w-md p-8">
          <div className="mb-6">
            <svg className={`w-16 h-16 mx-auto ${isLocked ? 'text-red-500' : 'text-amber-500'}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
              {isLocked ? (
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
              ) : (
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
              )}
            </svg>
          </div>
          <h2 className="text-2xl font-bold text-gray-800 dark:text-white mb-4">
            {isLocked ? 'Setup Locked' : 'Setup Window Expired'}
          </h2>
          <p className="text-gray-600 dark:text-gray-400 mb-6">
            {isLocked ? (
              <>
                The 5-minute setup window has expired and the wizard is now locked.
                <br /><br />
                <strong>To try again, restart the container.</strong>
              </>
            ) : (
              'The 5-minute setup window has expired.'
            )}
          </p>
          {isLocked ? (
            <div className="px-6 py-3 bg-gray-200 dark:bg-gray-700 text-gray-600 dark:text-gray-400 font-mono text-sm rounded-lg">
              docker restart readystackgo
            </div>
          ) : (
            <button
              onClick={reloadWizardState}
              className="px-6 py-3 bg-brand-600 text-white font-medium rounded-lg hover:bg-brand-700 transition-colors"
            >
              Refresh Status
            </button>
          )}
        </div>
      </div>
    );
  }

  return (
    <WizardLayout timeout={timeout} onTimeout={handleTimeout}>
      <AdminStep onNext={handleAdminCreated} />
    </WizardLayout>
  );
}
