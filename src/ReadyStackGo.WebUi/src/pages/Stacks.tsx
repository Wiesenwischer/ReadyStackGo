import { useEffect, useState } from "react";
import { stackApi, type Stack } from "../api/stacks";

export default function Stacks() {
  const [stacks, setStacks] = useState<Stack[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const loadStacks = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await stackApi.list();
      setStacks(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load stacks");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadStacks();
  }, []);

  const handleDeploy = async (id: string) => {
    try {
      setActionLoading(id);
      await stackApi.deploy(id);
      await loadStacks();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to deploy stack");
    } finally {
      setActionLoading(null);
    }
  };

  const handleRemove = async (id: string) => {
    try {
      setActionLoading(id);
      await stackApi.remove(id);
      await loadStacks();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to remove stack");
    } finally {
      setActionLoading(null);
    }
  };

  const getStatusBadge = (status: string) => {
    const statusLower = status.toLowerCase();
    let colorClasses = "bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300";

    if (statusLower === "running") {
      colorClasses = "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200";
    } else if (statusLower === "deploying") {
      colorClasses = "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200";
    } else if (statusLower === "failed") {
      colorClasses = "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200";
    }

    return (
      <span className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${colorClasses}`}>
        {status}
      </span>
    );
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-title-md2 font-semibold text-black dark:text-white">
          Stack Management
        </h1>
        <button
          onClick={loadStacks}
          className="inline-flex items-center justify-center gap-2.5 rounded-md bg-primary px-10 py-4 text-center font-medium text-white hover:bg-opacity-90 lg:px-8 xl:px-10"
        >
          Refresh
        </button>
      </div>

      {error && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
        </div>
      )}

      <div className="flex flex-col gap-10">
        <div className="rounded-sm border border-stroke bg-white shadow-default dark:border-strokedark dark:bg-boxdark">
          <div className="px-4 py-6 md:px-6 xl:px-7.5">
            <h4 className="text-xl font-semibold text-black dark:text-white">
              Available Stacks
            </h4>
          </div>

          {loading ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-500 dark:text-gray-400">
                Loading stacks...
              </p>
            </div>
          ) : stacks.length === 0 ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-500 dark:text-gray-400">
                No stacks available.
              </p>
            </div>
          ) : (
            <>
              <div className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5">
                <div className="col-span-3 flex items-center">
                  <p className="font-medium">Stack Name</p>
                </div>
                <div className="col-span-2 hidden items-center sm:flex">
                  <p className="font-medium">Services</p>
                </div>
                <div className="col-span-1 flex items-center">
                  <p className="font-medium">Status</p>
                </div>
                <div className="col-span-2 flex items-center">
                  <p className="font-medium">Actions</p>
                </div>
              </div>

              {stacks.map((stack) => (
                <div
                  key={stack.id}
                  className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5"
                >
                  <div className="col-span-3 flex flex-col gap-1">
                    <p className="text-sm text-black dark:text-white font-medium">
                      {stack.name}
                    </p>
                    <p className="text-xs text-gray-500 dark:text-gray-400">
                      {stack.description}
                    </p>
                  </div>

                  <div className="col-span-2 hidden items-center sm:flex">
                    <p className="text-sm text-black dark:text-white">
                      {stack.services.length} service{stack.services.length !== 1 ? 's' : ''}
                    </p>
                  </div>

                  <div className="col-span-1 flex items-center">
                    {getStatusBadge(stack.status)}
                  </div>

                  <div className="col-span-2 flex items-center gap-2">
                    {stack.status === "NotDeployed" ? (
                      <button
                        onClick={() => handleDeploy(stack.id)}
                        disabled={actionLoading === stack.id}
                        className="inline-flex items-center justify-center rounded bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {actionLoading === stack.id ? "Deploying..." : "Deploy"}
                      </button>
                    ) : (
                      <button
                        onClick={() => handleRemove(stack.id)}
                        disabled={actionLoading === stack.id}
                        className="inline-flex items-center justify-center rounded bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {actionLoading === stack.id ? "Removing..." : "Remove"}
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
