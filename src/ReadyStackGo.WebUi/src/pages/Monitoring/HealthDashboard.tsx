import { useEffect, useState, useCallback, useMemo } from 'react';
import { Link } from 'react-router';
import { useEnvironment } from '../../context/EnvironmentContext';
import { useHealthHub } from '../../hooks/useHealthHub';
import {
  getEnvironmentHealthSummary,
  getStackHealth,
  getHealthStatusPresentation,
  type EnvironmentHealthSummaryDto,
  type StackHealthSummaryDto,
  type StackHealthDto,
} from '../../api/health';
import HealthStackCard from '../../components/health/HealthStackCard';

type StatusFilter = 'all' | 'healthy' | 'degraded' | 'unhealthy';

interface ProductGroup {
  productDeploymentId: string;
  productDisplayName: string;
  stacks: StackHealthSummaryDto[];
  healthyStacks: number;
  totalStacks: number;
  healthyServices: number;
  totalServices: number;
  overallStatus: string;
}

function aggregateProductStatus(stacks: StackHealthSummaryDto[]): string {
  const statuses = stacks.map(s => s.overallStatus.toLowerCase());
  if (statuses.some(s => s === 'unhealthy')) return 'Unhealthy';
  if (statuses.some(s => s === 'degraded')) return 'Degraded';
  if (statuses.every(s => s === 'healthy')) return 'Healthy';
  return 'Unknown';
}

