import { useEffect, useState } from "react";
import {
  getEnvironments,
  createEnvironment,
  deleteEnvironment,
  setDefaultEnvironment,
  type EnvironmentResponse,
  type CreateEnvironmentRequest,
} from "../../api/environments";
import { getWizardStatus } from "../../api/wizard";
import { useEnvironment } from "../../context/EnvironmentContext";

export default function Environments() {
  const { refreshEnvironments: refreshEnvContext } = useEnvironment();
  const [environments, setEnvironments] = useState<EnvironmentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const loadEnvironments = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await getEnvironments();
      if (response.success) {
        setEnvironments(response.environments);
      } else {
        setError("Failed to load environments");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load environments");
    } finally {
      setLoading(false);
    }
  };

  // Refresh both local state and context
  const refreshAll = async () => {
    await loadEnvironments();
    await refreshEnvContext();
  };

  useEffect(() => {
    loadEnvironments();
  }, []);

  const handleSetDefault = async (id: string) => {
    try {
      setActionLoading(id);
      const response = await setDefaultEnvironment(id);
      if (response.success) {
        await refreshAll();
      } else {
        setError(response.message || "Failed to set default environment");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to set default environment");
    } finally {
      setActionLoading(null);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Are you sure you want to delete this environment?")) {
      return;
    }

    try {
      setActionLoading(id);
      const response = await deleteEnvironment(id);
      if (response.success) {
        await refreshAll();
      } else {
        setError(response.message || "Failed to delete environment");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete environment");
    } finally {
      setActionLoading(null);
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-title-md2 font-semibold text-black dark:text-white">
          Environments
        </h1>
        <div className="flex gap-3">
          <button
            onClick={() => setIsCreateModalOpen(true)}
            className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            Add Environment
          </button>
          <button
            onClick={loadEnvironments}
            className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Refresh
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
        </div>
      )}

      <div className="flex flex-col gap-10">
        <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="px-4 py-6 md:px-6 xl:px-7.5">
            <h4 className="text-xl font-semibold text-black dark:text-white">
              Docker Environments
            </h4>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
              Manage your Docker environments for container and stack deployments
            </p>
          </div>

          {loading ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                Loading environments...
              </p>
            </div>
          ) : environments.length === 0 ? (
            <div className="border-t border-stroke px-4 py-16 dark:border-strokedark">
              <div className="text-center max-w-md mx-auto">
                <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-brand-100 dark:bg-brand-900/30">
                  <svg className="h-8 w-8 text-brand-600 dark:text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01" />
                  </svg>
                </div>
                <h3 className="mt-4 text-lg font-semibold text-gray-900 dark:text-white">
                  No Environments Configured
                </h3>
                <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
                  Environments connect ReadyStackGo to Docker daemons. Create your first environment to start managing containers and deploying stacks.
                </p>
                <button
                  onClick={() => setIsCreateModalOpen(true)}
                  className="mt-6 inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
                >
                  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                  </svg>
                  Create Your First Environment
                </button>
              </div>
            </div>
          ) : (
            <>
              <div className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5">
                <div className="col-span-2 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Name</p>
                </div>
                <div className="col-span-2 hidden items-center sm:flex">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Type</p>
                </div>
                <div className="col-span-2 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Connection</p>
                </div>
                <div className="col-span-2 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Actions</p>
                </div>
              </div>

              {environments.map((env) => (
                <div
                  key={env.id}
                  className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5"
                >
                  <div className="col-span-2 flex flex-col gap-1">
                    <div className="flex items-center gap-2">
                      <p className="text-sm text-gray-900 dark:text-white font-medium">
                        {env.name}
                      </p>
                      {env.isDefault && (
                        <span className="inline-flex rounded-full bg-brand-100 px-2 py-0.5 text-xs font-medium text-brand-800 dark:bg-brand-900 dark:text-brand-200">
                          Default
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-gray-600 dark:text-gray-400">
                      {env.id}
                    </p>
                  </div>

                  <div className="col-span-2 hidden items-center sm:flex">
                    <span className="inline-flex rounded-full bg-gray-100 px-3 py-1 text-xs font-medium text-gray-800 dark:bg-gray-700 dark:text-gray-300">
                      {env.type}
                    </span>
                  </div>

                  <div className="col-span-2 flex items-center">
                    <p className="text-sm text-gray-900 dark:text-gray-300 truncate" title={env.connectionString}>
                      {env.connectionString}
                    </p>
                  </div>

                  <div className="col-span-2 flex items-center gap-2">
                    {!env.isDefault && (
                      <button
                        onClick={() => handleSetDefault(env.id)}
                        disabled={actionLoading === env.id}
                        className="inline-flex items-center justify-center rounded bg-blue-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {actionLoading === env.id ? "..." : "Set Default"}
                      </button>
                    )}
                    <button
                      onClick={() => handleDelete(env.id)}
                      disabled={actionLoading === env.id}
                      className="inline-flex items-center justify-center rounded bg-red-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      title="Delete environment"
                    >
                      {actionLoading === env.id ? "..." : "Delete"}
                    </button>
                  </div>
                </div>
              ))}
            </>
          )}
        </div>
      </div>

      {/* Create Environment Modal */}
      {isCreateModalOpen && (
        <CreateEnvironmentModal
          onClose={() => setIsCreateModalOpen(false)}
          onSuccess={async () => {
            setIsCreateModalOpen(false);
            await refreshAll();
          }}
        />
      )}
    </div>
  );
}

// Create Environment Modal Component
function CreateEnvironmentModal({
  onClose,
  onSuccess,
}: {
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [formData, setFormData] = useState<CreateEnvironmentRequest>({
    name: "",
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

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    try {
      setLoading(true);
      const response = await createEnvironment(formData);
      if (response.success) {
        onSuccess();
      } else {
        setError(response.message || "Failed to create environment");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create environment");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
      <div className="w-full max-w-md rounded-lg bg-white p-6 dark:bg-gray-800">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            Add Environment
          </h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
          >
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {error && (
          <div className="mb-4 rounded-md bg-red-50 p-3 dark:bg-red-900/20">
            <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
          </div>
        )}

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

          <div className="flex justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading}
              className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {loading ? "Creating..." : "Create Environment"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
