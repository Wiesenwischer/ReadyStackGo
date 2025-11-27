import { useState, useEffect, type FormEvent } from 'react';
import { getWizardStatus } from '../../api/wizard';

interface EnvironmentStepProps {
  onNext: (data: { id: string; name: string; socketPath: string } | null) => Promise<void>;
}

export default function EnvironmentStep({ onNext }: EnvironmentStepProps) {
  const [id, setId] = useState('local');
  const [name, setName] = useState('Local Docker');
  const [socketPath, setSocketPath] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSkipping, setIsSkipping] = useState(false);

  // Fetch default socket path from server on mount
  useEffect(() => {
    const fetchDefaultSocketPath = async () => {
      try {
        const status = await getWizardStatus();
        const defaultPath = status.defaultDockerSocketPath || 'unix:///var/run/docker.sock';
        setSocketPath(defaultPath);
      } catch {
        // Fallback to Linux default if API fails
        setSocketPath('unix:///var/run/docker.sock');
      }
    };
    fetchDefaultSocketPath();
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');

    // Validation
    if (id.length < 2) {
      setError('Environment ID must be at least 2 characters long');
      return;
    }

    if (name.length < 2) {
      setError('Environment name must be at least 2 characters long');
      return;
    }

    // Validate ID format (only lowercase letters, numbers, hyphens, underscores)
    if (!/^[a-z0-9_-]+$/.test(id)) {
      setError('Environment ID can only contain lowercase letters, numbers, hyphens, and underscores');
      return;
    }

    if (!socketPath) {
      setError('Docker socket path is required');
      return;
    }

    setIsLoading(true);
    try {
      await onNext({ id, name, socketPath });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create environment');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSkip = async () => {
    setIsSkipping(true);
    try {
      await onNext(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to skip step');
    } finally {
      setIsSkipping(false);
    }
  };

  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">
          Environment Setup
        </h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Configure your first Docker environment to manage containers and stacks.
          You can skip this step and add environments later.
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
              Environment ID <span className="text-error-500">*</span>
            </label>
            <input
              type="text"
              value={id}
              onChange={(e) => setId(e.target.value.toLowerCase())}
              placeholder="local"
              required
              minLength={2}
              pattern="[a-z0-9_-]+"
              className="w-full h-12.5 px-4 py-3 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-600"
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              Technical identifier (lowercase letters, numbers, hyphens, underscores)
            </p>
          </div>

          <div>
            <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
              Display Name <span className="text-error-500">*</span>
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Local Docker"
              required
              minLength={2}
              className="w-full h-12.5 px-4 py-3 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-600"
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              Friendly name for this environment
            </p>
          </div>

          <div>
            <label className="block mb-2.5 text-sm font-medium text-gray-700 dark:text-gray-300">
              Docker Socket Path <span className="text-error-500">*</span>
            </label>
            <input
              type="text"
              value={socketPath}
              onChange={(e) => setSocketPath(e.target.value)}
              placeholder="unix:///var/run/docker.sock"
              required
              className="w-full h-12.5 px-4 py-3 text-sm bg-transparent border border-gray-300 rounded-lg shadow-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-500 dark:border-gray-700 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-600"
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              Path to the Docker daemon socket (auto-detected for your platform)
            </p>
          </div>

          <div className="pt-4 flex gap-3">
            <button
              type="button"
              onClick={handleSkip}
              disabled={isLoading || isSkipping}
              className="flex-1 py-3 text-sm font-medium text-gray-700 transition-colors rounded-lg border border-gray-300 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-500/50 disabled:opacity-50 disabled:cursor-not-allowed dark:text-gray-300 dark:border-gray-600 dark:hover:bg-gray-800"
            >
              {isSkipping ? 'Skipping...' : 'Skip for now'}
            </button>
            <button
              type="submit"
              disabled={isLoading || isSkipping}
              className="flex-1 py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isLoading ? 'Creating...' : 'Create Environment'}
            </button>
          </div>
        </div>
      </form>
    </div>
  );
}
