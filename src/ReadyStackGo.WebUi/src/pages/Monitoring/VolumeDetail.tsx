import { useEffect, useState, useCallback } from "react";
import { Link, useParams, useNavigate } from "react-router";
import { volumeApi, type Volume } from "../../api/volumes";
import { useEnvironment } from "../../context/EnvironmentContext";

export default function VolumeDetail() {
  const { name } = useParams<{ name: string }>();
  const navigate = useNavigate();
  const [volume, setVolume] = useState<Volume | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deleteState, setDeleteState] = useState<"idle" | "confirm" | "deleting">("idle");
  const { activeEnvironment } = useEnvironment();

  const loadVolume = useCallback(async () => {
    if (!activeEnvironment || !name) {
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await volumeApi.get(activeEnvironment.id, decodeURIComponent(name));
      setVolume(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load volume");
    } finally {
      setLoading(false);
    }
  }, [activeEnvironment, name]);

  useEffect(() => {
    loadVolume();
  }, [loadVolume]);

  const handleDelete = async () => {
    if (!activeEnvironment || !volume) return;

    try {
      setDeleteState("deleting");
      await volumeApi.remove(activeEnvironment.id, volume.name, volume.containerCount > 0);
      navigate("/volumes");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to remove volume");
      setDeleteState("idle");
    }
  };

  const formatSize = (bytes?: number) => {
    if (bytes === undefined || bytes === null) return "Unknown";
    if (bytes === 0) return "0 B";
    const units = ["B", "KB", "MB", "GB", "TB"];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${units[i]}`;
  };

  const formatDate = (dateStr?: string) => {
    if (!dateStr) return "-";
    const date = new Date(dateStr);
    return date.toLocaleDateString(undefined, {
      year: "numeric",
      month: "long",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
  };

  if (loading) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <p className="text-center text-sm text-gray-600 dark:text-gray-400">
          Loading volume details...
        </p>
      </div>
    );
  }

  if (error && !volume) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="mb-4">
          <Link to="/volumes" className="text-sm text-brand-600 hover:text-brand-700 dark:text-brand-400">
            &larr; Back to Volumes
          </Link>
        </div>
        <div className="rounded-sm border border-red-300 bg-red-50 p-4 text-red-800 dark:border-red-700 dark:bg-red-900 dark:text-red-200">
          {error}
        </div>
      </div>
    );
  }

  if (!volume) return null;

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6">
        <Link to="/volumes" className="text-sm text-brand-600 hover:text-brand-700 dark:text-brand-400">
          &larr; Back to Volumes
        </Link>
      </div>

      {/* Header */}
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
            {volume.name}
          </h2>
          <div className="mt-2 flex items-center gap-2">
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
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={loadVolume}
            className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Refresh
          </button>
          {deleteState === "confirm" ? (
            <div className="flex items-center gap-2">
              <button
                onClick={handleDelete}
                className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700"
              >
                Confirm Remove
              </button>
              <button
                onClick={() => setDeleteState("idle")}
                className="rounded-md bg-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-300 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Cancel
              </button>
            </div>
          ) : deleteState === "deleting" ? (
            <button
              disabled
              className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white opacity-50"
            >
              Removing...
            </button>
          ) : (
            <button
              onClick={() => setDeleteState("confirm")}
              className="rounded-md bg-red-500 px-4 py-2 text-sm font-medium text-white hover:bg-red-600"
            >
              Remove Volume
            </button>
          )}
        </div>
      </div>

      {error && (
        <div className="mb-4 rounded-sm border border-red-300 bg-red-50 p-4 text-red-800 dark:border-red-700 dark:bg-red-900 dark:text-red-200">
          {error}
        </div>
      )}

      {/* Warning for in-use volumes */}
      {deleteState === "confirm" && volume.containerCount > 0 && (
        <div className="mb-4 rounded-2xl border border-amber-200 bg-amber-50 p-4 dark:border-amber-800 dark:bg-amber-900/20">
          <p className="text-sm text-amber-800 dark:text-amber-200">
            This volume is referenced by {volume.containerCount} container(s). Removing it will use force mode.
          </p>
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Volume Information */}
        <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
          <h4 className="mb-4 text-lg font-semibold text-black dark:text-white">
            Volume Information
          </h4>
          <dl className="space-y-3">
            <div className="flex justify-between">
              <dt className="text-sm text-gray-600 dark:text-gray-400">Name</dt>
              <dd className="text-sm font-medium text-black dark:text-white">{volume.name}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-sm text-gray-600 dark:text-gray-400">Driver</dt>
              <dd className="text-sm font-medium text-black dark:text-white">{volume.driver}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-sm text-gray-600 dark:text-gray-400">Scope</dt>
              <dd className="text-sm font-medium text-black dark:text-white">{volume.scope || "-"}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-sm text-gray-600 dark:text-gray-400">Mountpoint</dt>
              <dd className="text-sm font-medium text-black dark:text-white break-all text-right ml-4">
                {volume.mountpoint || "-"}
              </dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-sm text-gray-600 dark:text-gray-400">Size</dt>
              <dd className="text-sm font-medium text-black dark:text-white">{formatSize(volume.sizeBytes)}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-sm text-gray-600 dark:text-gray-400">Created</dt>
              <dd className="text-sm font-medium text-black dark:text-white">{formatDate(volume.createdAt)}</dd>
            </div>
          </dl>
        </div>

        {/* Container References */}
        <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
          <h4 className="mb-4 text-lg font-semibold text-black dark:text-white">
            Referenced by Containers ({volume.containerCount})
          </h4>
          {volume.referencedByContainers.length === 0 ? (
            <p className="text-sm text-gray-600 dark:text-gray-400">
              No containers are using this volume.
            </p>
          ) : (
            <ul className="space-y-2">
              {volume.referencedByContainers.map((containerName) => (
                <li
                  key={containerName}
                  className="flex items-center rounded-md bg-gray-50 px-3 py-2 dark:bg-gray-800/50"
                >
                  <span className="text-sm text-black dark:text-white">{containerName}</span>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Labels */}
        {Object.keys(volume.labels).length > 0 && (
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03] lg:col-span-2">
            <h4 className="mb-4 text-lg font-semibold text-black dark:text-white">
              Labels
            </h4>
            <div className="space-y-2">
              {Object.entries(volume.labels).map(([key, value]) => (
                <div
                  key={key}
                  className="flex items-start gap-2 rounded-md bg-gray-50 px-3 py-2 dark:bg-gray-800/50"
                >
                  <span className="text-sm font-medium text-gray-700 dark:text-gray-300">{key}:</span>
                  <span className="text-sm text-black dark:text-white break-all">{value}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
