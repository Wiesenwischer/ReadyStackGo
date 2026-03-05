import { useParams, Link } from "react-router";
import {
  useProductDeploymentDetailStore,
  type ProductStackDeploymentDto,
} from '@rsgo/core';
import { useAuth } from "../../context/AuthContext";
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
    case 'Stopped':
      return { label: 'Stopped', bgColor: 'bg-orange-100 dark:bg-orange-900/30', textColor: 'text-orange-800 dark:text-orange-300' };
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
    case 'Stopped':
      return { label: 'Stopped', bgColor: 'bg-orange-100 dark:bg-orange-900/30', textColor: 'text-orange-800 dark:text-orange-300' };
    default:
      return { label: status, bgColor: 'bg-gray-100 dark:bg-gray-700', textColor: 'text-gray-700 dark:text-gray-300' };
  }
}

export default function ProductDeploymentDetail() {
  const { productDeploymentId } = useParams<{ productDeploymentId: string }>();
  const { token } = useAuth();
  const { activeEnvironment } = useEnvironment();

  const store = useProductDeploymentDetailStore(token, activeEnvironment?.id, productDeploymentId);

  if (store.state === 'loading') {
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

  if (store.error || !store.deployment) {
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
            {store.error || "Product deployment not found"}
          </p>
        </div>
      </div>
    );
  }

  const deployment = store.deployment;
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
            Deployed {store.formatDate(deployment.createdAt)}
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
          {deployment.canRedeploy && (
            <Link
              to={`/redeploy-product/${deployment.productDeploymentId}`}
              className="inline-flex items-center justify-center rounded bg-blue-100 px-3 py-1.5 text-sm font-medium text-blue-700 hover:bg-blue-200 dark:bg-blue-900/30 dark:text-blue-400 dark:hover:bg-blue-900/50"
            >
              Redeploy
            </Link>
          )}
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
            <Link
              to={`/stop-product/${deployment.productDeploymentId}`}
              className="inline-flex items-center justify-center rounded bg-orange-100 px-3 py-1.5 text-sm font-medium text-orange-700 hover:bg-orange-200 dark:bg-orange-900/30 dark:text-orange-400 dark:hover:bg-orange-900/50"
            >
              Stop Containers
            </Link>
          )}
          {deployment.canRestart && (
            <Link
              to={`/restart-product/${deployment.productDeploymentId}`}
              className="inline-flex items-center justify-center rounded bg-amber-100 px-3 py-1.5 text-sm font-medium text-amber-700 hover:bg-amber-200 dark:bg-amber-900/30 dark:text-amber-400 dark:hover:bg-amber-900/50"
            >
              Restart Containers
            </Link>
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
          <span className="text-sm text-gray-900 dark:text-white">{store.formatDate(deployment.createdAt)}</span>
          {deployment.completedAt && (
            <span className="text-xs text-gray-500 dark:text-gray-400 block">
              Completed {store.formatDate(deployment.completedAt)}
            </span>
          )}
        </OverviewCard>
        <OverviewCard label="Duration">
          <span className="text-sm font-mono text-gray-900 dark:text-white">
            {store.formatDuration(deployment.durationSeconds)}
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
              <StackRow key={stack.stackId} stack={stack} formatDate={store.formatDate} formatStackDuration={store.formatStackDuration} />
            ))}
        </div>
      </div>

      {/* Shared Variables */}
      {variableEntries.length > 0 && (
        <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <button
            onClick={store.toggleVariables}
            className="w-full px-4 py-5 md:px-6 flex items-center justify-between text-left"
          >
            <h4 className="text-lg font-semibold text-gray-900 dark:text-white">
              Shared Variables ({variableEntries.length})
            </h4>
            <svg
              className={`w-5 h-5 text-gray-500 transition-transform ${store.showVariables ? 'rotate-180' : ''}`}
              fill="none" stroke="currentColor" viewBox="0 0 24 24"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </button>
          {store.showVariables && (
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

function StackRow({ stack, formatDate, formatStackDuration }: { stack: ProductStackDeploymentDto; formatDate: (s: string) => string; formatStackDuration: (s?: string, e?: string) => string }) {
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
