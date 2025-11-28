import { useEffect, useState } from "react";
import { listDeployments, removeDeployment, type DeploymentSummary } from "../api/deployments";
import { getStackDefinitions, syncSources, type StackDefinition } from "../api/stackSources";
import { useEnvironment } from "../context/EnvironmentContext";
import DeployComposeModal from "../components/stacks/DeployComposeModal";

export default function Stacks() {
  const { activeEnvironment } = useEnvironment();
  const [deployments, setDeployments] = useState<DeploymentSummary[]>([]);
  const [stackDefinitions, setStackDefinitions] = useState<StackDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [stacksLoading, setStacksLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [syncing, setSyncing] = useState(false);
  const [isDeployModalOpen, setIsDeployModalOpen] = useState(false);
  const [selectedStack, setSelectedStack] = useState<StackDefinition | null>(null);

  const loadDeployments = async () => {
    if (!activeEnvironment) {
      setDeployments([]);
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const response = await listDeployments(activeEnvironment.id);
      if (response.success) {
        setDeployments(response.deployments);
      } else {
        setError("Failed to load deployments");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load deployments");
    } finally {
      setLoading(false);
    }
  };

  const loadStackDefinitions = async () => {
    try {
      setStacksLoading(true);
      const stacks = await getStackDefinitions();
      setStackDefinitions(stacks);
    } catch (err) {
      console.error("Failed to load stack definitions:", err);
      // Don't set error for stack definitions as deployments might still work
    } finally {
      setStacksLoading(false);
    }
  };

  const handleSync = async () => {
    try {
      setSyncing(true);
      await syncSources();
      await loadStackDefinitions();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to sync sources");
    } finally {
      setSyncing(false);
    }
  };

  useEffect(() => {
    loadDeployments();
    loadStackDefinitions();
  }, [activeEnvironment]);

  const handleRemove = async (stackName: string) => {
    if (!activeEnvironment) return;

    try {
      setActionLoading(stackName);
      await removeDeployment(activeEnvironment.id, stackName);
      await loadDeployments();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to remove deployment");
    } finally {
      setActionLoading(null);
    }
  };

  const handleDeploy = (stack: StackDefinition) => {
    setSelectedStack(stack);
    setIsDeployModalOpen(true);
  };

  const getStatusBadge = (status: string | null | undefined) => {
    const statusLower = (status || "unknown").toLowerCase();
    let colorClasses = "bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300";

    if (statusLower === "running") {
      colorClasses = "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200";
    } else if (statusLower === "deploying") {
      colorClasses = "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200";
    } else if (statusLower === "failed" || statusLower === "error") {
      colorClasses = "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200";
    } else if (statusLower === "stopped") {
      colorClasses = "bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200";
    }

    return (
      <span className={`inline-flex rounded-full px-3 py-1 text-xs font-medium ${colorClasses}`}>
        {status || "unknown"}
      </span>
    );
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString() + " " + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-title-md2 font-semibold text-black dark:text-white">
          Stack Management
        </h1>
        <div className="flex gap-3">
          <button
            onClick={() => {
              setSelectedStack(null);
              setIsDeployModalOpen(true);
            }}
            disabled={!activeEnvironment}
            className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            Deploy Custom
          </button>
          <button
            onClick={handleSync}
            disabled={syncing}
            className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 disabled:opacity-50"
          >
            <svg className={`w-5 h-5 ${syncing ? 'animate-spin' : ''}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {syncing ? "Syncing..." : "Sync Sources"}
          </button>
        </div>
      </div>

      {!activeEnvironment && (
        <div className="mb-6 rounded-md bg-yellow-50 p-4 dark:bg-yellow-900/20">
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            No environment selected. Please select an environment to view and manage deployments.
          </p>
        </div>
      )}

      {error && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
        </div>
      )}

      <div className="flex flex-col gap-10">
        {/* Available Stacks Section */}
        <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="px-4 py-6 md:px-6 xl:px-7.5">
            <h4 className="text-xl font-semibold text-black dark:text-white">
              Available Stacks
            </h4>
            <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">
              Stack definitions from configured sources
            </p>
          </div>

          {stacksLoading ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                Loading available stacks...
              </p>
            </div>
          ) : stackDefinitions.length === 0 ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                No stack definitions available. Click "Sync Sources" to load stacks from configured sources.
              </p>
            </div>
          ) : (
            <div className="border-t border-stroke p-4 dark:border-strokedark">
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                {stackDefinitions.map((stack) => (
                  <div
                    key={stack.id}
                    className="rounded-lg border border-gray-200 bg-gray-50 p-4 dark:border-gray-700 dark:bg-gray-800/50"
                  >
                    <div className="mb-3 flex items-start justify-between">
                      <div>
                        <h5 className="font-semibold text-gray-900 dark:text-white">
                          {stack.name}
                        </h5>
                        <p className="text-xs text-gray-500 dark:text-gray-400">
                          {stack.relativePath ? `${stack.sourceName} / ${stack.relativePath}` : stack.sourceName}
                        </p>
                      </div>
                      <button
                        onClick={() => handleDeploy(stack)}
                        disabled={!activeEnvironment}
                        className="rounded bg-brand-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        Deploy
                      </button>
                    </div>

                    {stack.description && (
                      <p className="mb-3 text-sm text-gray-600 dark:text-gray-400 whitespace-pre-line line-clamp-2">
                        {stack.description}
                      </p>
                    )}

                    <div className="flex flex-wrap gap-2 text-xs">
                      <span className="inline-flex items-center rounded bg-blue-100 px-2 py-1 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300">
                        {stack.services.length} service{stack.services.length !== 1 ? 's' : ''}
                      </span>
                      {stack.variables.length > 0 && (
                        <span className="inline-flex items-center rounded bg-purple-100 px-2 py-1 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300">
                          {stack.variables.length} config{stack.variables.length !== 1 ? 's' : ''}
                        </span>
                      )}
                      {stack.version && (
                        <span className="inline-flex items-center rounded bg-gray-200 px-2 py-1 text-gray-700 dark:bg-gray-700 dark:text-gray-300">
                          v{stack.version.substring(0, 8)}
                        </span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Deployed Stacks Section */}
        <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="px-4 py-6 md:px-6 xl:px-7.5">
            <h4 className="text-xl font-semibold text-black dark:text-white">
              Deployed Stacks {activeEnvironment && `(${activeEnvironment.name})`}
            </h4>
          </div>

          {loading ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                Loading deployments...
              </p>
            </div>
          ) : !activeEnvironment ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                Select an environment to view deployments.
              </p>
            </div>
          ) : deployments.length === 0 ? (
            <div className="border-t border-stroke px-4 py-8 dark:border-strokedark">
              <p className="text-center text-sm text-gray-600 dark:text-gray-400">
                No deployments in this environment. Deploy a stack from the available stacks above.
              </p>
            </div>
          ) : (
            <>
              <div className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5">
                <div className="col-span-2 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Stack Name</p>
                </div>
                <div className="col-span-1 hidden items-center sm:flex">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Version</p>
                </div>
                <div className="col-span-1 hidden items-center sm:flex">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Services</p>
                </div>
                <div className="col-span-2 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Deployed At</p>
                </div>
                <div className="col-span-1 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Status</p>
                </div>
                <div className="col-span-1 flex items-center">
                  <p className="font-medium text-gray-900 dark:text-gray-200">Actions</p>
                </div>
              </div>

              {deployments.map((deployment) => (
                <div
                  key={deployment.deploymentId || deployment.stackName}
                  className="grid grid-cols-6 border-t border-stroke px-4 py-4.5 dark:border-strokedark sm:grid-cols-8 md:px-6 2xl:px-7.5"
                >
                  <div className="col-span-2 flex items-center">
                    <p className="text-sm text-gray-900 dark:text-white font-medium">
                      {deployment.stackName}
                    </p>
                  </div>

                  <div className="col-span-1 hidden items-center sm:flex">
                    <p className="text-sm text-gray-600 dark:text-gray-400">
                      {deployment.stackVersion || "-"}
                    </p>
                  </div>

                  <div className="col-span-1 hidden items-center sm:flex">
                    <p className="text-sm text-gray-900 dark:text-gray-300">
                      {deployment.serviceCount} service{deployment.serviceCount !== 1 ? 's' : ''}
                    </p>
                  </div>

                  <div className="col-span-2 flex items-center">
                    <p className="text-sm text-gray-600 dark:text-gray-400">
                      {formatDate(deployment.deployedAt)}
                    </p>
                  </div>

                  <div className="col-span-1 flex items-center">
                    {getStatusBadge(deployment.status)}
                  </div>

                  <div className="col-span-1 flex items-center">
                    <button
                      onClick={() => handleRemove(deployment.stackName)}
                      disabled={actionLoading === deployment.stackName}
                      className="inline-flex items-center justify-center rounded bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      {actionLoading === deployment.stackName ? "..." : "Remove"}
                    </button>
                  </div>
                </div>
              ))}
            </>
          )}
        </div>
      </div>

      <DeployComposeModal
        isOpen={isDeployModalOpen}
        onClose={() => {
          setIsDeployModalOpen(false);
          setSelectedStack(null);
        }}
        onDeploySuccess={() => {
          loadDeployments();
          setSelectedStack(null);
        }}
        preloadedStack={selectedStack}
      />
    </div>
  );
}
