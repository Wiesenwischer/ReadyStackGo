import { useEffect, useState, useCallback } from "react";
import { useParams, Link } from "react-router";
import {
  getProductDeployment,
  stopProductContainers,
  restartProductContainers,
  type GetProductDeploymentResponse,
  type ProductStackDeploymentDto,
} from "../../api/deployments";
import { useEnvironment } from "../../context/EnvironmentContext";

function getProductStatusPresentation(status: string) {
  switch (status) {
    case 'Running':
      return { label: 'Running', bgColor: 'bg-green-100 dark:bg-green-900/30', textColor: 'text-green-800 dark:text-green-300' };
    case 'Deploying':
    case 'Upgrading':
      return { label: status, bgColor: 'bg-brand-100 dark:bg-brand-900/30', textColor: 'text-brand-800 dark:text-brand-300' };
    case 'Failed':
      return { label: 'Failed', bgColor: 'bg-red-100 dark:bg-red-900/30', textColor: 'text-red-800 dark:text-red-300' };
    case 'PartiallyRunning':
      return { label: 'Partially Running', bgColor: 'bg-yellow-100 dark:bg-yellow-900/30', textColor: 'text-yellow-800 dark:text-yellow-300' };
    case 'Removing':
      return { label: 'Removing', bgColor: 'bg-gray-100 dark:bg-gray-700', textColor: 'text-gray-700 dark:text-gray-300' };
    case 'Removed':
      return { label: 'Removed', bgColor: 'bg-gray-100 dark:bg-gray-700', textColor: 'text-gray-500 dark:text-gray-400' };
    default:
      return { label: status, bgColor: 'bg-gray-100 dark:bg-gray-700', textColor: 'text-gray-700 dark:text-gray-300' };
  }
}

function getStackStatusPresentation(status: string) {
  switch (status) {
    case 'Running':
      return { label: 'Running', bgColor: 'bg-green-100 dark:bg-green-900/30', textColor: 'text-green-800 dark:text-green-300' };
    case 'Deploying':
      return { label: 'Deploying', bgColor: 'bg-brand-100 dark:bg-brand-900/30', textColor: 'text-brand-800 dark:text-brand-300' };
    case 'Pending':
      return { label: 'Pending', bgColor: 'bg-gray-100 dark:bg-gray-700', textColor: 'text-gray-600 dark:text-gray-400' };
    case 'Failed':
      return { label: 'Failed', bgColor: 'bg-red-100 dark:bg-red-900/30', textColor: 'text-red-800 dark:text-red-300' };
    case 'Removed':
      return { label: 'Removed', bgColor: 'bg-gray-100 dark:bg-gray-700', textColor: 'text-gray-500 dark:text-gray-400' };
    default:
      return { label: status, bgColor: 'bg-gray-100 dark:bg-gray-700', textColor: 'text-gray-700 dark:text-gray-300' };
  }
}

const formatDate = (dateString: string) => {
  const date = new Date(dateString);
  return date.toLocaleDateString() + " " + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
};

const formatDuration = (seconds?: number): string => {
  if (seconds == null) return "-";
  const rounded = Math.round(seconds);
  if (rounded < 60) return `${rounded}s`;
  const minutes = Math.floor(rounded / 60);
  const remainingSeconds = rounded % 60;
  return remainingSeconds > 0 ? `${minutes}m ${remainingSeconds}s` : `${minutes}m`;
};

const formatStackDuration = (startedAt?: string, completedAt?: string): string => {
  if (!startedAt || !completedAt) return "-";
  const seconds = (new Date(completedAt).getTime() - new Date(startedAt).getTime()) / 1000;
  return formatDuration(seconds);
};

