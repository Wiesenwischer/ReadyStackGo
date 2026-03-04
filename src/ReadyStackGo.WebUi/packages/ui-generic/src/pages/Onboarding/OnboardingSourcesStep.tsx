import { useState, useEffect, useCallback } from 'react';
import { useOnboardingSourcesStore } from '@rsgo/core';

interface OnboardingSourcesStepProps {
  onNext: () => void;
  onSkip: () => void;
}

export default function OnboardingSourcesStep({ onNext, onSkip }: OnboardingSourcesStepProps) {
  const store = useOnboardingSourcesStore();
  const [isSkipping, setIsSkipping] = useState(false);

  const handleSubmit = useCallback(async () => {
    if (store.isSubmitting || isSkipping) return;
    const success = await store.submit();
    if (success) {
      onNext();
    }
  }, [store, isSkipping, onNext]);

  const handleSkip = () => {
    setIsSkipping(true);
    onSkip();
  };

  // Enter key advances to next step
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Enter' && !store.isFetching) {
        e.preventDefault();
        if (store.selectedIds.size > 0) {
          handleSubmit();
        } else {
          handleSkip();
        }
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [store.isFetching, store.selectedIds, handleSubmit]);

  if (store.isFetching) {
    return (
      <div className="flex items-center justify-center py-12">
        <svg className="animate-spin h-8 w-8 text-brand-600 dark:text-brand-400" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
        </svg>
      </div>
    );
  }

  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">
          Stack Sources
        </h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Select curated stack sources to add to your instance.
          These provide ready-to-deploy stack definitions. You can add or remove sources later in Settings.
        </p>
      </div>

      {store.error && (
        <div className="mb-4 p-4 text-sm border border-red-300 rounded-lg bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
          {store.error}
        </div>
      )}

      {store.sources.length === 0 ? (
        <div className="py-8 text-center text-gray-500 dark:text-gray-400">
          <p>No curated sources available yet.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {store.sources.map(source => (
            <label
              key={source.id}
              className={`flex items-start gap-4 p-4 rounded-lg border cursor-pointer transition-colors ${
                store.selectedIds.has(source.id)
                  ? 'border-brand-500 bg-brand-50 dark:bg-brand-900/20 dark:border-brand-600'
                  : 'border-gray-200 hover:border-gray-300 dark:border-gray-700 dark:hover:border-gray-600'
              }`}
            >
              <input
                type="checkbox"
                checked={store.selectedIds.has(source.id)}
                onChange={() => store.toggleSource(source.id)}
                className="mt-1 h-4 w-4 rounded border-gray-300 text-brand-600 focus:ring-brand-500 dark:border-gray-600 dark:bg-gray-800"
              />
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <span className="font-medium text-gray-800 dark:text-white">
                    {source.name}
                  </span>
                  {source.featured && (
                    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-brand-100 text-brand-800 dark:bg-brand-900/40 dark:text-brand-300">
                      Featured
                    </span>
                  )}
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400">
                    {source.type === 'local-directory' ? 'Local' : 'Git'}
                  </span>
                </div>
                <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
                  {source.description}
                </p>
              </div>
            </label>
          ))}
        </div>
      )}

      <div className="pt-6 flex gap-3">
        <button
          type="button"
          onClick={handleSkip}
          disabled={store.isSubmitting || isSkipping}
          className="flex-1 py-3 text-sm font-medium text-gray-700 transition-colors rounded-lg border border-gray-300 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-500/50 disabled:opacity-50 disabled:cursor-not-allowed dark:text-gray-300 dark:border-gray-600 dark:hover:bg-gray-800"
        >
          Skip for now
        </button>
        <button
          type="button"
          onClick={handleSubmit}
          disabled={store.isSubmitting || isSkipping || store.selectedIds.size === 0}
          className="flex-1 py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {store.isSubmitting ? 'Adding sources...' : `Add ${store.selectedIds.size} source${store.selectedIds.size !== 1 ? 's' : ''}`}
        </button>
      </div>
    </div>
  );
}
