import { useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';

interface OnboardingCompleteStepProps {
  configuredOrg: boolean;
  configuredEnv: boolean;
  configuredSources: boolean;
  configuredRegistries: boolean;
}

export default function OnboardingCompleteStep({
  configuredOrg,
  configuredEnv,
  configuredSources,
  configuredRegistries,
}: OnboardingCompleteStepProps) {
  const navigate = useNavigate();

  const goToDashboard = useCallback(() => {
    navigate('/', { replace: true });
  }, [navigate]);

  // Enter key navigates to dashboard
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        goToDashboard();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [goToDashboard]);

  const items = [
    { label: 'Organization', done: configuredOrg },
    { label: 'Docker Environment', done: configuredEnv },
    { label: 'Stack Sources', done: configuredSources },
    { label: 'Container Registries', done: configuredRegistries },
  ];

  return (
    <div className="text-center">
      <div className="mb-6">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-green-100 dark:bg-green-900/30">
          <svg className="h-8 w-8 text-green-600 dark:text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
        </div>
      </div>

      <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">
        You're All Set!
      </h2>
      <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
        Your ReadyStackGo instance is ready to use. Here's what was configured:
      </p>

      <div className="mb-8 space-y-2 text-left max-w-sm mx-auto">
        {items.map(item => (
          <div key={item.label} className="flex items-center gap-3">
            {item.done ? (
              <svg className="w-5 h-5 text-green-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            ) : (
              <svg className="w-5 h-5 text-gray-300 dark:text-gray-600 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M15 12H9m12 0a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            )}
            <span className={`text-sm ${item.done ? 'text-gray-700 dark:text-gray-300' : 'text-gray-400 dark:text-gray-500'}`}>
              {item.label}
              {!item.done && ' — skipped'}
            </span>
          </div>
        ))}
      </div>

      <button
        onClick={goToDashboard}
        autoFocus
        className="w-full py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50"
      >
        Go to Dashboard
      </button>
    </div>
  );
}
