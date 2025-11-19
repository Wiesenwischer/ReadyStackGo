import { useState, type FormEvent } from 'react';

interface OrganizationStepProps {
  onNext: (data: { id: string; name: string }) => Promise<void>;
  onBack: () => void;
}

export default function OrganizationStep({ onNext, onBack }: OrganizationStepProps) {
  const [id, setId] = useState('');
  const [name, setName] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');

    // Validation
    if (id.length < 2) {
      setError('Organization ID must be at least 2 characters long');
      return;
    }

    if (name.length < 3) {
      setError('Organization name must be at least 3 characters long');
      return;
    }

    // Validate ID format (only lowercase letters, numbers, and hyphens)
    if (!/^[a-z0-9-]+$/.test(id)) {
      setError('Organization ID can only contain lowercase letters, numbers, and hyphens');
      return;
    }

    setIsLoading(true);
    try {
      await onNext({ id, name });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to set organization');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">
          Organization Setup
        </h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Define your organization details for this ReadyStackGo instance
        </p>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="space-y-5">
          {error && (
            <div className="p-4 text-sm border border-red-300 rounded-lg bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
              {error}
            </div>
          )}

          <div>
            <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
              Organization ID <span className="text-error-500">*</span>
            </label>
            <input
              type="text"
              value={id}
              onChange={(e) => setId(e.target.value.toLowerCase())}
              placeholder="my-company"
              required
              minLength={2}
              pattern="[a-z0-9-]+"
              className="w-full h-12.5 px-4 py-3 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-600"
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              Technical identifier (lowercase letters, numbers, hyphens only)
            </p>
          </div>

          <div>
            <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
              Organization Name <span className="text-error-500">*</span>
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="My Company Inc."
              required
              minLength={3}
              className="w-full h-12.5 px-4 py-3 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-600"
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              Display name for your organization
            </p>
          </div>

          <div className="pt-4 flex gap-3">
            <button
              type="button"
              onClick={onBack}
              className="inline-flex items-center justify-center flex-1 py-3 text-sm font-medium text-gray-700 dark:text-gray-300 transition-colors bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 px-7"
            >
              Back
            </button>
            <button
              type="submit"
              disabled={isLoading}
              className="inline-flex items-center justify-center flex-1 py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed px-7"
            >
              {isLoading ? 'Saving...' : 'Continue'}
            </button>
          </div>
        </div>
      </form>
    </div>
  );
}
