import { useEffect, useState } from "react";
import {
  getRegistries,
  createRegistry,
  updateRegistry,
  deleteRegistry,
  setDefaultRegistry,
  type RegistryDto,
  type CreateRegistryRequest,
  type UpdateRegistryRequest,
} from "../api/registries";
import {
  getStackSources,
  createStackSource,
  updateStackSource,
  deleteStackSource,
  syncSource,
  syncAllSources,
  type StackSourceDto,
  type CreateStackSourceRequest,
} from "../api/stackSources";

export default function Settings() {
  // Registry state
  const [registries, setRegistries] = useState<RegistryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [editingRegistry, setEditingRegistry] = useState<RegistryDto | null>(null);

  // Stack Sources state
  const [stackSources, setStackSources] = useState<StackSourceDto[]>([]);
  const [sourcesLoading, setSourcesLoading] = useState(true);
  const [sourceActionLoading, setSourceActionLoading] = useState<string | null>(null);
  const [isCreateSourceModalOpen, setIsCreateSourceModalOpen] = useState(false);

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

  const loadStackSources = async () => {
    try {
      setSourcesLoading(true);
      const sources = await getStackSources();
      setStackSources(sources);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load stack sources");
    } finally {
      setSourcesLoading(false);
    }
  };

  useEffect(() => {
    loadRegistries();
    loadStackSources();
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

  const handleDelete = async (id: string, name: string) => {
    if (!confirm(`Are you sure you want to delete the registry "${name}"?`)) {
      return;
    }

    try {
      setActionLoading(id);
      const response = await deleteRegistry(id);
      if (response.success) {
        await loadRegistries();
      } else {
        setError(response.message || "Failed to delete registry");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete registry");
    } finally {
      setActionLoading(null);
    }
  };

  // Stack Source handlers
  const handleToggleSource = async (source: StackSourceDto) => {
    try {
      setSourceActionLoading(source.id);
      const response = await updateStackSource(source.id, { enabled: !source.enabled });
      if (response.success) {
        await loadStackSources();
      } else {
        setError(response.message || "Failed to update stack source");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update stack source");
    } finally {
      setSourceActionLoading(null);
    }
  };

  const handleSyncSource = async (id: string) => {
    try {
      setSourceActionLoading(id);
      const result = await syncSource(id);
      if (result.success) {
        await loadStackSources();
      } else {
        setError(result.errors.join(", ") || "Failed to sync stack source");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to sync stack source");
    } finally {
      setSourceActionLoading(null);
    }
  };

  const handleSyncAllSources = async () => {
    try {
      setSourceActionLoading("all");
      const result = await syncAllSources();
      if (result.success) {
        await loadStackSources();
      } else {
        setError(result.errors.join(", ") || "Failed to sync stack sources");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to sync stack sources");
    } finally {
      setSourceActionLoading(null);
    }
  };

  const handleDeleteSource = async (id: string, name: string) => {
    if (!confirm(`Are you sure you want to delete the stack source "${name}"?`)) {
      return;
    }

    try {
      setSourceActionLoading(id);
      const response = await deleteStackSource(id);
      if (response.success) {
        await loadStackSources();
      } else {
        setError(response.message || "Failed to delete stack source");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete stack source");
    } finally {
      setSourceActionLoading(null);
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-title-md2 font-semibold text-black dark:text-white">
          Settings
        </h1>
      </div>

      {error && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg className="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
              </svg>
            </div>
            <div className="ml-3">
              <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
            </div>
            <div className="ml-auto pl-3">
              <button
                onClick={() => setError(null)}
                className="text-red-500 hover:text-red-600"
              >
                <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                  <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
                </svg>
              </button>
            </div>
          </div>
        </div>
      )}

      <div className="flex flex-col gap-10">
        {/* Registry Management Section */}
        <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="px-4 py-6 md:px-6 xl:px-7.5 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
            <div>
              <h4 className="text-xl font-semibold text-black dark:text-white">
                Container Registries
              </h4>
              <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
                Manage Docker registries for pulling container images during deployments
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
                Add Registry
              </button>
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
                  Add container registries to manage authentication for pulling images during deployments. Use image patterns to automatically match images to the correct registry credentials.
                </p>
                <button
                  onClick={() => setIsCreateModalOpen(true)}
                  className="mt-6 inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
                >
                  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                  </svg>
                  Add Your First Registry
                </button>
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
                      <button
                        onClick={() => setEditingRegistry(registry)}
                        className="inline-flex items-center justify-center rounded bg-gray-100 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
                      >
                        Edit
                      </button>
                      {!registry.isDefault && (
                        <button
                          onClick={() => handleSetDefault(registry.id)}
                          disabled={actionLoading === registry.id}
                          className="inline-flex items-center justify-center rounded bg-blue-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                          {actionLoading === registry.id ? "..." : "Set Default"}
                        </button>
                      )}
                      <button
                        onClick={() => handleDelete(registry.id, registry.name)}
                        disabled={actionLoading === registry.id}
                        className="inline-flex items-center justify-center rounded bg-red-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {actionLoading === registry.id ? "..." : "Delete"}
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Stack Sources Section */}
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
                onClick={() => setIsCreateSourceModalOpen(true)}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-4 py-2 text-center font-medium text-white hover:bg-brand-700"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                Add Source
              </button>
              <button
                onClick={handleSyncAllSources}
                disabled={sourceActionLoading === "all"}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-4 py-2 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                {sourceActionLoading === "all" ? "Syncing..." : "Sync All"}
              </button>
            </div>
          </div>

          {sourcesLoading ? (
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
                  onClick={() => setIsCreateSourceModalOpen(true)}
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
                        disabled={sourceActionLoading === source.id}
                        className={`inline-flex items-center justify-center rounded px-3 py-1.5 text-xs font-medium ${
                          source.enabled
                            ? "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
                            : "bg-green-600 text-white hover:bg-green-700"
                        } disabled:opacity-50 disabled:cursor-not-allowed`}
                      >
                        {sourceActionLoading === source.id ? "..." : source.enabled ? "Disable" : "Enable"}
                      </button>
                      <button
                        onClick={() => handleSyncSource(source.id)}
                        disabled={sourceActionLoading === source.id || !source.enabled}
                        className="inline-flex items-center justify-center rounded bg-blue-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {sourceActionLoading === source.id ? "..." : "Sync"}
                      </button>
                      <button
                        onClick={() => handleDeleteSource(source.id, source.name)}
                        disabled={sourceActionLoading === source.id}
                        className="inline-flex items-center justify-center rounded bg-red-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {sourceActionLoading === source.id ? "..." : "Delete"}
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Create Registry Modal */}
      {isCreateModalOpen && (
        <RegistryModal
          mode="create"
          onClose={() => setIsCreateModalOpen(false)}
          onSuccess={async () => {
            setIsCreateModalOpen(false);
            await loadRegistries();
          }}
        />
      )}

      {/* Edit Registry Modal */}
      {editingRegistry && (
        <RegistryModal
          mode="edit"
          registry={editingRegistry}
          onClose={() => setEditingRegistry(null)}
          onSuccess={async () => {
            setEditingRegistry(null);
            await loadRegistries();
          }}
        />
      )}

      {/* Create Stack Source Modal */}
      {isCreateSourceModalOpen && (
        <StackSourceModal
          onClose={() => setIsCreateSourceModalOpen(false)}
          onSuccess={async () => {
            setIsCreateSourceModalOpen(false);
            await loadStackSources();
          }}
        />
      )}
    </div>
  );
}

// Registry Modal Component (for Create and Edit)
function RegistryModal({
  mode,
  registry,
  onClose,
  onSuccess,
}: {
  mode: "create" | "edit";
  registry?: RegistryDto;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [formData, setFormData] = useState<CreateRegistryRequest>({
    name: registry?.name || "",
    url: registry?.url || "",
    username: registry?.username || "",
    password: "",
    imagePatterns: registry?.imagePatterns || [],
  });
  const [patternsInput, setPatternsInput] = useState(
    registry?.imagePatterns?.join("\n") || ""
  );
  const [clearCredentials, setClearCredentials] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    // Parse patterns from textarea (one per line)
    const patterns = patternsInput
      .split("\n")
      .map((p) => p.trim())
      .filter((p) => p.length > 0);

    try {
      setLoading(true);

      if (mode === "create") {
        const request: CreateRegistryRequest = {
          name: formData.name,
          url: formData.url,
          username: formData.username || undefined,
          password: formData.password || undefined,
          imagePatterns: patterns.length > 0 ? patterns : undefined,
        };
        const response = await createRegistry(request);
        if (response.success) {
          onSuccess();
        } else {
          setError(response.message || "Failed to create registry");
        }
      } else if (registry) {
        const request: UpdateRegistryRequest = {
          name: formData.name !== registry.name ? formData.name : undefined,
          url: formData.url !== registry.url ? formData.url : undefined,
          username: formData.username || undefined,
          password: formData.password || undefined,
          clearCredentials: clearCredentials,
          imagePatterns: patterns,
        };
        const response = await updateRegistry(registry.id, request);
        if (response.success) {
          onSuccess();
        } else {
          setError(response.message || "Failed to update registry");
        }
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : `Failed to ${mode} registry`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50 p-4">
      <div className="w-full max-w-lg rounded-lg bg-white p-6 dark:bg-gray-800 max-h-[90vh] overflow-y-auto">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            {mode === "create" ? "Add Registry" : "Edit Registry"}
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
              Registry Name *
            </label>
            <input
              type="text"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              placeholder="Docker Hub"
              required
              className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
            />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
              Registry URL *
            </label>
            <input
              type="text"
              value={formData.url}
              onChange={(e) => setFormData({ ...formData, url: e.target.value })}
              placeholder="https://index.docker.io/v1/"
              required
              className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
            />
            <p className="mt-1 text-xs text-gray-500">
              Examples: https://index.docker.io/v1/, https://ghcr.io, https://registry.example.com
            </p>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Username
              </label>
              <input
                type="text"
                value={formData.username}
                onChange={(e) => setFormData({ ...formData, username: e.target.value })}
                placeholder="username"
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Password / Token
              </label>
              <input
                type="password"
                value={formData.password}
                onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                placeholder={mode === "edit" ? "(unchanged)" : "password or token"}
                className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
              />
            </div>
          </div>

          {mode === "edit" && registry?.hasCredentials && (
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="clearCredentials"
                checked={clearCredentials}
                onChange={(e) => setClearCredentials(e.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-brand-600 focus:ring-brand-500"
              />
              <label htmlFor="clearCredentials" className="text-sm text-gray-700 dark:text-gray-300">
                Clear existing credentials
              </label>
            </div>
          )}

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
              Image Patterns
            </label>
            <textarea
              value={patternsInput}
              onChange={(e) => setPatternsInput(e.target.value)}
              placeholder={"library/*\nmyorg/*\nghcr.io/myorg/**"}
              rows={4}
              className="w-full rounded-lg border border-gray-300 bg-transparent px-4 py-2.5 text-sm font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-600 dark:text-white"
            />
            <p className="mt-1 text-xs text-gray-500">
              One pattern per line. Use * for single segment match, ** for multiple segments.
              <br />
              Examples: library/*, ghcr.io/myorg/**, myregistry.com:5000/*
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
              {loading ? (mode === "create" ? "Creating..." : "Saving...") : (mode === "create" ? "Create Registry" : "Save Changes")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// Stack Source Modal Component
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
          : { gitUrl: formData.gitUrl, branch: formData.branch || "main", path: formData.path || undefined }),
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