export default function HealthDashboard() {
  const { activeEnvironment } = useEnvironment();
  const [healthSummary, setHealthSummary] = useState<EnvironmentHealthSummaryDto | null>(null);
  const [detailedHealthMap, setDetailedHealthMap] = useState<Record<string, StackHealthDto>>({});
  const [loadingDetails, setLoadingDetails] = useState<Record<string, boolean>>({});
  const [expandedProducts, setExpandedProducts] = useState<Set<string>>(new Set());
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

  // Filter stacks — also match product display name in search
  const filteredStacks = useMemo(() => {
    return healthSummary?.stacks.filter((stack) => {
      if (statusFilter !== 'all') {
        if (stack.overallStatus.toLowerCase() !== statusFilter) {
          return false;
        }
      }
      if (searchQuery) {
        const query = searchQuery.toLowerCase();
        return (
          stack.stackName.toLowerCase().includes(query) ||
          stack.currentVersion?.toLowerCase().includes(query) ||
          stack.productDisplayName?.toLowerCase().includes(query)
        );
      }
      return true;
    }) ?? [];
  }, [healthSummary, statusFilter, searchQuery]);

  // Group filtered stacks by product
  const { productGroups, standaloneStacks, productCount, standaloneCount } = useMemo(() => {
    const groups = new Map<string, ProductGroup>();
    const standalone: StackHealthSummaryDto[] = [];

    for (const stack of filteredStacks) {
      if (stack.productDeploymentId && stack.productDisplayName) {
        const existing = groups.get(stack.productDeploymentId);
        const isHealthy = stack.overallStatus.toLowerCase() === 'healthy';
        if (existing) {
          existing.stacks.push(stack);
          existing.totalStacks += 1;
          existing.healthyServices += stack.healthyServices;
          existing.totalServices += stack.totalServices;
          if (isHealthy) existing.healthyStacks += 1;
        } else {
          groups.set(stack.productDeploymentId, {
            productDeploymentId: stack.productDeploymentId,
            productDisplayName: stack.productDisplayName,
            stacks: [stack],
            healthyStacks: isHealthy ? 1 : 0,
            totalStacks: 1,
            healthyServices: stack.healthyServices,
            totalServices: stack.totalServices,
            overallStatus: 'Healthy',
          });
        }
      } else {
        standalone.push(stack);
      }
    }

    const productGroupList = Array.from(groups.values()).map(g => ({
      ...g,
      overallStatus: aggregateProductStatus(g.stacks),
    }));

    return {
      productGroups: productGroupList,
      standaloneStacks: standalone,
      productCount: productGroupList.length,
      standaloneCount: standalone.length,
    };
  }, [filteredStacks]);

  const toggleProductExpanded = (productId: string) => {
    setExpandedProducts(prev => {
      const next = new Set(prev);
      if (next.has(productId)) {
        next.delete(productId);
      } else {
        next.add(productId);
      }
      return next;
    });
  };

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

  const hasAnyResults = productGroups.length > 0 || standaloneStacks.length > 0;

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
            className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Refresh
          </button>
        </div>
      </div>

      {/* Summary Cards — product-aware */}
      {healthSummary && (
        <div className="mb-6">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <SummaryCard
              label="Healthy"
              count={healthSummary.healthyCount}
              bgColor="bg-green-50 dark:bg-green-900/20"
              textColor="text-green-600 dark:text-green-400"
              labelColor="text-green-700 dark:text-green-300"
              dotColor="bg-green-500"
            />
            <SummaryCard
              label="Degraded"
              count={healthSummary.degradedCount}
              bgColor="bg-yellow-50 dark:bg-yellow-900/20"
              textColor="text-yellow-600 dark:text-yellow-400"
              labelColor="text-yellow-700 dark:text-yellow-300"
              dotColor="bg-yellow-500"
            />
            <SummaryCard
              label="Unhealthy"
              count={healthSummary.unhealthyCount}
              bgColor="bg-red-50 dark:bg-red-900/20"
              textColor="text-red-600 dark:text-red-400"
              labelColor="text-red-700 dark:text-red-300"
              dotColor="bg-red-500"
            />
            <div className="rounded-xl p-4 bg-gray-50 dark:bg-gray-800/50 transition-all hover:scale-[1.02]">
              <div className="flex items-center gap-2 mb-2">
                <span className="h-2.5 w-2.5 rounded-full bg-gray-500" />
                <span className="text-sm font-medium text-gray-700 dark:text-gray-300">Total</span>
              </div>
              <div className="text-3xl font-bold text-gray-600 dark:text-gray-400">
                {healthSummary.totalStacks}
              </div>
              {productCount > 0 && (
                <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {productCount} {productCount === 1 ? 'product' : 'products'}, {standaloneCount} standalone
                </div>
              )}
            </div>
          </div>
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
            placeholder="Search stacks or products..."
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

      {/* Stack List — grouped by product */}
      {!hasAnyResults ? (
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
          {/* Product groups */}
          {productGroups.map((group) => (
            <ProductGroupCard
              key={group.productDeploymentId}
              group={group}
              isExpanded={expandedProducts.has(group.productDeploymentId)}
              onToggle={() => toggleProductExpanded(group.productDeploymentId)}
              detailedHealthMap={detailedHealthMap}
              loadingDetails={loadingDetails}
              onExpandStack={handleExpandStack}
            />
          ))}

          {/* Standalone stacks section */}
          {standaloneStacks.length > 0 && productGroups.length > 0 && (
            <div className="pt-2">
              <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-3">
                Standalone Stacks
              </h3>
            </div>
          )}
          {standaloneStacks.map((stack) => (
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

// --- Sub-components ---

function SummaryCard({ label, count, bgColor, textColor, labelColor, dotColor }: {
  label: string;
  count: number;
  bgColor: string;
  textColor: string;
  labelColor: string;
  dotColor: string;
}) {
  return (
    <div className={`rounded-xl p-4 ${bgColor} transition-all hover:scale-[1.02]`}>
      <div className="flex items-center gap-2 mb-2">
        <span className={`h-2.5 w-2.5 rounded-full ${dotColor}`} />
        <span className={`text-sm font-medium ${labelColor}`}>{label}</span>
      </div>
      <div className={`text-3xl font-bold ${textColor}`}>{count}</div>
    </div>
  );
}

function ProductGroupCard({ group, isExpanded, onToggle, detailedHealthMap, loadingDetails, onExpandStack }: {
  group: ProductGroup;
  isExpanded: boolean;
  onToggle: () => void;
  detailedHealthMap: Record<string, StackHealthDto>;
  loadingDetails: Record<string, boolean>;
  onExpandStack: (deploymentId: string) => void;
}) {
  const statusPresentation = getHealthStatusPresentation(group.overallStatus);

  return (
    <div className="rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
      {/* Product group header */}
      <button
        onClick={onToggle}
        className="w-full px-4 py-4 flex items-center justify-between hover:bg-gray-50 dark:hover:bg-gray-800/30 transition-colors cursor-pointer"
      >
        <div className="flex items-center gap-3">
          {/* Product icon */}
          <div className={`h-8 w-8 rounded-lg flex items-center justify-center ${statusPresentation.bgColor}`}>
            <svg className={`w-4 h-4 ${statusPresentation.textColor}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
            </svg>
          </div>
          <div className="text-left">
            <span className="font-semibold text-gray-900 dark:text-white">
              {group.productDisplayName}
            </span>
            <div className="text-xs text-gray-500 dark:text-gray-400">
              {group.totalStacks} {group.totalStacks === 1 ? 'stack' : 'stacks'} &middot; {group.healthyServices}/{group.totalServices} services
            </div>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <span className="text-sm text-gray-600 dark:text-gray-400">
            {group.healthyStacks}/{group.totalStacks} healthy
          </span>
          <span
            className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${statusPresentation.bgColor} ${statusPresentation.textColor}`}
          >
            {statusPresentation.label}
          </span>
          <svg
            className={`w-5 h-5 text-gray-400 transition-transform ${isExpanded ? 'rotate-180' : ''}`}
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
          </svg>
        </div>
      </button>

      {/* Expanded: show individual stacks */}
      {isExpanded && (
        <div className="border-t border-gray-200 dark:border-gray-800">
          <div className="pl-4 space-y-0">
            {group.stacks.map((stack) => (
              <HealthStackCard
                key={stack.deploymentId}
                stack={stack}
                detailedHealth={detailedHealthMap[stack.deploymentId]}
                onExpand={onExpandStack}
                isLoading={loadingDetails[stack.deploymentId]}
              />
            ))}
          </div>
          <div className="px-4 py-3 bg-gray-50 dark:bg-gray-800/30 flex justify-end">
            <Link
              to={`/product-deployments/${encodeURIComponent(group.productDeploymentId)}`}
              className="text-sm font-medium text-brand-500 hover:text-brand-600 dark:text-brand-400"
            >
              View Product Details
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
