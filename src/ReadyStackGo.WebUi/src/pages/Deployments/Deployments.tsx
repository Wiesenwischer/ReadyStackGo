import { useEffect, useState, useCallback } from "react";
import { Link } from "react-router";
import {
  listDeployments,
  listProductDeployments,
  type DeploymentSummary,
  type ProductDeploymentSummaryDto,
} from "../../api/deployments";
import { useEnvironment } from "../../context/EnvironmentContext";
import { useHealthHub } from "../../hooks/useHealthHub";
import {
  getHealthStatusPresentation,
  getOperationModePresentation,
  type StackHealthDto
} from "../../api/health";

export default function Deployments() {
  const { activeEnvironment } = useEnvironment();
  const [deployments, setDeployments] = useState<DeploymentSummary[]>([]);
  const [productDeployments, setProductDeployments] = useState<ProductDeploymentSummaryDto[]>([]);
  const [healthData, setHealthData] = useState<Map<string, StackHealthDto>>(new Map());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // SignalR Health Hub connection
  const { connectionState, subscribeToEnvironment, unsubscribeFromEnvironment } = useHealthHub({
    onDeploymentHealthChanged: (health) => {
      setHealthData(prev => {
        const newMap = new Map(prev);
        newMap.set(health.deploymentId, health);
        return newMap;
      });
    },
    onEnvironmentHealthChanged: (summary) => {
      // Update all stacks from environment summary
      const newMap = new Map<string, StackHealthDto>();
      summary.stacks.forEach(stack => {
        newMap.set(stack.deploymentId, stack);
      });
      setHealthData(newMap);
    }
  });

  // Subscribe to environment when it changes
  useEffect(() => {
    if (activeEnvironment && connectionState === 'connected') {
      subscribeToEnvironment(activeEnvironment.id);
      return () => {
        unsubscribeFromEnvironment(activeEnvironment.id);
      };
    }
  }, [activeEnvironment, connectionState, subscribeToEnvironment, unsubscribeFromEnvironment]);

  const loadDeployments = useCallback(async () => {
    if (!activeEnvironment) {
      setDeployments([]);
      setProductDeployments([]);
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const [stackResponse, productResponse] = await Promise.all([
        listDeployments(activeEnvironment.id),
        listProductDeployments(activeEnvironment.id),
      ]);
      if (stackResponse.success) {
        setDeployments(stackResponse.deployments);
      } else {
        setError("Failed to load deployments");
      }
      if (productResponse.success) {
        setProductDeployments(productResponse.productDeployments);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load deployments");
    } finally {
      setLoading(false);
    }
  }, [activeEnvironment]);

  useEffect(() => {
    loadDeployments();
  }, [loadDeployments]);

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString() + " " + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  const getConnectionStatusBadge = () => {
    switch (connectionState) {
      case 'connected':
        return (
          <span className="inline-flex items-center gap-1.5 text-xs text-green-600 dark:text-green-400">
            <span className="h-2 w-2 rounded-full bg-green-500 animate-pulse"></span>
            Live
          </span>
        );
      case 'connecting':
      case 'reconnecting':
        return (
          <span className="inline-flex items-center gap-1.5 text-xs text-yellow-600 dark:text-yellow-400">
            <span className="h-2 w-2 rounded-full bg-yellow-500 animate-pulse"></span>
            Connecting...
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400">
            <span className="h-2 w-2 rounded-full bg-gray-400"></span>
            Offline
          </span>
        );
    }
  };

  const hasNoDeployments = !loading && activeEnvironment && deployments.length === 0 && productDeployments.length === 0;

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
            Deployments
          </h2>
          <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">
            Monitor and manage deployments {activeEnvironment && `in ${activeEnvironment.name}`}
          </p>
        </div>
        <div className="flex items-center gap-4">
          {getConnectionStatusBadge()}
          <Link
            to="/catalog"
            className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            Deploy New Stack
          </Link>
        </div>
      </div>

      {!activeEnvironment && (
        <div className="mb-6 rounded-md bg-yellow-50 p-4 dark:bg-yellow-900/20">
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            No environment selected. Please select an environment to view deployments.
          </p>
        </div>
      )}

      {error && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
        </div>
      )}

      {loading ? (
        <div className="rounded-2xl border border-gray-200 bg-white px-4 py-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <p className="text-center text-sm text-gray-600 dark:text-gray-400">
            Loading deployments...
          </p>
        </div>
      ) : !activeEnvironment ? (
        <div className="rounded-2xl border border-gray-200 bg-white px-4 py-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <p className="text-center text-sm text-gray-600 dark:text-gray-400">
            Select an environment to view deployments.
          </p>
        </div>
      ) : hasNoDeployments ? (
        <div className="rounded-2xl border border-gray-200 bg-white px-4 py-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="text-center">
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
              No deployments in this environment.
            </p>
            <Link
              to="/catalog"
              className="inline-flex items-center gap-2 text-brand-600 hover:text-brand-700 dark:text-brand-400"
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
          {productDeployments.length > 0 && (
            <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
              <div className="px-4 py-6 md:px-6 xl:px-7.5">
                <h4 className="text-xl font-semibold text-black dark:text-white">
                  Product Deployments
                </h4>
              </div>
              <div className="border-t border-stroke dark:border-strokedark">
                {productDeployments.map((pd) => (
                  <ProductDeploymentRow
                    key={pd.productDeploymentId}
                    deployment={pd}
                    formatDate={formatDate}
                  />
                ))}
              </div>
            </div>
          )}

          {/* Stack Deployments Section */}
          {deployments.length > 0 && (
            <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
              <div className="px-4 py-6 md:px-6 xl:px-7.5">
                <h4 className="text-xl font-semibold text-black dark:text-white">
                  Deployed Stacks
                </h4>
              </div>
              <div className="border-t border-stroke dark:border-strokedark">
                {deployments.map((deployment) => {
                  const health = healthData.get(deployment.deploymentId || '');
                  return (
                    <DeploymentRow
                      key={deployment.deploymentId || deployment.stackName}
                      deployment={deployment}
                      health={health}
                      formatDate={formatDate}
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
    default:
      return { label: status, bgColor: 'bg-gray-100 dark:bg-gray-700', textColor: 'text-gray-700 dark:text-gray-300' };
  }
}

interface ProductDeploymentRowProps {
  deployment: ProductDeploymentSummaryDto;
  formatDate: (date: string) => string;
}

function ProductDeploymentRow({ deployment, formatDate }: ProductDeploymentRowProps) {
  const status = getProductStatusPresentation(deployment.status);

  return (
    <div className="px-4 py-4 md:px-6 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
      <div className="flex items-center justify-between gap-4">
        {/* Product Info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-3">
            <h5 className="font-semibold text-gray-900 dark:text-white truncate">
              {deployment.productDisplayName}
            </h5>
            <span className="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600 dark:bg-gray-700 dark:text-gray-300">
              v{deployment.productVersion}
            </span>
            <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${status.bgColor} ${status.textColor}`}>
              {status.label}
            </span>
          </div>
          <div className="mt-1 flex items-center gap-4 text-sm text-gray-500 dark:text-gray-400">
            <span className="font-mono text-xs">{deployment.deploymentName}</span>
            <span>•</span>
            <span>{deployment.completedStacks}/{deployment.totalStacks} stacks</span>
            {deployment.failedStacks > 0 && (
              <>
                <span>•</span>
                <span className="text-red-600 dark:text-red-400">{deployment.failedStacks} failed</span>
              </>
            )}
            <span>•</span>
            <span>Deployed {formatDate(deployment.createdAt)}</span>
          </div>
          {deployment.errorMessage && (
            <p className="mt-1 text-xs text-red-600 dark:text-red-400">
              {deployment.errorMessage}
            </p>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2">
          <Link
            to={`/product-deployments/${encodeURIComponent(deployment.productDeploymentId)}`}
            className="inline-flex items-center justify-center rounded bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
          >
            Details
          </Link>
          {deployment.canUpgrade && (
            <Link
              to={`/upgrade-product/${deployment.productDeploymentId}`}
              className="inline-flex items-center justify-center rounded bg-brand-100 px-3 py-1.5 text-sm font-medium text-brand-700 hover:bg-brand-200 dark:bg-brand-900/30 dark:text-brand-400 dark:hover:bg-brand-900/50"
            >
              Upgrade
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
    <div className="px-4 py-4 md:px-6 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
      <div className="flex items-center justify-between gap-4">
        {/* Stack Info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-3">
            <h5 className="font-semibold text-gray-900 dark:text-white truncate">
              {deployment.stackName}
            </h5>
            {/* Health Status Badge */}
            <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ${statusPresentation.bgColor} ${statusPresentation.textColor}`}>
              {health?.requiresAttention && (
                <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
                </svg>
              )}
              {statusPresentation.label}
            </span>
            {/* Operation Mode Badge (if not Normal) */}
            {modePresentation && health?.operationMode !== 'Normal' && (
              <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${modePresentation.bgColor} ${modePresentation.textColor}`}>
                {modePresentation.label}
              </span>
            )}
          </div>
          <div className="mt-1 flex items-center gap-4 text-sm text-gray-500 dark:text-gray-400">
            {deployment.stackVersion && (
              <>
                <span>v{deployment.stackVersion}</span>
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
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              {health.statusMessage}
            </p>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2">
          <Link
            to={`/deployments/${encodeURIComponent(deployment.stackName)}`}
            className="inline-flex items-center justify-center rounded bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
          >
            Details
          </Link>
          <Link
            to={`/deployments/${encodeURIComponent(deployment.stackName)}/remove`}
            className="inline-flex items-center justify-center rounded bg-red-100 px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-200 dark:bg-red-900/30 dark:text-red-400 dark:hover:bg-red-900/50"
          >
            Remove
          </Link>
        </div>
      </div>
    </div>
  );
}
