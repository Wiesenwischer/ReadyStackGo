import { useEffect, useState, useCallback } from 'react';
import { useEnvironment } from '../../context/EnvironmentContext';
import { useHealthHub } from '../../hooks/useHealthHub';
import {
  getEnvironmentHealthSummary,
  getStackHealth,
  type EnvironmentHealthSummaryDto,
  type StackHealthDto,
} from '../../api/health';
import HealthSummaryCards from '../../components/health/HealthSummaryCards';
import HealthStackCard from '../../components/health/HealthStackCard';

type StatusFilter = 'all' | 'healthy' | 'degraded' | 'unhealthy';

export default function HealthDashboard() {
  const { activeEnvironment } = useEnvironment();
  const [healthSummary, setHealthSummary] = useState<EnvironmentHealthSummaryDto | null>(null);
  const [detailedHealthMap, setDetailedHealthMap] = useState<Record<string, StackHealthDto>>({});
  const [loadingDetails, setLoadingDetails] = useState<Record<string, boolean>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [searchQuery, setSearchQuery] = useState('');

  // SignalR for real-time updates
  const { connectionState, subscribeToEnvironment, unsubscribeFromEnvironment } = useHealthHub({
    onEnvironmentHealthChanged: (summary) => {
      if (summary.environmentId === activeEnvironment?.id) {
        setHealthSummary(summary);
      }
    },
    onDeploymentHealthChanged: (health) => {
      setHealthSummary((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          stacks: prev.stacks.map((s) =>
            s.deploymentId === health.deploymentId ? health : s
          ),
          healthyCount: prev.stacks.filter((s) =>
            s.deploymentId === health.deploymentId
              ? health.overallStatus.toLowerCase() === 'healthy'
              : s.overallStatus.toLowerCase() === 'healthy'
          ).length,
          degradedCount: prev.stacks.filter((s) =>
            s.deploymentId === health.deploymentId
              ? health.overallStatus.toLowerCase() === 'degraded'
              : s.overallStatus.toLowerCase() === 'degraded'
          ).length,
          unhealthyCount: prev.stacks.filter((s) =>
            s.deploymentId === health.deploymentId
              ? health.overallStatus.toLowerCase() === 'unhealthy'
              : s.overallStatus.toLowerCase() === 'unhealthy'
          ).length,
        };
      });
    },
    onDeploymentDetailedHealthChanged: (healthData) => {
      setDetailedHealthMap((prev) => ({
        ...prev,
        [healthData.deploymentId]: healthData,
      }));
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

  // Load detailed health when a card is expanded
  const handleExpandStack = useCallback(
    async (deploymentId: string) => {
      if (!activeEnvironment || detailedHealthMap[deploymentId]) return;

      setLoadingDetails((prev) => ({ ...prev, [deploymentId]: true }));
      try {
        const healthData = await getStackHealth(activeEnvironment.id, deploymentId, true);
        setDetailedHealthMap((prev) => ({
          ...prev,
          [deploymentId]: healthData,
        }));
      } catch (err) {
        console.error('Failed to load detailed health:', err);
      } finally {
        setLoadingDetails((prev) => ({ ...prev, [deploymentId]: false }));
      }
    },
    [activeEnvironment, detailedHealthMap]
  );

  // Filter stacks
  const filteredStacks = healthSummary?.stacks.filter((stack) => {
    // Status filter
    if (statusFilter !== 'all') {
      if (stack.overallStatus.toLowerCase() !== statusFilter) {
        return false;
      }
    }

    // Search filter
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      return (
        stack.stackName.toLowerCase().includes(query) ||
        stack.currentVersion?.toLowerCase().includes(query)
      );
    }

    return true;
  }) ?? [];

  // Connection indicator
  const getConnectionIndicator = () => {
    switch (connectionState) {
      case 'connected':
        return (
          <span className="flex items-center gap-1.5 text-xs text-green-600 dark:text-green-400">
            <span className="h-2 w-2 rounded-full bg-green-500 animate-pulse" />
            Live
          </span>
        );
      case 'connecting':
      case 'reconnecting':
        return (
          <span className="flex items-center gap-1.5 text-xs text-yellow-600 dark:text-yellow-400">
            <span className="h-2 w-2 rounded-full bg-yellow-500 animate-pulse" />
            Connecting...
          </span>
        );
      default:
        return (
          <span className="flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-400">
            <span className="h-2 w-2 rounded-full bg-gray-400" />
            Offline
          </span>
        );
    }
  };

  if (loading) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="animate-pulse">
          <div className="h-8 bg-gray-200 dark:bg-gray-700 rounded w-1/4 mb-6" />
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-6">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="h-24 bg-gray-200 dark:bg-gray-700 rounded-xl" />
            ))}
          </div>
          <div className="space-y-4">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-20 bg-gray-200 dark:bg-gray-700 rounded-xl" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-xl border border-red-200 bg-red-50 p-6 dark:border-red-800 dark:bg-red-900/20">
          <h2 className="text-lg font-semibold text-red-800 dark:text-red-300 mb-2">
            Error Loading Health Data
          </h2>
          <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
        </div>
      </div>
    );
  }

  if (!activeEnvironment) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
            No Environment Selected
          </h2>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            Please select an environment to view health status.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Header */}
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
            Health Dashboard
          </h1>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            Monitor the health of all deployments in {activeEnvironment.name}
          </p>
        </div>
        <div className="flex items-center gap-3">
          {getConnectionIndicator()}
          <button
            onClick={() => window.location.reload()}
            className="inline-flex items-center gap-2 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700"
          >
            <svg
              className="h-4 w-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
              />
            </svg>
            Refresh
          </button>
        </div>
      </div>

      {/* Summary Cards */}
      {healthSummary && (
        <div className="mb-6">
          <HealthSummaryCards
            healthyCount={healthSummary.healthyCount}
            degradedCount={healthSummary.degradedCount}
            unhealthyCount={healthSummary.unhealthyCount}
            totalCount={healthSummary.totalStacks}
          />
        </div>
      )}

      {/* Filters */}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-wrap gap-2">
          {(['all', 'healthy', 'degraded', 'unhealthy'] as StatusFilter[]).map(
            (filter) => (
              <button
                key={filter}
                onClick={() => setStatusFilter(filter)}
                className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                  statusFilter === filter
                    ? 'bg-brand-500 text-white'
                    : 'bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700'
                }`}
              >
                {filter.charAt(0).toUpperCase() + filter.slice(1)}
                {filter !== 'all' && healthSummary && (
                  <span className="ml-1.5 opacity-70">
                    (
                    {filter === 'healthy'
                      ? healthSummary.healthyCount
                      : filter === 'degraded'
                      ? healthSummary.degradedCount
                      : healthSummary.unhealthyCount}
                    )
                  </span>
                )}
              </button>
            )
          )}
        </div>
        <div className="relative">
          <input
            type="text"
            placeholder="Search stacks..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full sm:w-64 rounded-lg border border-gray-300 bg-white px-4 py-2 pl-10 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
          />
          <svg
            className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
            />
          </svg>
        </div>
      </div>

      {/* Stack List */}
      {filteredStacks.length === 0 ? (
        <div className="rounded-xl border border-gray-200 bg-white p-8 text-center dark:border-gray-800 dark:bg-white/[0.03]">
          <svg
            className="mx-auto h-12 w-12 text-gray-400"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={1.5}
              d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
          <h3 className="mt-4 text-lg font-medium text-gray-900 dark:text-white">
            {healthSummary?.totalStacks === 0
              ? 'No Deployments'
              : 'No Matching Stacks'}
          </h3>
          <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
            {healthSummary?.totalStacks === 0
              ? 'Deploy a stack to start monitoring its health.'
              : 'Try adjusting your filters or search query.'}
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {filteredStacks.map((stack) => (
            <HealthStackCard
              key={stack.deploymentId}
              stack={stack}
              detailedHealth={detailedHealthMap[stack.deploymentId]}
              onExpand={handleExpandStack}
              isLoading={loadingDetails[stack.deploymentId]}
            />
          ))}
        </div>
      )}
    </div>
  );
}
