import { useEffect, useState, useCallback } from "react";
import { volumeApi, type Volume } from "../../api/volumes";
import { useEnvironment } from "../../context/EnvironmentContext";

export default function Volumes() {
  const [volumes, setVolumes] = useState<Volume[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showOrphanedOnly, setShowOrphanedOnly] = useState(false);
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
          <button
            onClick={loadVolumes}
            className="inline-flex items-center justify-center gap-2.5 rounded-md bg-primary px-10 py-4 text-center font-medium text-white hover:bg-opacity-90 lg:px-8 xl:px-10"
          >
            Refresh
          </button>
        </div>
      </div>

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

          <div className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5">
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
            <div className="col-span-2 hidden items-center sm:flex">
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
                className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5"
              >
                <div className="col-span-3 flex items-center">
                  <p className="text-sm text-black dark:text-white truncate" title={volume.name}>
                    {volume.name}
                  </p>
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
                <div className="col-span-2 hidden items-center sm:flex">
                  <p className="text-sm text-black dark:text-white">
                    {formatDate(volume.createdAt)}
                  </p>
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
}
