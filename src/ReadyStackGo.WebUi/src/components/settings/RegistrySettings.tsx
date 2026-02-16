import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  getRegistries,
  createRegistry,
  updateRegistry,
  setDefaultRegistry,
  type RegistryDto,
  type CreateRegistryRequest,
  type UpdateRegistryRequest,
} from "../../api/registries";

export default function RegistrySettings() {
  const navigate = useNavigate();
  const [registries, setRegistries] = useState<RegistryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [editingRegistry, setEditingRegistry] = useState<RegistryDto | null>(null);

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
    <>
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
                      onClick={() => navigate(`/settings/registries/${registry.id}/delete`)}
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
    </>
  );
}

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
              Examples: https://index.docker.io/v1/, https://ghcr.io
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
