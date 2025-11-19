import { useEffect, useState, type ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { getWizardStatus } from '../../api/wizard';

interface WizardGuardProps {
  children: ReactNode;
}

export default function WizardGuard({ children }: WizardGuardProps) {
  const [isChecking, setIsChecking] = useState(true);
  const [isWizardCompleted, setIsWizardCompleted] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    const checkWizardStatus = async () => {
      // Skip wizard check if we're already on the wizard page
      if (location.pathname === '/wizard') {
        setIsWizardCompleted(true);
        setIsChecking(false);
        return;
      }

      try {
        const status = await getWizardStatus();

        if (!status.isCompleted) {
          // Wizard not completed, redirect to wizard
          navigate('/wizard', { replace: true });
        } else {
          setIsWizardCompleted(true);
        }
      } catch (error) {
        console.error('Failed to check wizard status:', error);
        // On error, assume wizard is not completed and redirect
        navigate('/wizard', { replace: true });
      } finally {
        setIsChecking(false);
      }
    };

    checkWizardStatus();
  }, [navigate, location.pathname]);

  if (isChecking) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-white dark:bg-gray-900">
        <div className="text-center">
          <svg className="animate-spin h-12 w-12 mx-auto text-brand-600 dark:text-brand-400 mb-4" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <p className="text-gray-600 dark:text-gray-400">Loading ReadyStackGo...</p>
        </div>
      </div>
    );
  }

  return isWizardCompleted ? <>{children}</> : null;
}
