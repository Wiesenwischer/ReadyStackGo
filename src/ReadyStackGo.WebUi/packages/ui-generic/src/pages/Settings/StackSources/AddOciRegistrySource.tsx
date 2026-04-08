import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useStackSourceStore, type CreateStackSourceRequest, testOciConnection } from '@rsgo/core';
import { useAuth } from "../../../context/AuthContext";

export default function AddOciRegistrySource() {
  const navigate = useNavigate();
  const store = useStackSourceStore();
  const { token } = useAuth();
  const [formData, setFormData] = useState({
    id: "",
    name: "",
    registryUrl: "",
    repository: "",
    tagPattern: "*",
    registryUsername: "",
    registryPassword: "",
  });
  const [testResult, setTestResult] = useState<{ success: boolean; message: string; sampleTags?: string[] } | null>(null);
  const [testing, setTesting] = useState(false);

  const handleTestConnection = async () => {
    if (!formData.registryUrl || !formData.repository) return;
    setTesting(true);
    setTestResult(null);
    try {
      const result = await testOciConnection(token!, {
        registryUrl: formData.registryUrl,
        repository: formData.repository,
        username: formData.registryUsername || undefined,
        password: formData.registryPassword || undefined,
      });
      setTestResult({
        success: result.success,
        message: result.message || (result.success ? `Found ${result.tagCount} tags` : 'Connection failed'),
        sampleTags: result.sampleTags,
      });
    } catch (err) {
      setTestResult({ success: false, message: `Connection error: ${err}` });
    } finally {
      setTesting(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    store.clearError();

    const request: CreateStackSourceRequest = {
      id: formData.id,
      name: formData.name,
      type: "OciRegistry",
      registryUrl: formData.registryUrl,
      repository: formData.repository,
      tagPattern: formData.tagPattern || "*",
      registryUsername: formData.registryUsername || undefined,
      registryPassword: formData.registryPassword || undefined,
    };

    const success = await store.create(request);
    if (success) {
      navigate("/settings/stack-sources");
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6 flex items-center gap-2 text-sm">
        <Link to="/settings" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Settings
        </Link>
        <span className="text-gray-400">/</span>
        <Link to="/settings/stack-sources" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Stack Sources
        </Link>
        <span className="text-gray-400">/</span>
        <Link to="/settings/stack-sources/add" className="text-gray-500 hover:text-brand-600 dark:text-gray-400">
          Add Source
        </Link>
        <span className="text-gray-400">/</span>
        <span className="text-gray-900 dark:text-white">OCI Registry</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-6 py-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 flex items-center justify-center rounded-full bg-blue-100 text-blue-600 dark:bg-blue-900/30 dark:text-blue-400">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
              </svg>
            </div>
            <div>
              <h4 className="text-xl font-semibold text-black dark:text-white">
                OCI Registry Source
              </h4>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Pull stack definitions from a Docker/OCI container registry
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
            {/* Basic Info */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                  Source ID <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={formData.id}
                  onChange={(e) => setFormData({ ...formData, id: e.target.value })}
                  placeholder="my-oci-stacks"
                  required
                  className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                />
                <p className="mt-1 text-xs text-gray-500">
                  Unique identifier for this source (lowercase, no spaces)
                </p>
              </div>
              <div>
                <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                  Display Name <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  placeholder="My OCI Stacks"
                  required
                  className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                />
              </div>
            </div>

            {/* Registry Host */}
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Registry Host <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={formData.registryUrl}
                onChange={(e) => setFormData({ ...formData, registryUrl: e.target.value })}
                placeholder="docker.io, ghcr.io, myregistry.azurecr.io"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                Registry hostname without protocol (e.g., docker.io, ghcr.io)
              </p>
            </div>

            {/* Repository */}
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Repository <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={formData.repository}
                onChange={(e) => setFormData({ ...formData, repository: e.target.value })}
                placeholder="myorg/rsgo-stacks"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                Full repository path (e.g., namespace/repository)
              </p>
            </div>

            {/* Tag Pattern */}
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Tag Pattern
              </label>
              <input
                type="text"
                value={formData.tagPattern}
                onChange={(e) => setFormData({ ...formData, tagPattern: e.target.value })}
                placeholder="*"
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                Glob pattern to filter tags (e.g., v*, ams-*, *). Default: * (all tags)
              </p>
            </div>

            {/* Authentication Section */}
            <div className="rounded-lg border border-gray-200 dark:border-gray-700 p-4">
              <h5 className="text-sm font-medium text-gray-900 dark:text-white mb-3">
                Authentication
                <span className="ml-2 text-xs font-normal text-gray-500">(optional, for private registries)</span>
              </h5>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Username
                  </label>
                  <input
                    type="text"
                    value={formData.registryUsername}
                    onChange={(e) => setFormData({ ...formData, registryUsername: e.target.value })}
                    placeholder="registry-user"
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Password / Token
                  </label>
                  <input
                    type="password"
                    value={formData.registryPassword}
                    onChange={(e) => setFormData({ ...formData, registryPassword: e.target.value })}
                    placeholder="Enter password or token"
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>
              </div>
              <p className="mt-3 text-xs text-gray-500">
                For Docker Hub, use your Docker Hub username and access token.
                For GHCR, use a GitHub PAT with read:packages scope.
              </p>
            </div>

            {/* Test Connection */}
            <div>
              <button
                type="button"
                onClick={handleTestConnection}
                disabled={testing || !formData.registryUrl || !formData.repository}
                className="inline-flex items-center gap-2 rounded-md bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 disabled:opacity-50 disabled:cursor-not-allowed dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                {testing ? (
                  <>
                    <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                    </svg>
                    Testing...
                  </>
                ) : (
                  <>
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                    </svg>
                    Test Connection
                  </>
                )}
              </button>

              {testResult && (
                <div className={`mt-3 rounded-md p-3 ${testResult.success ? 'bg-green-50 dark:bg-green-900/20' : 'bg-red-50 dark:bg-red-900/20'}`}>
                  <p className={`text-sm font-medium ${testResult.success ? 'text-green-800 dark:text-green-200' : 'text-red-800 dark:text-red-200'}`}>
                    {testResult.message}
                  </p>
                  {testResult.sampleTags && testResult.sampleTags.length > 0 && (
                    <div className="mt-2 flex flex-wrap gap-1">
                      {testResult.sampleTags.map((tag) => (
                        <span key={tag} className="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-800/30 dark:text-green-300">
                          {tag}
                        </span>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>

          <div className="mt-8 flex items-center justify-between border-t border-gray-200 dark:border-gray-700 pt-6">
            <Link
              to="/settings/stack-sources/add"
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Back
            </Link>
            <button
              type="submit"
              disabled={store.actionLoading === 'creating'}
              className="rounded-md bg-brand-600 px-6 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {store.actionLoading === 'creating' ? "Creating..." : "Create Source"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
