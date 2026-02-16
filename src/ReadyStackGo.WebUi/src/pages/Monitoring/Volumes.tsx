import { useEffect, useState, useCallback } from "react";
import { Link } from "react-router";
import { volumeApi, type Volume } from "../../api/volumes";
import { useEnvironment } from "../../context/EnvironmentContext";

export default function Volumes() {
  const [volumes, setVolumes] = useState<Volume[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showOrphanedOnly, setShowOrphanedOnly] = useState(false);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [confirmBulkDelete, setConfirmBulkDelete] = useState(false);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [createName, setCreateName] = useState("");
  const [createDriver, setCreateDriver] = useState("");
  const [createLoading, setCreateLoading] = useState(false);
  const { activeEnvironment } = useEnvironment();

  const loadVolumes = useCallback(async () => {
    if (!activeEnvironment) {
      setVolumes([]);
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await volumeApi.list(activeEnvironment.id);
      setVolumes(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load volumes");
    } finally {
      setLoading(false);
    }
  }, [activeEnvironment]);

  useEffect(() => {
    loadVolumes();
  }, [loadVolumes]);

  const handleDelete = async (name: string, force: boolean = false) => {
    if (!activeEnvironment) return;

    try {
      setActionLoading(name);
      await volumeApi.remove(activeEnvironment.id, name, force);
      setConfirmDelete(null);
      await loadVolumes();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to remove volume");
    } finally {
      setActionLoading(null);
    }
  };

  const handleBulkDeleteOrphaned = async () => {
    if (!activeEnvironment) return;

    const orphaned = volumes.filter((v) => v.isOrphaned);
    setConfirmBulkDelete(false);
    setActionLoading("bulk");

    try {
      for (const vol of orphaned) {
        await volumeApi.remove(activeEnvironment.id, vol.name, false);
      }
      await loadVolumes();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to remove orphaned volumes");
    } finally {
      setActionLoading(null);
    }
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeEnvironment || !createName.trim()) return;

    try {
      setCreateLoading(true);
      setError(null);
      await volumeApi.create(activeEnvironment.id, {
        name: createName.trim(),
        driver: createDriver.trim() || undefined,
      });
      setCreateName("");
      setCreateDriver("");
      setShowCreateForm(false);
      await loadVolumes();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create volume");
    } finally {
      setCreateLoading(false);
    }
  };

  const filteredVolumes = showOrphanedOnly
    ? volumes.filter((v) => v.isOrphaned)
    : volumes;

  const orphanedCount = volumes.filter((v) => v.isOrphaned).length;

  const formatDate = (dateStr?: string) => {
    if (!dateStr) return "-";
    const date = new Date(dateStr);
    return date.toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-title-md2 font-semibold text-black dark:text-white">
          Volume Management
        </h1>
        <div className="flex items-center gap-3">
          {orphanedCount > 0 && (
            <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 cursor-pointer">
              <input
                type="checkbox"
                checked={showOrphanedOnly}
                onChange={(e) => setShowOrphanedOnly(e.target.checked)}
                className="rounded border-gray-300 dark:border-gray-600"
              />
              Orphaned only ({orphanedCount})
            </label>
          )}
          {orphanedCount > 0 && (
            <button
              onClick={() => setConfirmBulkDelete(true)}
              disabled={actionLoading === "bulk"}
              className="rounded-md bg-red-500 px-4 py-2 text-sm font-medium text-white hover:bg-red-600 disabled:opacity-50"
            >
              {actionLoading === "bulk" ? "Removing..." : `Remove Orphaned (${orphanedCount})`}
            </button>
          )}
          <button
            onClick={() => setShowCreateForm(!showCreateForm)}
            className="rounded-md bg-brand-500 px-4 py-2 text-sm font-medium text-white hover:bg-brand-600"
          >
            Create Volume
          </button>
          <button
            onClick={loadVolumes}
            className="inline-flex items-center justify-center gap-2.5 rounded-md bg-primary px-10 py-4 text-center font-medium text-white hover:bg-opacity-90 lg:px-8 xl:px-10"
          >
            Refresh
          </button>
        </div>
      </div>

      {/* Bulk delete confirmation */}
      {confirmBulkDelete && (
        <div className="mb-4 rounded-2xl border border-red-200 bg-red-50 p-4 dark:border-red-800 dark:bg-red-900/20">
          <div className="flex items-center justify-between">
            <p className="text-sm text-red-800 dark:text-red-200">
              Are you sure you want to remove all {orphanedCount} orphaned volumes? This action cannot be undone.
            </p>
            <div className="flex items-center gap-2 ml-4">
              <button
                onClick={() => setConfirmBulkDelete(false)}
                className="rounded-md bg-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-300 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Cancel
              </button>
              <button
                onClick={handleBulkDeleteOrphaned}
                className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-700"
              >
                Remove All Orphaned
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Create volume form */}
      {showCreateForm && (
        <div className="mb-4 rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
          <h4 className="mb-4 text-lg font-semibold text-black dark:text-white">
            Create New Volume
          </h4>
          <form onSubmit={handleCreate} className="flex flex-col gap-4 sm:flex-row sm:items-end">
            <div className="flex-1">
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Volume Name
              </label>
              <input
                type="text"
                value={createName}
                onChange={(e) => setCreateName(e.target.value)}
                placeholder="my-volume"
                required
                className="w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-black dark:border-gray-600 dark:bg-gray-800 dark:text-white"
              />
            </div>
            <div className="sm:w-48">
              <label className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">
                Driver (optional)
              </label>
              <input
                type="text"
                value={createDriver}
                onChange={(e) => setCreateDriver(e.target.value)}
                placeholder="local"
                className="w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-black dark:border-gray-600 dark:bg-gray-800 dark:text-white"
              />
            </div>
            <div className="flex gap-2">
              <button
                type="submit"
                disabled={createLoading || !createName.trim()}
                className="rounded-md bg-brand-500 px-4 py-2 text-sm font-medium text-white hover:bg-brand-600 disabled:opacity-50"
              >
                {createLoading ? "Creating..." : "Create"}
              </button>
              <button
                type="button"
                onClick={() => { setShowCreateForm(false); setCreateName(""); setCreateDriver(""); }}
                className="rounded-md bg-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-300 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}

      {error && (
        <div className="mb-4 rounded-sm border border-red-300 bg-red-50 p-4 text-red-800 dark:border-red-700 dark:bg-red-900 dark:text-red-200">
          {error}
        </div>
      )}

      <div className="flex flex-col gap-10">
        <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="px-4 py-6 md:px-6 xl:px-7.5">
            <h4 className="text-xl font-semibold text-black dark:text-white">
              All Volumes
            </h4>
          </div>

          <div className="grid grid-cols-7 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-9 md:px-6 2xl:px-7.5">
            <div className="col-span-3 flex items-center">
              <p className="font-medium text-gray-900 dark:text-gray-200">Name</p>
            </div>
            <div className="col-span-1 hidden items-center sm:flex">
              <p className="font-medium text-gray-900 dark:text-gray-200">Driver</p>
            </div>
            <div className="col-span-1 flex items-center">
              <p className="font-medium text-gray-900 dark:text-gray-200">Containers</p>
            </div>
            <div className="col-span-1 flex items-center">
              <p className="font-medium text-gray-900 dark:text-gray-200">Status</p>
            </div>
            <div className="col-span-1 hidden items-center sm:flex">
              <p className="font-medium text-gray-900 dark:text-gray-200">Created</p>
            </div>
          </div>

          {loading ? (
            <div className="border-t border-stroke px-4 py-4.5 dark:border-strokedark md:px-6 2xl:px-7.5">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                Loading volumes...
              </p>
            </div>
          ) : filteredVolumes.length === 0 ? (
            <div className="border-t border-stroke px-4 py-4.5 dark:border-strokedark md:px-6 2xl:px-7.5">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                {showOrphanedOnly
                  ? "No orphaned volumes found."
                  : "No volumes found. Make sure Docker is running."}
              </p>
            </div>
          ) : (
            filteredVolumes.map((volume) => (
              <div
                key={volume.name}
                className="grid grid-cols-7 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-9 md:px-6 2xl:px-7.5 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors"
              >
                <div className="col-span-3 flex items-center">
                  <Link
                    to={`/volumes/${encodeURIComponent(volume.name)}`}
                    className="text-sm text-brand-600 hover:text-brand-700 dark:text-brand-400 dark:hover:text-brand-300 truncate"
                    title={volume.name}
                  >
                    {volume.name}
                  </Link>
                </div>
                <div className="col-span-1 hidden items-center sm:flex">
                  <p className="text-sm text-black dark:text-white">
                    {volume.driver}
                  </p>
                </div>
                <div className="col-span-1 flex items-center">
                  <p className="text-sm text-black dark:text-white">
                    {volume.containerCount}
                  </p>
                </div>
                <div className="col-span-1 flex items-center">
                  {volume.isOrphaned ? (
                    <span className="inline-flex rounded-full px-3 py-1 text-xs font-medium bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200">
                      orphaned
                    </span>
                  ) : (
                    <span className="inline-flex rounded-full px-3 py-1 text-xs font-medium bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200">
                      in use
                    </span>
                  )}
                </div>
                <div className="col-span-1 hidden items-center sm:flex">
                  <p className="text-sm text-black dark:text-white">
                    {formatDate(volume.createdAt)}
                  </p>
                </div>
                {/* Delete button - only visible on wider screens in last column */}
                <div className="col-span-1 flex items-center justify-end gap-1">
                  {confirmDelete === volume.name ? (
                    <>
                      <button
                        onClick={() => handleDelete(volume.name, volume.containerCount > 0)}
                        disabled={actionLoading === volume.name}
                        className="rounded bg-red-600 px-2 py-1 text-xs font-medium text-white hover:bg-red-700 disabled:opacity-50"
                      >
                        {actionLoading === volume.name ? "..." : "Confirm"}
                      </button>
                      <button
                        onClick={() => setConfirmDelete(null)}
                        className="rounded bg-gray-200 px-2 py-1 text-xs font-medium text-gray-700 hover:bg-gray-300 dark:bg-gray-700 dark:text-gray-300"
                      >
                        Cancel
                      </button>
                    </>
                  ) : (
                    <button
                      onClick={() => setConfirmDelete(volume.name)}
                      className="rounded bg-red-500 px-3 py-1 text-xs font-medium text-white hover:bg-red-600"
                    >
                      Remove
                    </button>
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
}
