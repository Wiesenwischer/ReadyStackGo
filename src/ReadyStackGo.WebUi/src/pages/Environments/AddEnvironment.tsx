import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { createEnvironment, type CreateEnvironmentRequest } from "../../api/environments";
import { getWizardStatus } from "../../api/wizard";

export default function AddEnvironment() {
  const navigate = useNavigate();
  const [formData, setFormData] = useState<CreateEnvironmentRequest>({
    name: "",
    socketPath: "",
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [defaultSocketPath, setDefaultSocketPath] = useState<string>("");

  useEffect(() => {
    const fetchDefaultSocketPath = async () => {
      try {
        const status = await getWizardStatus();
        const socketPath = status.defaultDockerSocketPath || "unix:///var/run/docker.sock";
        setDefaultSocketPath(socketPath);
        setFormData(prev => ({ ...prev, socketPath }));
      } catch {
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
        navigate("/environments");
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
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center gap-2 text-sm">
        <Link to="/environments" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Environments
        </Link>
        <span className="text-gray-400">/</span>
        <span className="text-gray-900 dark:text-white">Add Environment</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-6 py-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 flex items-center justify-center rounded-full bg-brand-100 text-brand-600 dark:bg-brand-900/30 dark:text-brand-400">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01" />
              </svg>
            </div>
            <div>
              <h4 className="text-xl font-semibold text-black dark:text-white">
                Add Environment
              </h4>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Connect ReadyStackGo to a Docker daemon
              </p>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="p-6">
          {error && (
            <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
              <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
            </div>
          )}

          <div className="space-y-6 max-w-2xl">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Environment Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder="Local Docker"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                A descriptive name for this Docker environment
              </p>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Docker Socket Path <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={formData.socketPath}
                onChange={(e) => setFormData({ ...formData, socketPath: e.target.value })}
                placeholder={defaultSocketPath || "Loading..."}
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                Path to the Docker daemon socket (auto-detected from server)
              </p>
            </div>
          </div>

          <div className="mt-8 flex items-center justify-between border-t border-gray-200 dark:border-gray-700 pt-6">
            <Link
              to="/environments"
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Cancel
            </Link>
            <button
              type="submit"
              disabled={loading}
              className="rounded-md bg-brand-600 px-6 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {loading ? "Creating..." : "Create Environment"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
