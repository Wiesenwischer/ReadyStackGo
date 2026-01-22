import { useEffect, useState, useCallback } from "react";
import { containerApi, type Container } from "../../api/containers";
import { useEnvironment } from "../../context/EnvironmentContext";

export default function Containers() {
  const [containers, setContainers] = useState<Container[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const { activeEnvironment } = useEnvironment();

  const loadContainers = useCallback(async () => {
    if (!activeEnvironment) {
      setContainers([]);
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await containerApi.list(activeEnvironment.id);
      setContainers(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load containers");
    } finally {
      setLoading(false);
    }
  }, [activeEnvironment]);

  useEffect(() => {
    loadContainers();
  }, [loadContainers]);

  const handleStart = async (id: string) => {
    if (!activeEnvironment) return;

    try {
      setActionLoading(id);
      await containerApi.start(activeEnvironment.id, id);
      await loadContainers();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to start container");
    } finally {
      setActionLoading(null);
    }
  };

  const handleStop = async (id: string) => {
    if (!activeEnvironment) return;

    try {
      setActionLoading(id);
      await containerApi.stop(activeEnvironment.id, id);
      await loadContainers();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to stop container");
    } finally {
      setActionLoading(null);
    }
  };

  /**
   * Get combined status badge (like Portainer).
   * Shows "healthy" instead of "running" when health check passes.
   */
  const getCombinedStatusBadge = (state: string, healthStatus: string | undefined) => {
    // If health check exists, use it as primary status (like Portainer)
    if (healthStatus && healthStatus !== "none") {
      switch (healthStatus.toLowerCase()) {
        case "healthy":
          return (
            <span className="inline-flex rounded-full px-3 py-1 text-xs font-medium bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200">
              healthy
            </span>
          );
        case "unhealthy":
          return (
            <span className="inline-flex rounded-full px-3 py-1 text-xs font-medium bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200">
              unhealthy
            </span>
          );
        case "starting":
          return (
            <span className="inline-flex rounded-full px-3 py-1 text-xs font-medium bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200">
              starting
            </span>
          );
      }
    }

    // Fall back to container state
    const stateLower = state.toLowerCase();
    switch (stateLower) {
      case "running":
        return (
          <span className="inline-flex rounded-full px-3 py-1 text-xs font-medium bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200">
            running
          </span>
        );
      case "exited":
      case "dead":
        return (
          <span className="inline-flex rounded-full px-3 py-1 text-xs font-medium bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200">
            {stateLower}
          </span>
        );
      case "paused":
      case "restarting":
        return (
          <span className="inline-flex rounded-full px-3 py-1 text-xs font-medium bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200">
            {stateLower}
          </span>
        );
      default:
        return (
          <span className="inline-flex rounded-full px-3 py-1 text-xs font-medium bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300">
            {state}
          </span>
        );
    }
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-title-md2 font-semibold text-black dark:text-white">
          Container Management
        </h1>
        <button
          onClick={loadContainers}
          className="inline-flex items-center justify-center gap-2.5 rounded-md bg-primary px-10 py-4 text-center font-medium text-white hover:bg-opacity-90 lg:px-8 xl:px-10"
        >
          Refresh
        </button>
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
              All Containers
            </h4>
          </div>

          <div className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5">
            <div className="col-span-3 flex items-center">
              <p className="font-medium text-gray-900 dark:text-gray-200">Container Name</p>
            </div>
            <div className="col-span-2 hidden items-center sm:flex">
              <p className="font-medium text-gray-900 dark:text-gray-200">Image</p>
            </div>
            <div className="col-span-1 flex items-center">
              <p className="font-medium text-gray-900 dark:text-gray-200">Status</p>
            </div>
            <div className="col-span-1 flex items-center">
              <p className="font-medium text-gray-900 dark:text-gray-200">Port</p>
            </div>
            <div className="col-span-1 flex items-center">
              <p className="font-medium text-gray-900 dark:text-gray-200">Actions</p>
            </div>
          </div>

          {loading ? (
            <div className="border-t border-stroke px-4 py-4.5 dark:border-strokedark md:px-6 2xl:px-7.5">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                Loading containers...
              </p>
            </div>
          ) : containers.length === 0 ? (
            <div className="border-t border-stroke px-4 py-4.5 dark:border-strokedark md:px-6 2xl:px-7.5">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                No containers found. Make sure Docker is running.
              </p>
            </div>
          ) : (
            containers.map((container) => (
              <div
                key={container.id}
                className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5"
              >
                <div className="col-span-3 flex items-center">
                  <p className="text-sm text-black dark:text-white">
                    {container.name}
                  </p>
                </div>
                <div className="col-span-2 hidden items-center sm:flex">
                  <p className="text-sm text-black dark:text-white">
                    {container.image}
                  </p>
                </div>
                <div className="col-span-1 flex items-center">
                  {getCombinedStatusBadge(container.state, container.healthStatus)}
                </div>
                <div className="col-span-1 flex items-center">
                  <p className="text-sm text-black dark:text-white">
                    {container.ports[0]
                      ? `${container.ports[0].publicPort}:${container.ports[0].privatePort}`
                      : "-"}
                  </p>
                </div>
                <div className="col-span-1 flex items-center gap-2">
                  {container.state.toLowerCase() === "running" ? (
                    <button
                      onClick={() => handleStop(container.id)}
                      disabled={actionLoading === container.id}
                      className="rounded bg-red-500 px-3 py-1 text-xs font-medium text-white hover:bg-red-600 disabled:opacity-50"
                    >
                      {actionLoading === container.id ? "..." : "Stop"}
                    </button>
                  ) : (
                    <button
                      onClick={() => handleStart(container.id)}
                      disabled={actionLoading === container.id}
                      className="rounded bg-green-500 px-3 py-1 text-xs font-medium text-white hover:bg-green-600 disabled:opacity-50"
                    >
                      {actionLoading === container.id ? "..." : "Start"}
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
