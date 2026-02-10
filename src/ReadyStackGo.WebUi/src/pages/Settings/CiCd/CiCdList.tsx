import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  getApiKeys,
  createApiKey,
  revokeApiKey,
  type ApiKeyDto,
  type CreateApiKeyRequest,
} from "../../../api/apiKeys";

const AVAILABLE_PERMISSIONS = [
  { value: "Hooks.Redeploy", label: "Redeploy", description: "Trigger redeployment of running stacks" },
  { value: "Hooks.Upgrade", label: "Upgrade", description: "Upgrade stacks to a new version" },
  { value: "Hooks.SyncSources", label: "Sync Sources", description: "Synchronize stack catalog sources" },
];

export default function CiCdList() {
  const [apiKeys, setApiKeys] = useState<ApiKeyDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showRevokeModal, setShowRevokeModal] = useState<string | null>(null);
  const [revokeReason, setRevokeReason] = useState("");
  const [createdKey, setCreatedKey] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);

  // Create form state
  const [newKeyName, setNewKeyName] = useState("");
  const [newKeyPermissions, setNewKeyPermissions] = useState<string[]>(["Hooks.Redeploy"]);
  const [newKeyExpiry, setNewKeyExpiry] = useState("");

  const loadApiKeys = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await getApiKeys();
      setApiKeys(response.apiKeys);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load API keys");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadApiKeys();
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newKeyName.trim()) return;

    try {
      setActionLoading(true);
      setError(null);
      const request: CreateApiKeyRequest = {
        name: newKeyName.trim(),
        permissions: newKeyPermissions,
      };
      if (newKeyExpiry) {
        const date = new Date(newKeyExpiry + "T23:59:59");
        request.expiresAt = date.toISOString();
      }

      const response = await createApiKey(request);
      if (response.success && response.apiKey) {
        setCreatedKey(response.apiKey.fullKey);
        setShowCreateModal(false);
        setNewKeyName("");
        setNewKeyPermissions(["Hooks.Redeploy"]);
        setNewKeyExpiry("");
        await loadApiKeys();
      } else {
        setError(response.message || "Failed to create API key");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create API key");
    } finally {
      setActionLoading(false);
    }
  };

  const handleRevoke = async () => {
    if (!showRevokeModal) return;

    try {
      setActionLoading(true);
      setError(null);
      const response = await revokeApiKey(showRevokeModal, revokeReason || undefined);
      if (response.success) {
        setShowRevokeModal(null);
        setRevokeReason("");
        await loadApiKeys();
      } else {
        setError(response.message || "Failed to revoke API key");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to revoke API key");
    } finally {
      setActionLoading(false);
    }
  };

  const handleCopyKey = async () => {
    if (!createdKey) return;
    try {
      await navigator.clipboard.writeText(createdKey);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback for non-secure contexts
      const textArea = document.createElement("textarea");
      textArea.value = createdKey;
      document.body.appendChild(textArea);
      textArea.select();
      document.execCommand("copy");
      document.body.removeChild(textArea);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const togglePermission = (perm: string) => {
    setNewKeyPermissions((prev) =>
      prev.includes(perm) ? prev.filter((p) => p !== perm) : [...prev, perm]
    );
  };

  const formatDate = (dateStr: string | null) => {
    if (!dateStr) return "-";
    return new Date(dateStr).toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const getStatusBadge = (key: ApiKeyDto) => {
    if (key.isRevoked) {
      return (
        <span className="inline-flex items-center rounded-full bg-red-100 px-2.5 py-0.5 text-xs font-medium text-red-800 dark:bg-red-900/30 dark:text-red-400">
          Revoked
        </span>
      );
    }
    if (key.expiresAt && new Date(key.expiresAt) < new Date()) {
      return (
        <span className="inline-flex items-center rounded-full bg-yellow-100 px-2.5 py-0.5 text-xs font-medium text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400">
          Expired
        </span>
      );
    }
    return (
      <span className="inline-flex items-center rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-400">
        Active
      </span>
    );
  };

  const revokeTarget = apiKeys.find((k) => k.id === showRevokeModal);

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <nav className="mb-6 flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
        <Link to="/settings" className="hover:text-brand-600 dark:hover:text-brand-400">
          Settings
        </Link>
        <span>/</span>
        <span className="text-gray-900 dark:text-white">CI/CD Integration</span>
      </nav>

      {/* Header */}
      <div className="mb-8 flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-black dark:text-white">CI/CD Integration</h2>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Manage API keys for CI/CD pipeline integration
          </p>
        </div>
        <button
          onClick={() => setShowCreateModal(true)}
          className="inline-flex items-center gap-2 rounded-lg bg-brand-500 px-4 py-2.5 text-sm font-medium text-white shadow-sm hover:bg-brand-600 transition-colors"
        >
          <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
          </svg>
          Create API Key
        </button>
      </div>

      {/* Error alert */}
      {error && (
        <div className="mb-6 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800 dark:border-red-800 dark:bg-red-900/20 dark:text-red-400">
          {error}
          <button onClick={() => setError(null)} className="ml-2 font-medium underline">
            Dismiss
          </button>
        </div>
      )}

      {/* Created key banner */}
      {createdKey && (
        <div className="mb-6 rounded-lg border border-green-200 bg-green-50 p-4 dark:border-green-800 dark:bg-green-900/20">
          <div className="flex items-start gap-3">
            <svg className="mt-0.5 h-5 w-5 text-green-600 dark:text-green-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            <div className="flex-1">
              <p className="font-semibold text-green-800 dark:text-green-300">
                API key created! Copy it now — it won't be shown again.
              </p>
              <div className="mt-2 flex items-center gap-2">
                <code className="rounded bg-green-100 px-3 py-1.5 font-mono text-sm text-green-900 dark:bg-green-900/40 dark:text-green-200 select-all">
                  {createdKey}
                </code>
                <button
                  onClick={handleCopyKey}
                  className="inline-flex items-center gap-1.5 rounded-md bg-green-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-green-700 transition-colors"
                >
                  {copied ? (
                    <>
                      <svg className="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                      </svg>
                      Copied!
                    </>
                  ) : (
                    <>
                      <svg className="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                      </svg>
                      Copy
                    </>
                  )}
                </button>
              </div>
            </div>
            <button
              onClick={() => setCreatedKey(null)}
              className="text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-200"
            >
              <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>
      )}

      {/* Loading state */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-brand-500 border-t-transparent" />
        </div>
      ) : apiKeys.length === 0 ? (
        /* Empty state */
        <div className="rounded-2xl border border-gray-200 bg-white p-12 text-center dark:border-gray-800 dark:bg-white/[0.03]">
          <svg className="mx-auto h-12 w-12 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z" />
          </svg>
          <h3 className="mt-4 text-lg font-semibold text-gray-900 dark:text-white">No API keys yet</h3>
          <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
            Create an API key to enable CI/CD pipeline integration with webhooks.
          </p>
          <button
            onClick={() => setShowCreateModal(true)}
            className="mt-6 inline-flex items-center gap-2 rounded-lg bg-brand-500 px-4 py-2.5 text-sm font-medium text-white hover:bg-brand-600 transition-colors"
          >
            Create your first API key
          </button>
        </div>
      ) : (
        /* API Keys table */
        <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-800">
            <thead className="bg-gray-50 dark:bg-gray-800/50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Name</th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Permissions</th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Created</th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Last Used</th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Expires</th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Status</th>
                <th className="px-6 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200 dark:divide-gray-800">
              {apiKeys.map((key) => (
                <tr key={key.id} className={key.isRevoked ? "opacity-60" : ""}>
                  <td className="whitespace-nowrap px-6 py-4 text-sm font-medium text-gray-900 dark:text-white">
                    {key.name}
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex flex-wrap gap-1">
                      {key.permissions.map((perm) => (
                        <span
                          key={perm}
                          className="inline-flex items-center rounded-md bg-blue-50 px-2 py-0.5 text-xs font-medium text-blue-700 dark:bg-blue-900/30 dark:text-blue-300"
                        >
                          {perm.split(".")[1]}
                        </span>
                      ))}
                    </div>
                  </td>
                  <td className="whitespace-nowrap px-6 py-4 text-sm text-gray-500 dark:text-gray-400">
                    {formatDate(key.createdAt)}
                  </td>
                  <td className="whitespace-nowrap px-6 py-4 text-sm text-gray-500 dark:text-gray-400">
                    {formatDate(key.lastUsedAt)}
                  </td>
                  <td className="whitespace-nowrap px-6 py-4 text-sm text-gray-500 dark:text-gray-400">
                    {key.expiresAt ? new Date(key.expiresAt).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" }) : "Never"}
                  </td>
                  <td className="whitespace-nowrap px-6 py-4">{getStatusBadge(key)}</td>
                  <td className="whitespace-nowrap px-6 py-4 text-right">
                    {!key.isRevoked && (
                      <button
                        onClick={() => setShowRevokeModal(key.id)}
                        className="text-sm font-medium text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
                      >
                        Revoke
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Create Modal */}
      {showCreateModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl dark:bg-gray-900">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Create API Key</h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
              The API key will be shown only once after creation.
            </p>
            <form onSubmit={handleCreate} className="mt-4 space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300">Name</label>
                <input
                  type="text"
                  value={newKeyName}
                  onChange={(e) => setNewKeyName(e.target.value)}
                  placeholder="e.g., GitHub Actions Deploy"
                  className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                  required
                  autoFocus
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300">Permissions</label>
                <div className="mt-2 space-y-2">
                  {AVAILABLE_PERMISSIONS.map((perm) => (
                    <label key={perm.value} className="flex items-start gap-3 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={newKeyPermissions.includes(perm.value)}
                        onChange={() => togglePermission(perm.value)}
                        className="mt-0.5 h-4 w-4 rounded border-gray-300 text-brand-600 focus:ring-brand-500"
                      />
                      <div>
                        <span className="text-sm font-medium text-gray-900 dark:text-white">{perm.label}</span>
                        <p className="text-xs text-gray-500 dark:text-gray-400">{perm.description}</p>
                      </div>
                    </label>
                  ))}
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300">
                  Expiry (optional)
                </label>
                <input
                  type="date"
                  value={newKeyExpiry}
                  onChange={(e) => setNewKeyExpiry(e.target.value)}
                  className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                />
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-800"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={actionLoading || newKeyPermissions.length === 0}
                  className="rounded-lg bg-brand-500 px-4 py-2 text-sm font-medium text-white hover:bg-brand-600 disabled:opacity-50 transition-colors"
                >
                  {actionLoading ? "Creating..." : "Create"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Revoke confirmation modal */}
      {showRevokeModal && revokeTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl dark:bg-gray-900">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Revoke API Key</h3>
            <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
              Are you sure you want to revoke <strong className="text-gray-900 dark:text-white">{revokeTarget.name}</strong>?
              This action cannot be undone. Any pipelines using this key will stop working.
            </p>
            <div className="mt-4">
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300">Reason (optional)</label>
              <input
                type="text"
                value={revokeReason}
                onChange={(e) => setRevokeReason(e.target.value)}
                placeholder="e.g., Key compromised"
                className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
              />
            </div>
            <div className="mt-6 flex justify-end gap-3">
              <button
                onClick={() => { setShowRevokeModal(null); setRevokeReason(""); }}
                className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-800"
              >
                Cancel
              </button>
              <button
                onClick={handleRevoke}
                disabled={actionLoading}
                className="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50 transition-colors"
              >
                {actionLoading ? "Revoking..." : "Revoke Key"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
