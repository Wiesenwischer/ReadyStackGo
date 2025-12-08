import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { useEnvironment } from '../../context/EnvironmentContext';
import { useHealthHub } from '../../hooks/useHealthHub';
import {
  getEnvironmentHealthSummary,
  getHealthStatusPresentation,
  type EnvironmentHealthSummaryDto,
  type StackHealthSummaryDto,
} from '../../api/health';

interface HealthWidgetProps {
  className?: string;
}

export default function HealthWidget({ className = '' }: HealthWidgetProps) {
  const { activeEnvironment } = useEnvironment();
  const [healthSummary, setHealthSummary] = useState<EnvironmentHealthSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // SignalR for real-time updates
  const { connectionState, subscribeToEnvironment, unsubscribeFromEnvironment } = useHealthHub({
    onEnvironmentHealthChanged: (summary) => {
      if (summary.environmentId === activeEnvironment?.id) {
        setHealthSummary(summary);
      }
    },
    onDeploymentHealthChanged: (health) => {
      // Update individual deployment in current summary
      setHealthSummary((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          stacks: prev.stacks.map((s) =>
            s.deploymentId === health.deploymentId ? health : s
          ),
          healthyCount: prev.stacks.filter(s =>
            s.deploymentId === health.deploymentId
              ? health.overallStatus.toLowerCase() === 'healthy'
              : s.overallStatus.toLowerCase() === 'healthy'
          ).length,
          degradedCount: prev.stacks.filter(s =>
            s.deploymentId === health.deploymentId
              ? health.overallStatus.toLowerCase() === 'degraded'
              : s.overallStatus.toLowerCase() === 'degraded'
          ).length,
          unhealthyCount: prev.stacks.filter(s =>
            s.deploymentId === health.deploymentId
              ? health.overallStatus.toLowerCase() === 'unhealthy'
              : s.overallStatus.toLowerCase() === 'unhealthy'
          ).length,
        };
      });
    },
  });

  // Fetch initial data
  useEffect(() => {
    if (!activeEnvironment) {
      setHealthSummary(null);
      setLoading(false);
      return;
    }

    const fetchHealth = async () => {
      try {
        setLoading(true);
        const data = await getEnvironmentHealthSummary(activeEnvironment.id);
        setHealthSummary(data);
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load health data');
      } finally {
        setLoading(false);
      }
    };

    fetchHealth();
  }, [activeEnvironment]);

  // Subscribe to SignalR updates
  useEffect(() => {
    if (connectionState === 'connected' && activeEnvironment) {
      subscribeToEnvironment(activeEnvironment.id);
      return () => {
        unsubscribeFromEnvironment(activeEnvironment.id);
      };
    }
  }, [connectionState, activeEnvironment, subscribeToEnvironment, unsubscribeFromEnvironment]);

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

  const StackHealthRow = ({ stack }: { stack: StackHealthSummaryDto }) => {
    const presentation = getHealthStatusPresentation(stack.overallStatus);
    return (
      <div className="flex items-center justify-between py-2 border-b border-gray-100 dark:border-gray-800 last:border-0">
        <div className="flex items-center gap-2">
          <span className={`h-2 w-2 rounded-full ${presentation.bgColor.replace('bg-', 'bg-').replace('-100', '-500')}`} />
          <span className="text-sm font-medium text-gray-900 dark:text-white truncate max-w-[140px]">
            {stack.stackName}
          </span>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {stack.healthyServices}/{stack.totalServices}
          </span>
          <HealthStatusBadge status={stack.overallStatus} />
        </div>
      </div>
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

      {/* Stack List */}
      <div className="max-h-[200px] overflow-y-auto">
        {healthSummary.stacks.map((stack) => (
          <StackHealthRow key={stack.deploymentId} stack={stack} />
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
