import { useState } from 'react';

interface InstallStepProps {
  onInstall: () => Promise<void>;
  onBack?: () => void;
}

export default function InstallStep({ onInstall }: InstallStepProps) {
  const [isInstalling, setIsInstalling] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<{
    success: boolean;
    stackVersion?: string;
    deployedContexts: string[];
    errors: string[];
  } | null>(null);

  const handleInstall = async () => {
    setError('');
    setResult(null);
    setIsInstalling(true);

    try {
      await onInstall();
      // Success will be handled by the parent component redirecting to dashboard
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Installation failed';
      setError(errorMessage);
      setResult({
        success: false,
        deployedContexts: [],
        errors: [errorMessage],
      });
    } finally {
      setIsInstalling(false);
    }
  };

  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">
          Complete Setup
        </h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Review your configuration and complete the initial setup
        </p>
      </div>

      <div className="space-y-5">
        {/* Summary Card */}
        <div className="p-5 border border-gray-200 rounded-lg bg-gray-50 dark:bg-gray-800/50 dark:border-gray-700">
          <h3 className="mb-3 text-sm font-medium text-gray-700 dark:text-gray-300">
            Configuration Summary
          </h3>
          <div className="space-y-2 text-sm">
            <div className="flex items-center gap-2">
              <svg className="w-5 h-5 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
              <span className="text-gray-600 dark:text-gray-400">Admin account configured</span>
            </div>
            <div className="flex items-center gap-2">
              <svg className="w-5 h-5 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
              <span className="text-gray-600 dark:text-gray-400">Organization details set</span>
            </div>
            <div className="flex items-center gap-2">
              <svg className="w-5 h-5 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
              <span className="text-gray-600 dark:text-gray-400">Connection strings configured</span>
            </div>
          </div>
        </div>

        {/* What will happen */}
        <div className="p-4 border border-blue-200 rounded-lg bg-blue-50 dark:bg-blue-900/20 dark:border-blue-800">
          <div className="flex gap-3">
            <svg className="w-5 h-5 text-blue-600 dark:text-blue-400 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <div>
              <p className="text-sm font-medium text-blue-800 dark:text-blue-300">
                What happens next
              </p>
              <p className="mt-2 text-xs text-blue-700 dark:text-blue-400">
                Your configuration will be saved and you'll be able to access the admin dashboard. You can deploy and manage container stacks from there.
              </p>
            </div>
          </div>
        </div>

        {/* Error Display */}
        {error && (
          <div className="p-4 text-sm border border-red-300 rounded-lg bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
            <p className="font-medium mb-1">Setup Failed</p>
            <p>{error}</p>
            {result?.errors && result.errors.length > 0 && (
              <ul className="mt-2 space-y-1 list-disc list-inside">
                {result.errors.map((err, idx) => (
                  <li key={idx}>{err}</li>
                ))}
              </ul>
            )}
          </div>
        )}

        {/* Setup in Progress */}
        {isInstalling && (
          <div className="p-4 text-sm border border-blue-300 rounded-lg bg-blue-50 dark:bg-blue-900/20 dark:border-blue-800">
            <div className="flex items-center gap-3">
              <svg className="animate-spin h-5 w-5 text-blue-600 dark:text-blue-400" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              <p className="text-blue-800 dark:text-blue-300">
                Completing setup...
              </p>
            </div>
          </div>
        )}

        <div className="pt-4">
          <button
            type="button"
            onClick={handleInstall}
            disabled={isInstalling}
            className="inline-flex items-center justify-center w-full py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed px-7"
          >
            {isInstalling ? 'Completing Setup...' : 'Complete Setup'}
          </button>
        </div>
      </div>
    </div>
  );
}
