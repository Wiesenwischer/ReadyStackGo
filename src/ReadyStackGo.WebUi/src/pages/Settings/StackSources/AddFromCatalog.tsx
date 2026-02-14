import { useState, useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import { getRegistrySources, addFromRegistry, type RegistrySourceDto } from "../../../api/stackSources";

export default function AddFromCatalog() {
  const navigate = useNavigate();
  const [sources, setSources] = useState<RegistrySourceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [adding, setAdding] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchSources = async () => {
      try {
        const entries = await getRegistrySources();
        setSources(entries);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load catalog");
      } finally {
        setLoading(false);
      }
    };
    fetchSources();
  }, []);

  const handleAdd = async (source: RegistrySourceDto) => {
    setError(null);
    setAdding(source.id);
    try {
      const response = await addFromRegistry(source.id);
      if (response.success) {
        navigate("/settings/stack-sources");
      } else {
        setError(response.message || "Failed to add source");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add source");
    } finally {
      setAdding(null);
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
        <span className="text-gray-900 dark:text-white">From Catalog</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-6 py-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 flex items-center justify-center rounded-full bg-brand-100 text-brand-600 dark:bg-brand-900/30 dark:text-brand-400">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
              </svg>
            </div>
            <div>
              <h4 className="text-xl font-semibold text-black dark:text-white">
                Add from Catalog
              </h4>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Select a curated source to add with one click
              </p>
            </div>
          </div>
        </div>

        <div className="p-6">
          {error && (
            <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
              <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
            </div>
          )}

          {loading ? (
            <div className="flex items-center justify-center py-12">
              <svg className="animate-spin h-8 w-8 text-brand-600 dark:text-brand-400" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
            </div>
          ) : sources.length === 0 ? (
            <div className="py-12 text-center text-gray-500 dark:text-gray-400">
              <svg className="mx-auto h-12 w-12 mb-4 text-gray-300 dark:text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" />
              </svg>
              <p>No curated sources available yet.</p>
            </div>
          ) : (
            <div className="space-y-3">
              {sources.map(source => (
                <div
                  key={source.id}
                  className="flex items-start gap-4 p-4 rounded-lg border border-gray-200 dark:border-gray-700"
                >
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-gray-800 dark:text-white">
                        {source.name}
                      </span>
                      {source.featured && (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-brand-100 text-brand-800 dark:bg-brand-900/40 dark:text-brand-300">
                          Featured
                        </span>
                      )}
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400">
                        {source.category}
                      </span>
                    </div>
                    <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
                      {source.description}
                    </p>
                    <p className="mt-1 text-xs text-gray-400 dark:text-gray-500 font-mono">
                      {source.gitUrl} ({source.gitBranch})
                    </p>
                  </div>
                  <div className="flex-shrink-0">
                    {source.alreadyAdded ? (
                      <span className="inline-flex items-center px-3 py-1.5 rounded-md text-xs font-medium bg-green-50 text-green-700 dark:bg-green-900/20 dark:text-green-400">
                        Already added
                      </span>
                    ) : (
                      <button
                        onClick={() => handleAdd(source)}
                        disabled={adding !== null}
                        className="rounded-md bg-brand-600 px-4 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {adding === source.id ? "Adding..." : "Add"}
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}

          <div className="mt-8 flex items-center justify-between border-t border-gray-200 dark:border-gray-700 pt-6">
            <Link
              to="/settings/stack-sources/add"
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Back
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
