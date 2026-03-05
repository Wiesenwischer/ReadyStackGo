import { type FormEvent } from 'react';
import { useOnboardingOrgStore } from '@rsgo/core';

interface OnboardingOrgStepProps {
  onNext: () => void;
}

export default function OnboardingOrgStep({ onNext }: OnboardingOrgStepProps) {
  const store = useOnboardingOrgStore();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const success = await store.submit();
    if (success) {
      onNext();
    }
  };

  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">
          Create Your Organization
        </h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Your organization is the top-level identity for this ReadyStackGo instance.
          All environments, stacks, and settings belong to it.
        </p>
      </div>

      {store.error && (
        <div className="mb-4 p-4 text-sm border border-red-300 rounded-lg bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
          {store.error}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        <div className="space-y-5">
          <div>
            <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">
              Organization ID
            </label>
            <input
              type="text"
              value={store.id}
              onChange={(e) => store.setId(e.target.value)}
              placeholder="my-company"
              required
              minLength={2}
              pattern="[a-z0-9-]+"
              autoFocus
              className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-3 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white dark:placeholder-gray-500"
            />
            <p className="mt-1.5 text-xs text-gray-400 dark:text-gray-500">
              Technical identifier (lowercase letters, numbers, hyphens only)
            </p>
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">
              Organization Name
            </label>
            <input
              type="text"
              value={store.name}
              onChange={(e) => store.setName(e.target.value)}
              placeholder="My Company Inc."
              required
              minLength={3}
              className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-3 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white dark:placeholder-gray-500"
            />
            <p className="mt-1.5 text-xs text-gray-400 dark:text-gray-500">
              Display name — you can change this later in Settings
            </p>
          </div>
        </div>

        <div className="pt-6">
          <button
            type="submit"
            disabled={store.isSubmitting}
            className="w-full py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {store.isSubmitting ? 'Creating...' : 'Continue'}
          </button>
        </div>
      </form>
    </div>
  );
}
