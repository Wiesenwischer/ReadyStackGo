import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  getRegistries,
  setDefaultRegistry,
  type RegistryDto,
} from "../../../api/registries";

export default function RegistriesList() {
  const [registries, setRegistries] = useState<RegistryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const loadRegistries = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await getRegistries();
      setRegistries(response.registries);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load registries");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRegistries();
  }, []);

  const handleSetDefault = async (id: string) => {
    try {
      setActionLoading(id);
      const response = await setDefaultRegistry(id);
      if (response.success) {
        await loadRegistries();
      } else {
        setError(response.message || "Failed to set default registry");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to set default registry");
    } finally {
      setActionLoading(null);
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
        <span className="text-gray-900 dark:text-white">Container Registries</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-6 md:px-6 xl:px-7.5 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div>
            <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
              Container Registries
            </h2>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
              Manage Docker registries for pulling container images during deployments
            </p>
          </div>
          <div className="flex gap-3">
            <Link
              to="/settings/registries/add"
              className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-4 py-2 text-center font-medium text-white hover:bg-brand-700"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              Add Registry
            </Link>
            <button
              onClick={loadRegistries}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-4 py-2 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Refresh
            </button>
          </div>
        </div>

        {error && (
          <div className="mx-4 mb-4 rounded-md bg-red-50 p-3 dark:bg-red-900/20">
            <div className="flex justify-between items-center">
              <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
              <button onClick={() => setError(null)} className="text-red-500 hover:text-red-600">
                <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
          </div>
        )}

        {loading ? (
          <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
            <p className="text-center text-sm text-gray-600 dark:text-gray-400">
              Loading registries...
            </p>
          </div>
        ) : registries.length === 0 ? (
          <div className="border-t border-stroke px-4 py-16 dark:border-strokedark">
            <div className="text-center max-w-md mx-auto">
              <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-brand-100 dark:bg-brand-900/30">
                <svg className="h-8 w-8 text-brand-600 dark:text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 8h14M5 8a2 2 0 110-4h14a2 2 0 110 4M5 8v10a2 2 0 002 2h10a2 2 0 002-2V8m-9 4h4" />
                </svg>
              </div>
              <h3 className="mt-4 text-lg font-semibold text-gray-900 dark:text-white">
                No Registries Configured
              </h3>
              <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
                Add container registries to manage authentication for pulling images during deployments.
              </p>
              <Link
                to="/settings/registries/add"
                className="mt-6 inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                Add Your First Registry
              </Link>
            </div>
          </div>
        ) : (
          <div className="border-t border-stroke dark:border-strokedark">
            {registries.map((registry) => (
              <div
                key={registry.id}
                className="border-b border-stroke px-4 py-4 dark:border-strokedark last:border-b-0 md:px-6"
              >
                <div className="flex flex-col md:flex-row md:items-start md:justify-between gap-4">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <h5 className="text-base font-semibold text-gray-900 dark:text-white">
                        {registry.name}
                      </h5>
                      {registry.isDefault && (
                        <span className="inline-flex rounded-full bg-brand-100 px-2 py-0.5 text-xs font-medium text-brand-800 dark:bg-brand-900 dark:text-brand-200">
                          Default
                        </span>
                      )}
                      {registry.hasCredentials && (
                        <span className="inline-flex rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900 dark:text-green-200">
                          Authenticated
                        </span>
                      )}
                    </div>
                    <p className="mt-1 text-sm text-gray-600 dark:text-gray-400 truncate">
                      {registry.url}
                    </p>
                    {registry.imagePatterns.length > 0 && (
                      <div className="mt-2 flex flex-wrap gap-1">
                        {registry.imagePatterns.map((pattern, index) => (
                          <span
                            key={index}
                            className="inline-flex rounded bg-gray-100 px-2 py-0.5 text-xs font-mono text-gray-700 dark:bg-gray-700 dark:text-gray-300"
                          >
                            {pattern}
                          </span>
                        ))}
                      </div>
                    )}
                    {registry.username && (
                      <p className="mt-1 text-xs text-gray-500 dark:text-gray-500">
                        Username: {registry.username}
                      </p>
                    )}
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    <Link
                      to={`/settings/registries/${registry.id}/edit`}
                      className="inline-flex items-center justify-center rounded bg-gray-100 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
                    >
                      Edit
                    </Link>
                    {!registry.isDefault && (
                      <button
                        onClick={() => handleSetDefault(registry.id)}
                        disabled={actionLoading === registry.id}
                        className="inline-flex items-center justify-center rounded bg-blue-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {actionLoading === registry.id ? "..." : "Set Default"}
                      </button>
                    )}
                    <Link
                      to={`/settings/registries/${registry.id}/delete`}
                      className="inline-flex items-center justify-center rounded bg-red-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-700"
                    >
                      Delete
                    </Link>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
