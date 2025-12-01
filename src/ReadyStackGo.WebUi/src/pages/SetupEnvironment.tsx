import { useState, useEffect } from "react";
import {
  createEnvironment,
  type CreateEnvironmentRequest,
} from "../api/environments";
import { getWizardStatus } from "../api/wizard";
import { useEnvironment } from "../context/EnvironmentContext";

/**
 * Page shown when no environments exist.
 * Guides the user to create their first Docker environment.
 */
export default function SetupEnvironment() {
  const { environments, isLoading, refreshEnvironments } = useEnvironment();
  const [formData, setFormData] = useState<CreateEnvironmentRequest>({
    name: "Local Docker",
    socketPath: "", // Will be set from API
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [defaultSocketPath, setDefaultSocketPath] = useState<string>("");

  // Fetch default socket path from server on mount
  useEffect(() => {
    const fetchDefaultSocketPath = async () => {
      try {
        const status = await getWizardStatus();
        const socketPath = status.defaultDockerSocketPath || "unix:///var/run/docker.sock";
        setDefaultSocketPath(socketPath);
        setFormData(prev => ({ ...prev, socketPath }));
      } catch {
        // Fallback to Linux default if API fails
        const fallback = "unix:///var/run/docker.sock";
        setDefaultSocketPath(fallback);
        setFormData(prev => ({ ...prev, socketPath: fallback }));
      }
    };
    fetchDefaultSocketPath();
  }, []);

  // Redirect to dashboard if environments already exist
  useEffect(() => {
    if (!isLoading && environments.length > 0) {
      window.location.href = "/";
    }
  }, [environments, isLoading]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    try {
      setLoading(true);
      const response = await createEnvironment(formData);
      console.log('Create environment response:', response);
      if (response.success) {
        await refreshEnvironments();
        // Use window.location for a full page reload to ensure fresh state
        window.location.href = "/";
      } else {
        setError(response.message || "Failed to create environment");
      }
    } catch (err) {
      console.error('Create environment error:', err);
      setError(err instanceof Error ? err.message : "Failed to create environment");
    } finally {
      setLoading(false);
    }
  };

  // Show loading while checking for existing environments
  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gray-50 dark:bg-gray-900">
        <div className="text-center">
          <svg className="mx-auto h-8 w-8 animate-spin text-brand-600" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
          </svg>
          <p className="mt-2 text-sm text-gray-500">Loading...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50 dark:bg-gray-900 px-4">
      <div className="w-full max-w-md">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 shadow-sm dark:border-gray-800 dark:bg-gray-800">
          {/* Header */}
          <div className="mb-6 text-center">
            <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-brand-100 dark:bg-brand-900/30">
              <svg className="h-8 w-8 text-brand-600 dark:text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01" />
              </svg>
            </div>
            <h1 className="text-2xl font-semibold text-gray-900 dark:text-white">
              Setup Your First Environment
            </h1>
            <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
              Connect to a Docker daemon to start managing containers and stacks
            </p>
          </div>

          {/* Error */}
          {error && (
            <div className="mb-4 rounded-md bg-red-50 p-3 dark:bg-red-900/20">
              <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
            </div>
          )}

          {/* Form */}
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Environment Name
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder="Local Docker"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Docker Socket Path
              </label>
              <input
                type="text"
                value={formData.socketPath}
                onChange={(e) => setFormData({ ...formData, socketPath: e.target.value })}
                placeholder={defaultSocketPath || "Loading..."}
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                Path to the Docker daemon socket
              </p>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="mt-6 w-full rounded-lg bg-brand-600 px-4 py-3 text-sm font-medium text-white hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {loading ? "Creating Environment..." : "Create Environment"}
            </button>
          </form>

          {/* Info */}
          <div className="mt-6 rounded-lg border border-blue-200 bg-blue-50 p-4 dark:border-blue-800 dark:bg-blue-900/20">
            <div className="flex gap-3">
              <svg className="h-5 w-5 flex-shrink-0 text-blue-600 dark:text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <div className="text-xs text-blue-700 dark:text-blue-300">
                <p className="font-medium">Docker Socket</p>
                <p className="mt-1">
                  The default path <code className="rounded bg-blue-100 px-1 dark:bg-blue-800">{defaultSocketPath || "..."}</code> has been detected for your server's operating system.
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
