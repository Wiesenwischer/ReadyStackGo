import { useEffect, useState, useRef } from "react";
import { Link } from "react-router-dom";
import {
  getStackSources,
  updateStackSource,
  syncSource,
  syncAllSources,
  exportSources,
  importSources,
  type StackSourceDto,
} from "../../../api/stackSources";

export default function StackSourcesList() {
  const [stackSources, setStackSources] = useState<StackSourceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const loadStackSources = async () => {
    try {
      setLoading(true);
      setError(null);
      const sources = await getStackSources();
      setStackSources(sources);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load stack sources");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadStackSources();
  }, []);

  const handleToggleSource = async (source: StackSourceDto) => {
    try {
      setActionLoading(source.id);
      const response = await updateStackSource(source.id, { enabled: !source.enabled });
      if (response.success) {
        await loadStackSources();
      } else {
        setError(response.message || "Failed to update stack source");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update stack source");
    } finally {
      setActionLoading(null);
    }
  };

  const handleSyncSource = async (id: string) => {
    try {
      setActionLoading(id);
      const result = await syncSource(id);
      if (result.success) {
        await loadStackSources();
      } else {
        setError(result.errors.join(", ") || "Failed to sync stack source");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to sync stack source");
    } finally {
      setActionLoading(null);
    }
  };

  const handleSyncAllSources = async () => {
    try {
      setActionLoading("all");
      const result = await syncAllSources();
      if (result.success) {
        await loadStackSources();
      } else {
        setError(result.errors.join(", ") || "Failed to sync stack sources");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to sync stack sources");
    } finally {
      setActionLoading(null);
    }
  };

  const handleExport = async () => {
    try {
      setActionLoading("export");
      const data = await exportSources();
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `rsgo-sources-${new Date().toISOString().slice(0, 10)}.json`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to export sources");
    } finally {
      setActionLoading(null);
    }
  };

  const handleImportFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      setActionLoading("import");
      const text = await file.text();
      const data = JSON.parse(text);

      if (!data.version || !Array.isArray(data.sources)) {
        setError("Invalid import file format. Expected { version, sources[] }.");
        return;
      }

      const result = await importSources(data);
      if (result.success) {
        await loadStackSources();
        setError(null);
      } else {
        setError(result.message || "Failed to import sources");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to import sources");
    } finally {
      setActionLoading(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
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
        <span className="text-gray-900 dark:text-white">Stack Sources</span>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-6 md:px-6 xl:px-7.5 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div>
            <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
              Stack Sources
            </h2>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
              Configure where ReadyStackGo loads stack definitions from
            </p>
          </div>
          <div className="flex gap-3">
            <Link
              to="/settings/stack-sources/add"
              className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-4 py-2 text-center font-medium text-white hover:bg-brand-700"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              Add Source
            </Link>
            <button
              onClick={handleExport}
              disabled={actionLoading === "export"}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-4 py-2 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              {actionLoading === "export" ? "Exporting..." : "Export"}
            </button>
            <button
              onClick={() => fileInputRef.current?.click()}
              disabled={actionLoading === "import"}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-4 py-2 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
              </svg>
              {actionLoading === "import" ? "Importing..." : "Import"}
            </button>
            <input
              ref={fileInputRef}
              type="file"
              accept=".json"
              onChange={handleImportFile}
              className="hidden"
            />
            <button
              onClick={handleSyncAllSources}
              disabled={actionLoading === "all"}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-4 py-2 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              {actionLoading === "all" ? "Syncing..." : "Sync All"}
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
              Loading stack sources...
            </p>
          </div>
        ) : stackSources.length === 0 ? (
          <div className="border-t border-stroke px-4 py-16 dark:border-strokedark">
            <div className="text-center max-w-md mx-auto">
              <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-brand-100 dark:bg-brand-900/30">
                <svg className="h-8 w-8 text-brand-600 dark:text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
                </svg>
              </div>
              <h3 className="mt-4 text-lg font-semibold text-gray-900 dark:text-white">
                No Stack Sources Configured
              </h3>
              <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
                Add a local directory or Git repository to load stack definitions from.
              </p>
              <Link
                to="/settings/stack-sources/add"
                className="mt-6 inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                Add Your First Source
              </Link>
            </div>
          </div>
        ) : (
          <div className="border-t border-stroke dark:border-strokedark">
            {stackSources.map((source) => (
              <div
                key={source.id}
                className="border-b border-stroke px-4 py-4 dark:border-strokedark last:border-b-0 md:px-6"
              >
                <div className="flex flex-col md:flex-row md:items-start md:justify-between gap-4">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <h5 className="text-base font-semibold text-gray-900 dark:text-white">
                        {source.name}
                      </h5>
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                        source.type === "LocalDirectory"
                          ? "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200"
                          : "bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200"
                      }`}>
                        {source.type === "LocalDirectory" ? "Local" : "Git"}
                      </span>
                      <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                        source.enabled
                          ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
                          : "bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400"
                      }`}>
                        {source.enabled ? "Enabled" : "Disabled"}
                      </span>
                    </div>
                    <p className="mt-1 text-sm text-gray-600 dark:text-gray-400 font-mono truncate">
                      {source.details.path || source.details.repositoryUrl || source.id}
                    </p>
                    {source.details.branch && (
                      <p className="mt-1 text-xs text-gray-500">
                        Branch: {source.details.branch}
                        {source.details.username && (
                          <span className="ml-2 text-green-600 dark:text-green-400">
                            Authenticated as {source.details.username}
                          </span>
                        )}
                      </p>
                    )}
                    {source.lastSyncedAt && (
                      <p className="mt-1 text-xs text-gray-500">
                        Last synced: {new Date(source.lastSyncedAt).toLocaleString()}
                      </p>
                    )}
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    <button
                      onClick={() => handleToggleSource(source)}
                      disabled={actionLoading === source.id}
                      className={`inline-flex items-center justify-center rounded px-3 py-1.5 text-xs font-medium ${
                        source.enabled
                          ? "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
                          : "bg-green-600 text-white hover:bg-green-700"
                      } disabled:opacity-50 disabled:cursor-not-allowed`}
                    >
                      {actionLoading === source.id ? "..." : source.enabled ? "Disable" : "Enable"}
                    </button>
                    <button
                      onClick={() => handleSyncSource(source.id)}
                      disabled={actionLoading === source.id || !source.enabled}
                      className="inline-flex items-center justify-center rounded bg-blue-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      {actionLoading === source.id ? "..." : "Sync"}
                    </button>
                    <Link
                      to={`/settings/stack-sources/${source.id}/delete`}
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
