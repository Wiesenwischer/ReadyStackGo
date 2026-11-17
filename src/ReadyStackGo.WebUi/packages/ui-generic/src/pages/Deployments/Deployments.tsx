import { Link } from "react-router";
import {
  useDeploymentsStore,
  type DeploymentSummary,
  type ProductDeploymentSummaryDto,
  getHealthStatusPresentation,
  getOperationModePresentation,
  type StackHealthDto,
} from '@rsgo/core';
import { useEnvironment } from "../../context/EnvironmentContext";
import { useAuth } from "../../context/AuthContext";
import { DeploymentError } from "../../components/ui/DeploymentError";
import { ButtonLink } from "../../components/ui/Button";
import { StatusBadge } from "../../components/ui/StatusBadge";
import { toneForHealthStatus, toneForOperationMode, toneForProductStatus } from "../../components/ui/statusTone";

export default function Deployments() {
  const { activeEnvironment } = useEnvironment();
  const { token } = useAuth();
  const store = useDeploymentsStore(token, activeEnvironment?.id);

  const getConnectionStatusBadge = () => {
    switch (store.connectionState) {
      case 'connected':
        return (
          <span className="inline-flex items-center gap-1.5 text-xs text-status-healthy">
            <span className="h-2 w-2 rounded-full bg-status-healthy animate-pulse"></span>
            Live
          </span>
        );
      case 'connecting':
      case 'reconnecting':
        return (
          <span className="inline-flex items-center gap-1.5 text-xs text-status-degraded">
            <span className="h-2 w-2 rounded-full bg-status-degraded animate-pulse"></span>
            Connecting...
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1.5 text-xs text-fg-muted">
            <span className="h-2 w-2 rounded-full bg-status-unknown"></span>
            Offline
          </span>
        );
    }
  };

  const hasNoDeployments = !store.loading && activeEnvironment && store.deployments.length === 0 && store.productDeployments.length === 0;

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-[26px] leading-[34px] font-bold text-fg">
            Deployments
          </h2>
          <p className="mt-1 text-sm text-fg-secondary">
            Monitor and manage deployments {activeEnvironment && `in ${activeEnvironment.name}`}
          </p>
        </div>
        <div className="flex items-center gap-4">
          {getConnectionStatusBadge()}
          <ButtonLink to="/catalog" variant="primary">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            Deploy New Stack
          </ButtonLink>
        </div>
      </div>

      {!activeEnvironment && (
        <div className="mb-6 rounded-md bg-yellow-50 p-4 dark:bg-yellow-900/20">
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            No environment selected. Please select an environment to view deployments.
          </p>
        </div>
      )}

      {store.error && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">{store.error}</p>
        </div>
      )}

      {store.loading ? (
        <div className="rounded-2xl border border-line bg-surface px-4 py-8">
          <p className="text-center text-sm text-fg-secondary">
            Loading deployments...
          </p>
        </div>
      ) : !activeEnvironment ? (
        <div className="rounded-2xl border border-line bg-surface px-4 py-8">
          <p className="text-center text-sm text-fg-secondary">
            Select an environment to view deployments.
          </p>
        </div>
      ) : hasNoDeployments ? (
        <div className="rounded-2xl border border-line bg-surface px-4 py-8">
          <div className="text-center">
            <p className="text-sm text-fg-secondary mb-4">
              No deployments in this environment.
            </p>
            <Link
              to="/catalog"
              className="inline-flex items-center gap-2 text-fg-brand hover:underline"
            >
              Browse Catalog
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
              </svg>
            </Link>
          </div>
        </div>
      ) : (
        <div className="flex flex-col gap-6">
          {/* Product Deployments Section */}
          {store.productDeployments.length > 0 && (
            <div className="overflow-hidden rounded-2xl border border-line bg-surface">
              <div className="px-4 py-[18px] md:px-6">
                <h4 className="text-base font-semibold text-fg">
                  Product Deployments
                </h4>
              </div>
              <div className="divide-y divide-line border-t border-line">
                {store.productDeployments.map((pd) => (
                  <ProductDeploymentRow
                    key={pd.productDeploymentId}
                    deployment={pd}
                    formatDate={store.formatDate}
                  />
                ))}
              </div>
            </div>
          )}

          {/* Stack Deployments Section */}
          {store.deployments.length > 0 && (
            <div className="overflow-hidden rounded-2xl border border-line bg-surface">
              <div className="px-4 py-[18px] md:px-6">
                <h4 className="text-base font-semibold text-fg">
                  Deployed Stacks
                </h4>
              </div>
              <div className="divide-y divide-line border-t border-line">
                {store.deployments.map((deployment) => {
                  const health = store.healthData.get(deployment.deploymentId || '');
                  return (
                    <DeploymentRow
                      key={deployment.deploymentId || deployment.stackName}
                      deployment={deployment}
                      health={health}
                      formatDate={store.formatDate}
                    />
                  );
                })}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Product Deployment Row
// ============================================================================

interface ProductDeploymentRowProps {
  deployment: ProductDeploymentSummaryDto;
  formatDate: (date: string) => string;
}

function ProductDeploymentRow({ deployment, formatDate }: ProductDeploymentRowProps) {
  // When in maintenance mode, show "Stopped" instead of the lifecycle status
  const effectiveStatus = deployment.operationMode === 'Maintenance' ? 'Stopped' : deployment.status;
  const statusTone = toneForProductStatus(effectiveStatus);
  const statusLabel = effectiveStatus === 'PartiallyRunning' ? 'Partially Running' : effectiveStatus;

  return (
    <div className="px-4 py-4 md:px-6 hover:bg-raised transition-colors">
      <div className="flex items-center justify-between gap-4">
        {/* Product Info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-3">
            <h5 className="text-[15px] font-semibold text-fg truncate">
              {deployment.productDisplayName}
            </h5>
            <span className="inline-flex items-center rounded-md bg-raised px-2 py-0.5 font-mono text-xs text-fg-secondary">
              v{deployment.productVersion}
            </span>
            <StatusBadge tone={statusTone}>{statusLabel}</StatusBadge>
            {deployment.operationMode !== 'Normal' && (() => {
              const mp = getOperationModePresentation(deployment.operationMode);
              return <StatusBadge tone={toneForOperationMode(deployment.operationMode)}>{mp.label}</StatusBadge>;
            })()}
          </div>
          <div className="mt-1 flex items-center gap-4 text-[13px] text-fg-muted">
            <span className="font-mono text-xs">{deployment.deploymentName}</span>
            <span>•</span>
            <span>{deployment.completedStacks}/{deployment.totalStacks} stacks</span>
            {deployment.failedStacks > 0 && (
              <>
                <span>•</span>
                <span className="text-status-unhealthy">{deployment.failedStacks} failed</span>
              </>
            )}
            <span>•</span>
            <span>Deployed {formatDate(deployment.createdAt)}</span>
          </div>
          {deployment.errorMessage && (
            <div className="mt-1">
              <DeploymentError error={deployment.errorMessage} compact />
            </div>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2">
          <ButtonLink
            to={`/product-deployments/${encodeURIComponent(deployment.productDeploymentId)}`}
            variant="secondary"
            size="sm"
          >
            Details
          </ButtonLink>
          {deployment.canUpgrade && (
            <ButtonLink
              to={`/upgrade-product/${deployment.productDeploymentId}`}
              variant="secondary"
              size="sm"
            >
              Upgrade
            </ButtonLink>
          )}
          {deployment.canRemove && (
            <ButtonLink
              to={`/remove-product/${deployment.productDeploymentId}`}
              variant="secondary"
              size="sm"
            >
              Remove
            </ButtonLink>
          )}
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// Stack Deployment Row
// ============================================================================

interface DeploymentRowProps {
  deployment: DeploymentSummary;
  health?: StackHealthDto;
  formatDate: (date: string) => string;
}

function DeploymentRow({ deployment, health, formatDate }: DeploymentRowProps) {
  const statusPresentation = health
    ? getHealthStatusPresentation(health.overallStatus)
    : getHealthStatusPresentation('unknown');

  const modePresentation = health
    ? getOperationModePresentation(health.operationMode)
    : null;

  return (
    <div className="px-4 py-4 md:px-6 hover:bg-raised transition-colors">
      <div className="flex items-center justify-between gap-4">
        {/* Stack Info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-3">
            <h5 className="text-[15px] font-semibold text-fg truncate">
              {deployment.stackName}
            </h5>
            {/* Health Status Badge */}
            <StatusBadge
              tone={toneForHealthStatus(health?.overallStatus ?? 'unknown')}
              icon={health?.requiresAttention ? (
                <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
                  <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
                </svg>
              ) : undefined}
            >
              {statusPresentation.label}
            </StatusBadge>
            {/* Operation Mode Badge (if not Normal) */}
            {modePresentation && health?.operationMode !== 'Normal' && (
              <StatusBadge tone={toneForOperationMode(health?.operationMode)}>{modePresentation.label}</StatusBadge>
            )}
          </div>
          <div className="mt-1 flex items-center gap-4 text-[13px] text-fg-muted">
            {deployment.stackVersion && (
              <>
                <span className="font-mono text-xs">v{deployment.stackVersion}</span>
                <span>•</span>
              </>
            )}
            <span>
              {health
                ? `${health.healthyServices}/${health.totalServices} services healthy`
                : `${deployment.serviceCount} service${deployment.serviceCount !== 1 ? 's' : ''}`}
            </span>
            <span>•</span>
            <span>Deployed {formatDate(deployment.deployedAt)}</span>
          </div>
          {health?.statusMessage && (
            <p className="mt-1 text-xs text-fg-muted">
              {health.statusMessage}
            </p>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2">
          <ButtonLink
            to={`/deployments/${encodeURIComponent(deployment.stackName)}`}
            variant="secondary"
            size="sm"
          >
            Details
          </ButtonLink>
          <ButtonLink
            to={`/deployments/${encodeURIComponent(deployment.stackName)}/remove`}
            variant="secondary"
            size="sm"
          >
            Remove
          </ButtonLink>
        </div>
      </div>
    </div>
  );
}
