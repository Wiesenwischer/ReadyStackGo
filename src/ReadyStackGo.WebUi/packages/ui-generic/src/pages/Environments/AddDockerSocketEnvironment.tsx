import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useEnvironmentStore, type CreateEnvironmentRequest } from '@rsgo/core';

export default function AddDockerSocketEnvironment() {
  const navigate = useNavigate();
  const store = useEnvironmentStore();
  const [formData, setFormData] = useState<CreateEnvironmentRequest>({
    name: "",
    type: "DockerSocket",
    socketPath: "",
  });
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);

  useEffect(() => {
    store.loadDefaultSocketPath();
  }, [store.loadDefaultSocketPath]);

  useEffect(() => {
    if (store.defaultSocketPath && !formData.socketPath) {
      setFormData(prev => ({ ...prev, socketPath: store.defaultSocketPath }));
    }
  }, [store.defaultSocketPath, formData.socketPath]);

  const handleTestConnection = async () => {
    setTestResult(null);
    const result = await store.testConn({
      type: 'DockerSocket' as const,
      dockerHost: formData.socketPath,
    });
    if (result) {
      setTestResult({ success: result.success, message: result.message });
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    store.clearError();
    const success = await store.create(formData);
    if (success) {
      navigate("/environments");
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
        <Link to="/environments/add" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Add Environment
        </Link>
        <span className="text-gray-400">/</span>
        <span className="text-gray-900 dark:text-white">Docker Socket</span>
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
                Local Docker Socket
              </h4>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Connect via Unix socket on the local server
              </p>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="p-6">
          {store.error && (
            <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
              <p className="text-sm text-red-800 dark:text-red-200">{store.error}</p>
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
                placeholder="My Docker Server"
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
                value={formData.socketPath || ''}
                onChange={(e) => setFormData({ ...formData, socketPath: e.target.value })}
                placeholder={store.defaultSocketPath || "Loading..."}
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                Path to the Docker daemon socket (auto-detected from server)
              </p>
            </div>

            {/* Test Connection */}
            <div className="pt-2">
              <button
                type="button"
                onClick={handleTestConnection}
                disabled={store.actionLoading === 'testing'}
                className="inline-flex items-center gap-2 rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-800"
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
                {store.actionLoading === 'testing' ? 'Testing...' : 'Test Connection'}
              </button>
              {testResult && (
                <div className={`mt-2 rounded-md p-3 text-sm ${
                  testResult.success
                    ? 'bg-green-50 text-green-800 dark:bg-green-900/20 dark:text-green-200'
                    : 'bg-red-50 text-red-800 dark:bg-red-900/20 dark:text-red-200'
                }`}>
                  {testResult.message}
                </div>
              )}
            </div>
          </div>

          <div className="mt-8 flex items-center justify-between border-t border-gray-200 dark:border-gray-700 pt-6">
            <Link
              to="/environments/add"
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Back
            </Link>
            <button
              type="submit"
              disabled={store.actionLoading === 'creating'}
              className="rounded-md bg-brand-600 px-6 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {store.actionLoading === 'creating' ? "Creating..." : "Create Environment"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
