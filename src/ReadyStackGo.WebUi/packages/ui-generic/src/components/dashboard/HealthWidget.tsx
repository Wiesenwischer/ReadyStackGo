import { Link } from 'react-router';
import { useEnvironment } from '../../context/EnvironmentContext';
import { useAuth } from '../../context/AuthContext';
import {
  getHealthStatusPresentation,
  useHealthWidgetStore,
} from '@rsgo/core';

interface HealthWidgetProps {
  className?: string;
}

export default function HealthWidget({ className = '' }: HealthWidgetProps) {
  const { activeEnvironment } = useEnvironment();
  const { token } = useAuth();
  const {
    healthSummary,
    loading,
    error,
    connectionState,
    productGroups,
    standaloneStacks,
  } = useHealthWidgetStore(token, activeEnvironment?.id);

  const getConnectionIndicator = () => {
    switch (connectionState) {
      case 'connected':
        return (
          <span className="flex items-center gap-1 text-xs text-green-600 dark:text-green-400">
            <span className="h-2 w-2 rounded-full bg-green-500 animate-pulse" />
            Live
          </span>
        );
      case 'connecting':
      case 'reconnecting':
        return (
          <span className="flex items-center gap-1 text-xs text-yellow-600 dark:text-yellow-400">
            <span className="h-2 w-2 rounded-full bg-yellow-500 animate-pulse" />
            Connecting...
          </span>
        );
      default:
        return (
          <span className="flex items-center gap-1 text-xs text-gray-500 dark:text-gray-400">
            <span className="h-2 w-2 rounded-full bg-gray-400" />
            Offline
          </span>
        );
    }
  };

  const HealthStatusBadge = ({ status }: { status: string }) => {
    const presentation = getHealthStatusPresentation(status);
    return (
      <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ${presentation.bgColor} ${presentation.textColor} dark:bg-opacity-20`}>
        {presentation.label}
      </span>
    );
  };

  const HealthRow = ({ name, healthyCount, totalCount, overallStatus, linkTo }: {
    name: string;
    healthyCount: number;
    totalCount: number;
    overallStatus: string;
    linkTo: string;
  }) => {
    const presentation = getHealthStatusPresentation(overallStatus);
    return (
      <Link
        to={linkTo}
        className="flex items-center justify-between py-2 border-b border-gray-100 dark:border-gray-800 last:border-0 hover:bg-gray-50 dark:hover:bg-gray-800/50 -mx-2 px-2 rounded transition-colors"
      >
        <div className="flex items-center gap-2 min-w-0">
          <span className={`h-2 w-2 rounded-full flex-shrink-0 ${presentation.bgColor.replace('-100', '-500').replace('dark:bg-', '')}`} />
          <span className="text-sm font-medium text-gray-900 dark:text-white truncate">
            {name}
          </span>
        </div>
        <div className="flex items-center gap-2 flex-shrink-0 ml-2">
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {healthyCount}/{totalCount}
          </span>
          <HealthStatusBadge status={overallStatus} />
        </div>
      </Link>
    );
  };

  if (loading) {
    return (
      <div className={`rounded-2xl border border-gray-200 bg-white px-5 pb-5 pt-7.5 dark:border-gray-800 dark:bg-white/[0.03] sm:px-7.5 ${className}`}>
        <div className="animate-pulse">
          <div className="h-6 bg-gray-200 dark:bg-gray-700 rounded w-1/3 mb-4" />
          <div className="space-y-3">
            <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded" />
            <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded" />
            <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded" />
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={`rounded-2xl border border-red-200 bg-red-50 px-5 pb-5 pt-7.5 dark:border-red-800 dark:bg-red-900/20 sm:px-7.5 ${className}`}>
        <h4 className="text-xl font-semibold text-red-800 dark:text-red-300 mb-2">
          Health Status
        </h4>
        <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
      </div>
    );
  }

  if (!healthSummary || healthSummary.totalStacks === 0) {
    return (
      <div className={`rounded-2xl border border-gray-200 bg-white px-5 pb-5 pt-7.5 dark:border-gray-800 dark:bg-white/[0.03] sm:px-7.5 ${className}`}>
        <div className="flex items-center justify-between mb-4">
          <h4 className="text-xl font-semibold text-black dark:text-white">
            Health Status
          </h4>
          {getConnectionIndicator()}
        </div>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          No deployments to monitor. Deploy a stack to see health status.
        </p>
        <Link
          to="/catalog"
          className="mt-4 inline-block text-sm font-medium text-brand-500 hover:text-brand-600 dark:text-brand-400"
        >
          Browse Stack Catalog
        </Link>
      </div>
    );
  }

  return (
    <div className={`rounded-2xl border border-gray-200 bg-white px-5 pb-5 pt-7.5 dark:border-gray-800 dark:bg-white/[0.03] sm:px-7.5 ${className}`}>
      <div className="flex items-center justify-between mb-4">
        <h4 className="text-xl font-semibold text-black dark:text-white">
          Health Status
        </h4>
        {getConnectionIndicator()}
      </div>

      {/* Summary Stats */}
      <div className="grid grid-cols-3 gap-2 mb-4">
        <div className="text-center p-2 rounded-lg bg-green-50 dark:bg-green-900/20">
          <div className="text-lg font-bold text-green-600 dark:text-green-400">
            {healthSummary.healthyCount}
          </div>
          <div className="text-xs text-green-700 dark:text-green-300">Healthy</div>
        </div>
        <div className="text-center p-2 rounded-lg bg-yellow-50 dark:bg-yellow-900/20">
          <div className="text-lg font-bold text-yellow-600 dark:text-yellow-400">
            {healthSummary.degradedCount}
          </div>
          <div className="text-xs text-yellow-700 dark:text-yellow-300">Degraded</div>
        </div>
        <div className="text-center p-2 rounded-lg bg-red-50 dark:bg-red-900/20">
          <div className="text-lg font-bold text-red-600 dark:text-red-400">
            {healthSummary.unhealthyCount}
          </div>
          <div className="text-xs text-red-700 dark:text-red-300">Unhealthy</div>
        </div>
      </div>

      {/* Health List — grouped by product */}
      <div>
        {productGroups.map((group) => (
          <HealthRow
            key={group.productDeploymentId}
            name={group.productDisplayName}
            healthyCount={group.healthyStacks}
            totalCount={group.totalStacks}
            overallStatus={group.overallStatus}
            linkTo={`/product-deployments/${encodeURIComponent(group.productDeploymentId)}`}
          />
        ))}
        {standaloneStacks.map((stack) => (
          <HealthRow
            key={stack.deploymentId}
            name={stack.stackName}
            healthyCount={stack.healthyServices}
            totalCount={stack.totalServices}
            overallStatus={stack.overallStatus}
            linkTo={`/deployments/${encodeURIComponent(stack.stackName)}`}
          />
        ))}
      </div>

      {/* View All Link */}
      <Link
        to="/deployments"
        className="mt-4 inline-block text-sm font-medium text-brand-500 hover:text-brand-600 dark:text-brand-400"
      >
        View all deployments
      </Link>
    </div>
  );
}
