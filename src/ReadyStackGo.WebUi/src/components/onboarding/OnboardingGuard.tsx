import { useEffect, useState, type ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { getOnboardingStatus } from '../../api/onboarding';

interface OnboardingGuardProps {
  children: ReactNode;
}

/**
 * Guard that redirects to /onboarding if the organization has not been set up yet.
 * Follows the same pattern as WizardGuard.
 */
export default function OnboardingGuard({ children }: OnboardingGuardProps) {
  const [isChecking, setIsChecking] = useState(true);
  const [isOnboardingComplete, setIsOnboardingComplete] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    const checkOnboardingStatus = async () => {
      // Skip check if already on the onboarding page
      if (location.pathname === '/onboarding') {
        setIsOnboardingComplete(true);
        setIsChecking(false);
        return;
      }

      try {
        const status = await getOnboardingStatus();

        if (!status.organization.done) {
          // Organization not configured → mandatory onboarding
          navigate('/onboarding', { replace: true });
        } else {
          setIsOnboardingComplete(true);
        }
      } catch (error) {
        console.error('Failed to check onboarding status:', error);
        // On error, let through — don't block the app
        setIsOnboardingComplete(true);
      } finally {
        setIsChecking(false);
      }
    };

    checkOnboardingStatus();
  }, [navigate, location.pathname]);

  if (isChecking) {
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

  return isOnboardingComplete ? <>{children}</> : null;
}
