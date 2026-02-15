import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getOnboardingStatus } from '../../api/onboarding';

/**
 * Minimal, non-dismissable banner shown on the Dashboard when optional
 * setup items (sources, registries) are still missing.
 * Disappears automatically once all items are configured.
 */
export default function SetupHint() {
  const [hints, setHints] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    const checkStatus = async () => {
      try {
        const status = await getOnboardingStatus();
        const missing: string[] = [];
        if (!status.stackSources.done) missing.push('stack sources');
        if (!status.registries.done) missing.push('container registries');
        setHints(missing);
      } catch {
        // Non-critical — don't show anything on error
      } finally {
        setLoading(false);
      }
    };
    checkStatus();
  }, []);

  if (loading || hints.length === 0) {
    return null;
  }

  return (
    <div className="mb-6 flex items-center gap-3 rounded-lg border border-blue-200 bg-blue-50 px-5 py-3 dark:border-blue-800 dark:bg-blue-900/20">
      <svg className="h-5 w-5 flex-shrink-0 text-blue-500 dark:text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
      </svg>
      <p className="flex-1 text-sm text-blue-800 dark:text-blue-200">
        Configure {hints.join(' and ')} in{' '}
        <button
          onClick={() => navigate('/settings')}
          className="font-medium underline hover:no-underline"
        >
          Settings
        </button>
        {' '}to start deploying stacks.
      </p>
    </div>
  );
}
