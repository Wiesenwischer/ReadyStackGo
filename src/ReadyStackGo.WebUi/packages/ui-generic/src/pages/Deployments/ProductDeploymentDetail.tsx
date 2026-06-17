import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import {
  useProductDeploymentDetailStore,
  getOperationModePresentation,
  listPrtgConnections,
  linkProductDeploymentToPrtgConnection,
  setInlinePrtgRegistration,
  probePrtgGroups,
  type ProductStackDeploymentDto,
  type PrtgConnectionDto,
  type ProbedPrtgGroup,
} from '@rsgo/core';
import { useAuth } from "../../context/AuthContext";
import { useEnvironment } from "../../context/EnvironmentContext";
import { DeploymentError } from "../../components/ui/DeploymentError";
import ProductUpdateBadge from "../../components/deployments/ProductUpdateBadge";

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
  // When in maintenance mode, show "Stopped" instead of the lifecycle status (e.g. "Running")
  const effectiveStatus = deployment.operationMode === 'Maintenance' ? 'Stopped' : deployment.status;
  const status = getProductStatusPresentation(effectiveStatus);
  const modePresentation = deployment.operationMode !== 'Normal'
    ? getOperationModePresentation(deployment.operationMode)
    : null;
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
            {activeEnvironment?.id && productDeploymentId && (
              <ProductUpdateBadge
                environmentId={activeEnvironment.id}
                productDeploymentId={productDeploymentId}
              />
            )}
            {modePresentation && (
              <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${modePresentation.bgColor} ${modePresentation.textColor}`}>
                {modePresentation.label}
              </span>
            )}
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
          {/* Maintenance Mode Controls */}
          {deployment.canEnterMaintenance && (
            <Link
              to={`/enter-maintenance/${deployment.productDeploymentId}`}
              className="inline-flex items-center justify-center gap-2 rounded bg-yellow-100 px-3 py-1.5 text-sm font-medium text-yellow-800 hover:bg-yellow-200 dark:bg-yellow-900/30 dark:text-yellow-400 dark:hover:bg-yellow-900/50"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
              Enter Maintenance
            </Link>
          )}
          {deployment.canExitMaintenance && (
            <Link
              to={`/exit-maintenance/${deployment.productDeploymentId}`}
              className="inline-flex items-center justify-center gap-2 rounded bg-green-100 px-3 py-1.5 text-sm font-medium text-green-800 hover:bg-green-200 dark:bg-green-900/30 dark:text-green-400 dark:hover:bg-green-900/50"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z" />
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              Exit Maintenance
            </Link>
          )}
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


      {/* Maintenance Info */}
      {deployment.operationMode === 'Maintenance' && deployment.maintenanceTrigger && (
        <div className="mb-6 rounded-lg border border-yellow-200 bg-yellow-50 p-3 dark:border-yellow-800 dark:bg-yellow-900/20">
          <div className="flex items-center gap-2 text-sm text-yellow-800 dark:text-yellow-300">
            <svg className="w-4 h-4 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
            <span className="font-medium">Maintenance Mode</span>
            <span className="text-yellow-600 dark:text-yellow-400">
              ({deployment.maintenanceTrigger.source === 'Observer' ? 'Observer' : 'Manual'})
            </span>
            {deployment.maintenanceTrigger.reason && (
              <span className="text-yellow-700 dark:text-yellow-400">
                &mdash; {deployment.maintenanceTrigger.reason}
              </span>
            )}
          </div>
        </div>
      )}

      {/* Error Alert */}
      {deployment.errorMessage && (
        <div className="mb-6">
          <DeploymentError error={deployment.errorMessage} />
        </div>
      )}

      {/* Overview Cards */}
      <div className="mb-6 grid gap-4 md:grid-cols-5">
        <OverviewCard label="Status">
          <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${status.bgColor} ${status.textColor}`}>
            {status.label}
          </span>
        </OverviewCard>
        <OverviewCard label="Operation Mode">
          {(() => {
            const mp = getOperationModePresentation(deployment.operationMode);
            return (
              <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${mp.bgColor} ${mp.textColor}`}>
                {mp.label}
              </span>
            );
          })()}
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
              <StackRow key={stack.stackId} stack={stack} parentOperationMode={deployment.operationMode} formatDate={store.formatDate} formatStackDuration={store.formatStackDuration} />
            ))}
        </div>
      </div>

      {/* PRTG monitoring */}
      <PrtgMonitoringCard
        productDeploymentId={deployment.productDeploymentId}
        prtgConnectionId={deployment.prtgConnectionId ?? null}
        prtgDeviceId={deployment.prtgDeviceId ?? null}
        prtgLastSyncedAt={deployment.prtgLastSyncedAt ?? null}
        inlineUrl={deployment.inlinePrtgUrl ?? null}
        hasInlineApiToken={deployment.hasInlinePrtgApiToken ?? false}
        inlineTemplateDeviceId={deployment.inlinePrtgTemplateDeviceId ?? null}
        inlineVerifyTls={deployment.inlinePrtgVerifyTls ?? true}
        formatDate={store.formatDate}
      />

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

function StackRow({ stack, parentOperationMode, formatDate, formatStackDuration }: { stack: ProductStackDeploymentDto; parentOperationMode: string; formatDate: (s: string) => string; formatStackDuration: (s?: string, e?: string) => string }) {
  const effectiveStackStatus = parentOperationMode === 'Maintenance' ? 'Stopped' : stack.status;
  const status = getStackStatusPresentation(effectiveStackStatus);
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
          <div className="mt-1 ml-9">
            <DeploymentError error={stack.errorMessage} compact />
          </div>
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

// ============================================================================
// PRTG monitoring card (Variant 3)
// ============================================================================

interface PrtgMonitoringCardProps {
  productDeploymentId: string;
  prtgConnectionId: string | null;
  prtgDeviceId: number | null;
  prtgLastSyncedAt: string | null;
  inlineUrl: string | null;
  hasInlineApiToken: boolean;
  inlineTemplateDeviceId: number | null;
  inlineVerifyTls: boolean;
  formatDate: (dateString: string) => string;
}

type PrtgMode = 'connection' | 'inline';

function PrtgMonitoringCard(props: PrtgMonitoringCardProps) {
  const {
    productDeploymentId,
    prtgConnectionId,
    prtgDeviceId,
    prtgLastSyncedAt,
    inlineUrl,
    hasInlineApiToken,
    inlineTemplateDeviceId,
    inlineVerifyTls,
    formatDate,
  } = props;

  const [connections, setConnections] = useState<PrtgConnectionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Default mode: whichever is currently configured, falling back to connection.
  const [mode, setMode] = useState<PrtgMode>(inlineUrl ? 'inline' : 'connection');

  // "Add to PRTG" dialog state
  const [dialogOpen, setDialogOpen] = useState(false);

  // Inline-mode state
  const [inline, setInline] = useState({
    url: inlineUrl ?? '',
    apiToken: '',
    templateDeviceId: inlineTemplateDeviceId !== null ? String(inlineTemplateDeviceId) : '',
    verifyTls: inlineVerifyTls,
  });

  useEffect(() => {
    (async () => {
      try {
        setLoading(true);
        const list = await listPrtgConnections();
        setConnections(list);
        setError(null);
      } catch (e: unknown) {
        setError(e instanceof Error ? e.message : String(e));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const linkedConnection = connections.find((c) => c.id === prtgConnectionId);

  const unlink = async () => {
    if (!confirm('Remove this deployment from PRTG monitoring? The PRTG device will be deleted.')) return;
    setSaving(true);
    setError(null);
    try {
      await linkProductDeploymentToPrtgConnection(productDeploymentId, null, null);
      window.location.reload();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
      setSaving(false);
    }
  };

  const saveInline = async () => {
    setSaving(true);
    setError(null);
    try {
      const url = inline.url.trim();
      await setInlinePrtgRegistration(productDeploymentId, {
        url: url === '' ? null : url,
        apiToken: inline.apiToken === '' ? null : inline.apiToken,
        templateDeviceId: inline.templateDeviceId === '' ? null : Number(inline.templateDeviceId),
        verifyTls: inline.verifyTls,
      });
      window.location.reload();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
      setSaving(false);
    }
  };

  const clearInline = async () => {
    setSaving(true);
    setError(null);
    try {
      await setInlinePrtgRegistration(productDeploymentId, {
        url: null,
        verifyTls: true,
      });
      window.location.reload();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
      setSaving(false);
    }
  };

  return (
    <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
      <div className="px-4 py-5 md:px-6">
        <h4 className="text-lg font-semibold text-gray-900 dark:text-white">PRTG monitoring</h4>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          RSGO can auto-register this deployment as a PRTG device when it goes Running and
          auto-deregister on remove. Pick a saved <strong>connection</strong> (reusable across
          deployments) or enter <strong>inline credentials</strong> for one-off setups.
        </p>
      </div>

      {/* Tab bar */}
      <div className="border-t border-gray-200 dark:border-gray-700 px-4 md:px-6 pt-3 flex gap-2">
        <button
          onClick={() => setMode('connection')}
          className={`px-3 py-1.5 text-sm rounded-md ${
            mode === 'connection'
              ? 'bg-brand-100 text-brand-700 dark:bg-brand-900/30 dark:text-brand-300'
              : 'text-gray-600 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800/50'
          }`}
        >
          Saved connection
        </button>
        <button
          onClick={() => setMode('inline')}
          className={`px-3 py-1.5 text-sm rounded-md ${
            mode === 'inline'
              ? 'bg-brand-100 text-brand-700 dark:bg-brand-900/30 dark:text-brand-300'
              : 'text-gray-600 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800/50'
          }`}
        >
          Inline (ad-hoc)
        </button>
      </div>

      {mode === 'connection' ? (
        <div className="px-4 py-4 md:px-6 space-y-3">
          {loading ? (
            <p className="text-sm text-gray-500">Loading PRTG connections…</p>
          ) : prtgConnectionId ? (
            // Already linked: show status + Remove
            <>
              <div className="rounded-md bg-green-50 px-3 py-3 text-sm text-green-800 dark:bg-green-900/20 dark:text-green-300 flex items-start gap-2">
                <span aria-hidden>✓</span>
                <div>
                  <div>
                    Registered with <strong>{linkedConnection?.name ?? 'a deleted connection'}</strong>
                    {prtgDeviceId !== null && <> as PRTG device <code className="font-mono">{prtgDeviceId}</code></>}
                    .
                  </div>
                  {linkedConnection && prtgDeviceId !== null && (
                    <a
                      href={`${linkedConnection.url.replace(/\/$/, '')}/device.htm?id=${prtgDeviceId}`}
                      target="_blank"
                      rel="noreferrer"
                      className="text-xs underline hover:no-underline"
                    >
                      Open device in PRTG ↗
                    </a>
                  )}
                  {prtgLastSyncedAt && (
                    <div className="mt-0.5 text-xs opacity-80">
                      Last sync: {formatDate(prtgLastSyncedAt)}
                    </div>
                  )}
                </div>
              </div>
              <button
                onClick={unlink}
                disabled={saving}
                className="rounded-md bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-200 dark:hover:bg-gray-600 disabled:opacity-50"
              >
                {saving ? 'Removing…' : 'Remove from PRTG monitoring'}
              </button>
              {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}
            </>
          ) : connections.length === 0 ? (
            // Not linked + no connections exist → bounce to Settings
            <p className="text-sm text-gray-500 dark:text-gray-400">
              No PRTG connections configured.{' '}
              <Link to="/settings/prtg-connections" className="text-brand-600 hover:underline">
                Add one in Settings
              </Link>
              {' '}first, then come back here to register this deployment.
            </p>
          ) : (
            // Not linked, connections exist → single Add button + modal
            <>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Not yet registered in PRTG. RSGO can create a PRTG device for this
                deployment and trigger auto-discovery — sensors come from the imported
                RSGO MIB.
              </p>
              <button
                onClick={() => setDialogOpen(true)}
                className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
              >
                + Add to PRTG monitoring
              </button>
              {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}
              {dialogOpen && (
                <AddToPrtgDialog
                  productDeploymentId={productDeploymentId}
                  connections={connections}
                  onClose={() => setDialogOpen(false)}
                  onDone={() => { setDialogOpen(false); window.location.reload(); }}
                />
              )}
            </>
          )}
        </div>
      ) : (
        <div className="px-4 py-4 md:px-6 space-y-3">
          <p className="text-xs text-gray-500 dark:text-gray-400">
            Inline credentials are stored encrypted on this single deployment. They take
            precedence over any saved connection (saving inline clears the connection link).
            Use this for ad-hoc / customer-hosted PRTG setups.
          </p>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-gray-500 dark:text-gray-400">URL</span>
              <input
                value={inline.url}
                onChange={(e) => setInline({ ...inline, url: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md font-mono text-xs dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                placeholder="https://prtg.example.local"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-gray-500 dark:text-gray-400">
                API token / passhash {hasInlineApiToken && <span className="text-xs italic">(leave empty to keep existing)</span>}
              </span>
              <input
                type="password"
                value={inline.apiToken}
                onChange={(e) => setInline({ ...inline, apiToken: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md font-mono text-xs dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                placeholder={hasInlineApiToken ? '••••••••••' : 'PRTG API token or passhash'}
              />
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-gray-500 dark:text-gray-400">Template Device ID</span>
              <input
                type="number"
                value={inline.templateDeviceId}
                onChange={(e) => setInline({ ...inline, templateDeviceId: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-md dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                placeholder="e.g. 4221"
              />
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={inline.verifyTls}
                onChange={(e) => setInline({ ...inline, verifyTls: e.target.checked })}
                className="rounded text-brand-600 focus:ring-brand-500"
              />
              <span className="text-gray-700 dark:text-gray-200">Verify TLS certificate</span>
            </label>
          </div>

          {prtgDeviceId !== null && inlineUrl && (
            <div className="rounded-md bg-gray-50 px-3 py-2 text-xs text-gray-600 dark:bg-gray-800/50 dark:text-gray-300">
              Registered as PRTG device <code className="font-mono">{prtgDeviceId}</code> on{' '}
              <span className="font-mono">{inlineUrl}</span>.
              {prtgLastSyncedAt && <> Last sync: {formatDate(prtgLastSyncedAt)}.</>}
            </div>
          )}

          <div className="flex items-center gap-3">
            <button
              onClick={saveInline}
              disabled={saving || !inline.url || (!hasInlineApiToken && !inline.apiToken)}
              className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
            >
              {saving ? 'Saving…' : 'Save inline'}
            </button>
            {inlineUrl && (
              <button
                onClick={clearInline}
                disabled={saving}
                className="rounded-md bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-200 dark:hover:bg-gray-600 disabled:opacity-50"
              >
                Clear inline
              </button>
            )}
            {error && <span className="text-sm text-red-600 dark:text-red-400">{error}</span>}
          </div>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// AddToPrtgDialog — modal that takes the admin from "have a connection" to
// "have a PRTG device" in a single short flow. Shows phased progress like a
// product deployment so the user knows what's happening.
// ============================================================================

type AddPhaseStatus = 'pending' | 'running' | 'ok' | 'failed';

interface AddPhase {
  label: string;
  status: AddPhaseStatus;
  detail?: string;
}

function AddToPrtgDialog({
  productDeploymentId,
  connections,
  onClose,
  onDone,
}: {
  productDeploymentId: string;
  connections: PrtgConnectionDto[];
  onClose: () => void;
  onDone: () => void;
}) {
  const [connectionId, setConnectionId] = useState(connections[0]?.id ?? '');
  const [groups, setGroups] = useState<ProbedPrtgGroup[]>([]);
  const [groupId, setGroupId] = useState<string>('');
  const [loadingGroups, setLoadingGroups] = useState(false);
  const [probeError, setProbeError] = useState<string | null>(null);
  const [phases, setPhases] = useState<AddPhase[] | null>(null);
  const [running, setRunning] = useState(false);

  // Auto-load groups whenever the selected connection changes.
  useEffect(() => {
    if (!connectionId) {
      setGroups([]);
      return;
    }
    let cancelled = false;
    (async () => {
      setLoadingGroups(true);
      setProbeError(null);
      try {
        const resp = await probePrtgGroups({
          url: connections.find((c) => c.id === connectionId)?.url ?? '',
          existingConnectionId: connectionId,
          verifyTls: connections.find((c) => c.id === connectionId)?.verifyTls ?? true,
        });
        if (cancelled) return;
        if (!resp.success) {
          setProbeError(resp.error ?? 'Could not reach PRTG.');
          setGroups([]);
          return;
        }
        setGroups(resp.groups);
        // Auto-pick first group if only one was returned.
        if (resp.groups.length === 1) setGroupId(String(resp.groups[0].objectId));
      } catch (e: unknown) {
        if (!cancelled) {
          setProbeError(e instanceof Error ? e.message : String(e));
          setGroups([]);
        }
      } finally {
        if (!cancelled) setLoadingGroups(false);
      }
    })();
    return () => { cancelled = true; };
  }, [connectionId, connections]);

  const run = async () => {
    if (!connectionId || !groupId) return;
    setRunning(true);
    const initial: AddPhase[] = [
      { label: 'Creating device in PRTG…', status: 'running' },
      { label: 'Triggering auto-discovery', status: 'pending' },
    ];
    setPhases(initial);

    try {
      await linkProductDeploymentToPrtgConnection(productDeploymentId, connectionId, Number(groupId));
      // The backend does both create + discovery synchronously; if we got here
      // without an exception, both succeeded. Mark them as ok in one shot.
      setPhases([
        { label: 'Device created in PRTG', status: 'ok' },
        { label: 'Auto-discovery triggered', status: 'ok' },
      ]);
      // Brief pause so the user sees the green check, then reload to show the
      // post-link status box on the page underneath.
      setTimeout(onDone, 800);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      setPhases([
        { label: 'Create device in PRTG', status: 'failed', detail: msg },
        { label: 'Auto-discovery', status: 'pending' },
      ]);
      setRunning(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-md rounded-2xl bg-white p-6 dark:bg-gray-900">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Add to PRTG monitoring</h3>

        {phases === null ? (
          <div className="mt-4 space-y-3">
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-gray-500 dark:text-gray-400">PRTG connection</span>
              <select
                value={connectionId}
                onChange={(e) => { setConnectionId(e.target.value); setGroupId(''); }}
                className="w-full px-3 py-2 border border-gray-300 rounded-md dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                disabled={connections.length === 1}
              >
                {connections.map((c) => (
                  <option key={c.id} value={c.id}>{c.name} ({c.url})</option>
                ))}
              </select>
            </label>

            <label className="flex flex-col gap-1 text-sm">
              <span className="text-gray-500 dark:text-gray-400">
                Target group {loadingGroups && <span className="text-xs italic">(loading from PRTG…)</span>}
              </span>
              <select
                value={groupId}
                onChange={(e) => setGroupId(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-md dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500 disabled:opacity-50"
                disabled={loadingGroups || groups.length === 0}
              >
                <option value="">— pick a group —</option>
                {groups.map((g) => (
                  <option key={g.objectId} value={g.objectId}>
                    {g.probe ? `${g.probe} › ${g.name}` : g.name}
                  </option>
                ))}
              </select>
              {probeError && <span className="text-xs text-red-600 dark:text-red-400">{probeError}</span>}
            </label>

            <div className="flex justify-end gap-2 pt-2">
              <button
                onClick={onClose}
                className="rounded-md bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-200 dark:hover:bg-gray-600"
              >
                Cancel
              </button>
              <button
                onClick={run}
                disabled={!connectionId || !groupId || running}
                className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
              >
                Add to PRTG
              </button>
            </div>
          </div>
        ) : (
          <div className="mt-4">
            <ol className="space-y-2 text-sm">
              {phases.map((p, i) => (
                <li key={i} className="flex items-start gap-2">
                  <PhaseIcon status={p.status} />
                  <div>
                    <div className="text-gray-800 dark:text-gray-200">{p.label}</div>
                    {p.detail && <div className="text-xs text-red-600 dark:text-red-400">{p.detail}</div>}
                  </div>
                </li>
              ))}
            </ol>
            {!running && (
              <div className="mt-4 flex justify-end">
                <button
                  onClick={onClose}
                  className="rounded-md bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-200 dark:hover:bg-gray-600"
                >
                  Close
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function PhaseIcon({ status }: { status: AddPhaseStatus }) {
  if (status === 'ok') return <span className="text-green-600" aria-hidden>✓</span>;
  if (status === 'failed') return <span className="text-red-600" aria-hidden>✗</span>;
  if (status === 'running') return <span className="text-brand-600 animate-pulse" aria-hidden>●</span>;
  return <span className="text-gray-400" aria-hidden>○</span>;
}
