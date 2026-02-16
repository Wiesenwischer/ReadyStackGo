import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  getStackSources,
  createStackSource,
  updateStackSource,
  syncSource,
  syncAllSources,
  type StackSourceDto,
  type CreateStackSourceRequest,
} from "../../api/stackSources";

export default function StackSourcesSettings() {
  const navigate = useNavigate();
  const [stackSources, setStackSources] = useState<StackSourceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

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

  return (
    <>
      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-6 md:px-6 xl:px-7.5 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div>
            <h4 className="text-xl font-semibold text-black dark:text-white">
              Stack Sources
            </h4>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
              Configure where ReadyStackGo loads stack definitions from
            </p>
          </div>
          <div className="flex gap-3">
            <button
              onClick={() => setIsCreateModalOpen(true)}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-4 py-2 text-center font-medium text-white hover:bg-brand-700"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              Add Source
            </button>
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
              <button
                onClick={() => setIsCreateModalOpen(true)}
                className="mt-6 inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                Add Your First Source
              </button>
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
                            • Authenticated as {source.details.username}
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
                    <button
                      onClick={() => navigate(`/settings/stack-sources/${source.id}/delete`)}
                      className="inline-flex items-center justify-center rounded bg-red-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-700"
                    >
                      Delete
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Create Stack Source Modal */}
      {isCreateModalOpen && (
        <StackSourceModal
          onClose={() => setIsCreateModalOpen(false)}
          onSuccess={async () => {
            setIsCreateModalOpen(false);
            await loadStackSources();
          }}
        />
      )}
    </>
  );
}

function StackSourceModal({
  onClose,
  onSuccess,
}: {
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [sourceType, setSourceType] = useState<"LocalDirectory" | "GitRepository">("LocalDirectory");
  const [formData, setFormData] = useState({
    id: "",
    name: "",
    path: "",
    filePattern: "*.yml;*.yaml",
    gitUrl: "",
    branch: "main",
    gitUsername: "",
    gitPassword: "",
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    try {
      setLoading(true);

      const request: CreateStackSourceRequest = {
        id: formData.id,
        name: formData.name,
        type: sourceType,
        filePattern: formData.filePattern || undefined,
        ...(sourceType === "LocalDirectory"
          ? { path: formData.path }
          : {
              gitUrl: formData.gitUrl,
              branch: formData.branch || "main",
              path: formData.path || undefined,
              gitUsername: formData.gitUsername || undefined,
              gitPassword: formData.gitPassword || undefined,
            }),
      };

      const response = await createStackSource(request);
      if (response.success) {
        onSuccess();
      } else {
        setError(response.message || "Failed to create stack source");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create stack source");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50 p-4">
      <div className="w-full max-w-lg rounded-lg bg-white p-6 dark:bg-gray-800 max-h-[90vh] overflow-y-auto">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            Add Stack Source
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
          {/* Source Type Selection */}
          <div>
            <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-300">
              Source Type *
            </label>
            <div className="flex gap-4">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="radio"
                  name="sourceType"
                  value="LocalDirectory"
                  checked={sourceType === "LocalDirectory"}
                  onChange={() => setSourceType("LocalDirectory")}
                  className="h-4 w-4 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">Local Directory</span>
              </label>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="radio"
                  name="sourceType"
                  value="GitRepository"
                  checked={sourceType === "GitRepository"}
                  onChange={() => setSourceType("GitRepository")}
                  className="h-4 w-4 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">Git Repository</span>
              </label>
            </div>
          </div>

          {/* Common Fields */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Source ID *
              </label>
              <input
                type="text"
                value={formData.id}
                onChange={(e) => setFormData({ ...formData, id: e.target.value })}
                placeholder="my-stacks"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Display Name *
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder="My Stack Sources"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
            </div>
          </div>

          {/* Local Directory Fields */}
          {sourceType === "LocalDirectory" && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Path *
              </label>
              <input
                type="text"
                value={formData.path}
                onChange={(e) => setFormData({ ...formData, path: e.target.value })}
                placeholder="/app/stacks"
                required
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
              <p className="mt-1 text-xs text-gray-500">
                Absolute or relative path to the directory containing stack definitions
              </p>
            </div>
          )}

          {/* Git Repository Fields */}
          {sourceType === "GitRepository" && (
            <>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                  Git URL *
                </label>
                <input
                  type="text"
                  value={formData.gitUrl}
                  onChange={(e) => setFormData({ ...formData, gitUrl: e.target.value })}
                  placeholder="https://github.com/org/stacks-repo.git"
                  required
                  className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Branch
                  </label>
                  <input
                    type="text"
                    value={formData.branch}
                    onChange={(e) => setFormData({ ...formData, branch: e.target.value })}
                    placeholder="main"
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                    Sub-path
                  </label>
                  <input
                    type="text"
                    value={formData.path}
                    onChange={(e) => setFormData({ ...formData, path: e.target.value })}
                    placeholder="stacks/"
                    className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                  />
                  <p className="mt-1 text-xs text-gray-500">
                    Optional path within the repository
                  </p>
                </div>
              </div>

              {/* Git Credentials (optional) */}
              <div className="border-t border-gray-200 dark:border-gray-700 pt-4 mt-2">
                <p className="mb-3 text-sm font-medium text-gray-700 dark:text-gray-300">
                  Authentication (optional, for private repositories)
                </p>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Username
                    </label>
                    <input
                      type="text"
                      value={formData.gitUsername}
                      onChange={(e) => setFormData({ ...formData, gitUsername: e.target.value })}
                      placeholder="git-user"
                      className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Password / Token
                    </label>
                    <input
                      type="password"
                      value={formData.gitPassword}
                      onChange={(e) => setFormData({ ...formData, gitPassword: e.target.value })}
                      placeholder="••••••••"
                      className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
                    />
                  </div>
                </div>
                <p className="mt-2 text-xs text-gray-500">
                  For GitHub/GitLab, use a Personal Access Token instead of your password
                </p>
              </div>
            </>
          )}

          {/* File Pattern */}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
              File Pattern
            </label>
            <input
              type="text"
              value={formData.filePattern}
              onChange={(e) => setFormData({ ...formData, filePattern: e.target.value })}
              placeholder="*.yml;*.yaml"
              className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
            />
            <p className="mt-1 text-xs text-gray-500">
              Semicolon-separated patterns for stack files (default: *.yml;*.yaml)
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
              {loading ? "Creating..." : "Create Source"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
