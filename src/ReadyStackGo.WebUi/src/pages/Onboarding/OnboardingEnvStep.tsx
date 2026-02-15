import { useState, useEffect } from 'react';
import { createEnvironment } from '../../api/environments';
import { getWizardStatus } from '../../api/wizard';

interface OnboardingEnvStepProps {
  onNext: () => void;
  onSkip: () => void;
}

export default function OnboardingEnvStep({ onNext, onSkip }: OnboardingEnvStepProps) {
  const [name, setName] = useState('Local Docker');
  const [socketPath, setSocketPath] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSkipping, setIsSkipping] = useState(false);

  useEffect(() => {
    const fetchDefaultSocketPath = async () => {
      try {
        const status = await getWizardStatus();
        setSocketPath(status.defaultDockerSocketPath || 'unix:///var/run/docker.sock');
      } catch {
        setSocketPath('unix:///var/run/docker.sock');
      }
    };
    fetchDefaultSocketPath();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      const response = await createEnvironment({
        name: name.trim(),
        socketPath: socketPath.trim(),
      });
      if (response.success) {
        onNext();
      } else {
        setError(response.message || 'Failed to create environment');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create environment');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSkip = () => {
    setIsSkipping(true);
    onSkip();
  };

  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">
          Add Docker Environment
        </h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Connect ReadyStackGo to a Docker daemon to manage containers and deploy stacks.
          You can add more environments later.
        </p>
      </div>

      {error && (
        <div className="mb-4 p-4 text-sm border border-red-300 rounded-lg bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        <div className="mb-4">
          <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">
            Environment Name
          </label>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Local Docker"
            required
            autoFocus
            className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-3 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white dark:placeholder-gray-500"
          />
        </div>

        <div className="mb-6">
          <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">
            Docker Socket Path
          </label>
          <input
            type="text"
            value={socketPath}
            onChange={(e) => setSocketPath(e.target.value)}
            placeholder="unix:///var/run/docker.sock"
            required
            className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-3 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white dark:placeholder-gray-500"
          />
          <p className="mt-1.5 text-xs text-gray-400 dark:text-gray-500">
            Path to the Docker daemon socket on the host
          </p>
        </div>

        <div className="flex gap-3">
          <button
            type="button"
            onClick={handleSkip}
            disabled={isLoading || isSkipping}
            className="flex-1 py-3 text-sm font-medium text-gray-700 transition-colors rounded-lg border border-gray-300 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-500/50 disabled:opacity-50 disabled:cursor-not-allowed dark:text-gray-300 dark:border-gray-600 dark:hover:bg-gray-800"
          >
            Skip for now
          </button>
          <button
            type="submit"
            disabled={isLoading || isSkipping}
            className="flex-1 py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isLoading ? 'Creating...' : 'Continue'}
          </button>
        </div>
      </form>
    </div>
  );
}