export default function ProductDeploymentDetail() {
  const { productDeploymentId } = useParams<{ productDeploymentId: string }>();
  const { activeEnvironment } = useEnvironment();
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showVariables, setShowVariables] = useState(false);
  const [containerAction, setContainerAction] = useState<'stop' | 'restart' | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [actionResult, setActionResult] = useState<{ success: boolean; message: string } | null>(null);

  const loadDeployment = useCallback(async () => {
    if (!activeEnvironment || !productDeploymentId) {
      setError(!activeEnvironment ? "No active environment" : "No deployment ID provided");
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await getProductDeployment(activeEnvironment.id, productDeploymentId);
      setDeployment(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load product deployment");
    } finally {
      setLoading(false);
    }
  }, [activeEnvironment, productDeploymentId]);

  const handleContainerAction = useCallback(async (action: 'stop' | 'restart') => {
    if (!activeEnvironment || !productDeploymentId) return;
    setContainerAction(null);
    setActionLoading(true);
    setActionResult(null);
    try {
      const result = action === 'stop'
        ? await stopProductContainers(activeEnvironment.id, productDeploymentId)
        : await restartProductContainers(activeEnvironment.id, productDeploymentId);
      setActionResult({ success: result.success, message: result.message ?? `${action === 'stop' ? 'Stop' : 'Restart'} completed` });
      await loadDeployment();
    } catch (err) {
      setActionResult({ success: false, message: err instanceof Error ? err.message : `Failed to ${action} containers` });
    } finally {
      setActionLoading(false);
    }
  }, [activeEnvironment, productDeploymentId, loadDeployment]);

  useEffect(() => {
    loadDeployment();
  }, [loadDeployment]);

  if (loading) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="animate-pulse">
          <div className="h-8 bg-gray-200 dark:bg-gray-700 rounded w-1/3 mb-4" />
          <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded w-1/2 mb-8" />
          <div className="space-y-4">
            <div className="h-32 bg-gray-200 dark:bg-gray-700 rounded" />
            <div className="h-48 bg-gray-200 dark:bg-gray-700 rounded" />
          </div>
        </div>
      </div>
    );
  }

  if (error || !deployment) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to="/deployments"
            className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back to Deployments
          </Link>
        </div>
        <div className="rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">
            {error || "Product deployment not found"}
          </p>
        </div>
      </div>
    );
  }

  const status = getProductStatusPresentation(deployment.status);
  const variableEntries = Object.entries(deployment.sharedVariables ?? {});

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6">
        <Link
          to="/deployments"
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back to Deployments
        </Link>
      </div>

      {/* Header */}
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-3">
            <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
              {deployment.productDisplayName}
            </h2>
            <span className="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600 dark:bg-gray-700 dark:text-gray-300">
              v{deployment.productVersion}
            </span>
            <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${status.bgColor} ${status.textColor}`}>
              {status.label}
            </span>
          </div>
          <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">
            <span className="font-mono text-xs">{deployment.deploymentName}</span>
            <span className="mx-2">·</span>
            Deployed {formatDate(deployment.createdAt)}
            {activeEnvironment && (
              <>
                <span className="mx-2">·</span>
                {activeEnvironment.name}
              </>
            )}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Link
            to={`/catalog/${encodeURIComponent(deployment.productGroupId)}`}
            className="inline-flex items-center justify-center rounded bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
          >
            View in Catalog
          </Link>
          {deployment.canRetry && (
            <Link
              to={`/retry-product/${deployment.productDeploymentId}`}
              className="inline-flex items-center justify-center rounded bg-yellow-100 px-3 py-1.5 text-sm font-medium text-yellow-700 hover:bg-yellow-200 dark:bg-yellow-900/30 dark:text-yellow-400 dark:hover:bg-yellow-900/50"
            >
              Retry Failed
            </Link>
          )}
          {deployment.canUpgrade && (
            <Link
              to={`/upgrade-product/${deployment.productDeploymentId}`}
              className="inline-flex items-center justify-center rounded bg-brand-100 px-3 py-1.5 text-sm font-medium text-brand-700 hover:bg-brand-200 dark:bg-brand-900/30 dark:text-brand-400 dark:hover:bg-brand-900/50"
            >
              Upgrade
            </Link>
          )}
          {deployment.canStop && (
            <button
              onClick={() => setContainerAction('stop')}
              disabled={actionLoading}
              className="inline-flex items-center justify-center rounded bg-orange-100 px-3 py-1.5 text-sm font-medium text-orange-700 hover:bg-orange-200 dark:bg-orange-900/30 dark:text-orange-400 dark:hover:bg-orange-900/50 disabled:opacity-50"
            >
              Stop Containers
            </button>
          )}
          {deployment.canRestart && (
            <button
              onClick={() => setContainerAction('restart')}
              disabled={actionLoading}
              className="inline-flex items-center justify-center rounded bg-amber-100 px-3 py-1.5 text-sm font-medium text-amber-700 hover:bg-amber-200 dark:bg-amber-900/30 dark:text-amber-400 dark:hover:bg-amber-900/50 disabled:opacity-50"
            >
              Restart Containers
            </button>
          )}
          {deployment.canRemove && (
            <Link
              to={`/remove-product/${deployment.productDeploymentId}`}
              className="inline-flex items-center justify-center rounded bg-red-100 px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-200 dark:bg-red-900/30 dark:text-red-400 dark:hover:bg-red-900/50"
            >
              Remove
            </Link>
          )}
        </div>
      </div>

      {/* Confirmation Dialog */}
      {containerAction && (
        <div className="mb-6 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
          <p className="text-sm font-medium text-gray-900 dark:text-white">
            {containerAction === 'stop'
              ? `Stop all containers of "${deployment.productDisplayName}"?`
              : `Restart all containers of "${deployment.productDisplayName}"?`}
          </p>
          <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
            {containerAction === 'stop'
              ? 'This will stop all running containers. The deployment status will not change.'
              : 'This will stop and start all containers sequentially. The deployment status will not change.'}
          </p>
          <div className="mt-3 flex items-center gap-2">
            <button
              onClick={() => handleContainerAction(containerAction)}
              className={`inline-flex items-center rounded px-3 py-1.5 text-sm font-medium text-white ${
                containerAction === 'stop'
                  ? 'bg-orange-600 hover:bg-orange-700'
                  : 'bg-amber-600 hover:bg-amber-700'
              }`}
            >
              {containerAction === 'stop' ? 'Stop Containers' : 'Restart Containers'}
            </button>
            <button
              onClick={() => setContainerAction(null)}
              className="inline-flex items-center rounded px-3 py-1.5 text-sm font-medium text-gray-700 bg-gray-100 hover:bg-gray-200 dark:text-gray-300 dark:bg-gray-700 dark:hover:bg-gray-600"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {/* Action Loading */}
      {actionLoading && (
        <div className="mb-6 rounded-lg border border-brand-200 bg-brand-50 p-4 dark:border-brand-800 dark:bg-brand-900/20">
          <div className="flex items-center gap-2">
            <svg className="w-4 h-4 animate-spin text-brand-600" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            <span className="text-sm text-brand-800 dark:text-brand-300">Processing...</span>
          </div>
        </div>
      )}

      {/* Action Result */}
      {actionResult && (
        <div className={`mb-6 rounded-lg p-4 ${
          actionResult.success
            ? 'bg-green-50 border border-green-200 dark:bg-green-900/20 dark:border-green-800'
            : 'bg-red-50 border border-red-200 dark:bg-red-900/20 dark:border-red-800'
        }`}>
          <div className="flex items-center justify-between">
            <p className={`text-sm ${
              actionResult.success
                ? 'text-green-800 dark:text-green-300'
                : 'text-red-800 dark:text-red-300'
            }`}>
              {actionResult.message}
            </p>
            <button
              onClick={() => setActionResult(null)}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>
      )}

      {/* Error Alert */}
      {deployment.errorMessage && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm font-medium text-red-800 dark:text-red-200">Deployment Error</p>
          <p className="mt-1 text-sm text-red-700 dark:text-red-300">{deployment.errorMessage}</p>
        </div>
      )}

      {/* Overview Cards */}
      <div className="mb-6 grid gap-4 md:grid-cols-4">
        <OverviewCard label="Status">
          <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${status.bgColor} ${status.textColor}`}>
            {status.label}
          </span>
        </OverviewCard>
        <OverviewCard label="Deployed">
          <span className="text-sm text-gray-900 dark:text-white">{formatDate(deployment.createdAt)}</span>
          {deployment.completedAt && (
            <span className="text-xs text-gray-500 dark:text-gray-400 block">
              Completed {formatDate(deployment.completedAt)}
            </span>
          )}
        </OverviewCard>
        <OverviewCard label="Duration">
          <span className="text-sm font-mono text-gray-900 dark:text-white">
            {formatDuration(deployment.durationSeconds)}
          </span>
        </OverviewCard>
        <OverviewCard label="Stacks">
          <span className="text-sm text-gray-900 dark:text-white">
            {deployment.completedStacks}/{deployment.totalStacks} completed
          </span>
          {deployment.failedStacks > 0 && (
            <span className="text-xs text-red-600 dark:text-red-400 block">
              {deployment.failedStacks} failed
            </span>
          )}
        </OverviewCard>
      </div>

      {/* Upgrade Info */}
      {deployment.upgradeCount > 0 && (
        <div className="mb-6 rounded-lg border border-brand-200 bg-brand-50 p-3 dark:border-brand-800 dark:bg-brand-900/20">
          <p className="text-sm text-brand-800 dark:text-brand-300">
            Upgraded {deployment.upgradeCount} time{deployment.upgradeCount !== 1 ? 's' : ''}
            {deployment.previousVersion && (
              <> from <span className="font-mono">v{deployment.previousVersion}</span></>
            )}
          </p>
        </div>
      )}

      {/* Stacks Table */}
      <div className="mb-6 rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-5 md:px-6">
          <h4 className="text-lg font-semibold text-gray-900 dark:text-white">
            Stacks ({deployment.stacks.length})
          </h4>
        </div>
        <div className="border-t border-gray-200 dark:border-gray-700 divide-y divide-gray-200 dark:divide-gray-700">
          {deployment.stacks
            .sort((a, b) => a.order - b.order)
            .map((stack) => (
              <StackRow key={stack.stackId} stack={stack} />
            ))}
        </div>
      </div>

      {/* Shared Variables */}
      {variableEntries.length > 0 && (
        <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <button
            onClick={() => setShowVariables(!showVariables)}
            className="w-full px-4 py-5 md:px-6 flex items-center justify-between text-left"
          >
            <h4 className="text-lg font-semibold text-gray-900 dark:text-white">
              Shared Variables ({variableEntries.length})
            </h4>
            <svg
              className={`w-5 h-5 text-gray-500 transition-transform ${showVariables ? 'rotate-180' : ''}`}
              fill="none" stroke="currentColor" viewBox="0 0 24 24"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </button>
          {showVariables && (
            <div className="border-t border-gray-200 dark:border-gray-700 p-4 md:p-6">
              <div className="grid gap-3 md:grid-cols-2">
                {variableEntries.map(([key, value]) => (
                  <div key={key} className="rounded-lg bg-gray-50 p-3 dark:bg-gray-800/50">
                    <div className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">{key}</div>
                    <div className="mt-1 text-sm font-mono text-gray-900 dark:text-white break-all">
                      {value || <span className="text-gray-400 italic">not set</span>}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Sub-components
// ============================================================================

function OverviewCard({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-white/[0.03]">
      <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">{label}</p>
      {children}
    </div>
  );
}

function StackRow({ stack }: { stack: ProductStackDeploymentDto }) {
  const status = getStackStatusPresentation(stack.status);
  const displayName = stack.stackDisplayName || stack.stackName;
  const canDrillDown = stack.deploymentStackName;

  const content = (
    <div className="px-4 py-4 md:px-6 flex items-center justify-between gap-4">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-3">
          <span className="text-xs font-mono text-gray-400 dark:text-gray-500 w-6 text-right">
            #{stack.order}
          </span>
          <span className="font-medium text-gray-900 dark:text-white truncate">
            {displayName}
          </span>
          <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${status.bgColor} ${status.textColor}`}>
            {status.label}
          </span>
          {stack.isNewInUpgrade && (
            <span className="inline-flex items-center rounded-full bg-brand-100 px-2 py-0.5 text-xs font-medium text-brand-700 dark:bg-brand-900/30 dark:text-brand-400">
              New
            </span>
          )}
        </div>
        <div className="mt-1 ml-9 flex items-center gap-4 text-xs text-gray-500 dark:text-gray-400">
          <span>{stack.serviceCount} service{stack.serviceCount !== 1 ? 's' : ''}</span>
          {stack.startedAt && (
            <>
              <span>·</span>
              <span>Started {formatDate(stack.startedAt)}</span>
            </>
          )}
          {stack.completedAt && (
            <>
              <span>·</span>
              <span>{formatStackDuration(stack.startedAt, stack.completedAt)}</span>
            </>
          )}
        </div>
        {stack.errorMessage && (
          <p className="mt-1 ml-9 text-xs text-red-600 dark:text-red-400">{stack.errorMessage}</p>
        )}
      </div>
      {canDrillDown && (
        <svg className="w-4 h-4 text-gray-400 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
        </svg>
      )}
    </div>
  );

  if (canDrillDown) {
    return (
      <Link
        to={`/deployments/${encodeURIComponent(stack.deploymentStackName!)}`}
        className="block hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors"
      >
        {content}
      </Link>
    );
  }

  return <div>{content}</div>;
}
