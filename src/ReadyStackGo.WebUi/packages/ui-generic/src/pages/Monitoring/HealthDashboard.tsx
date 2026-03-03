import { Link } from 'react-router';
import { useEnvironment } from '../../context/EnvironmentContext';
import { useAuth } from '../../context/AuthContext';
import {
  useHealthDashboardStore,
  getHealthStatusPresentation,
  type ProductGroup,
  type StatusFilter,
} from '@rsgo/core';
import HealthStackCard from '../../components/health/HealthStackCard';

export default function HealthDashboard() {
  const { activeEnvironment } = useEnvironment();
  const { token } = useAuth();
  const store = useHealthDashboardStore(token, activeEnvironment?.id);

  // Connection indicator
  const getConnectionIndicator = () => {
    switch (store.connectionState) {
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

  if (store.loading) {
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

  if (store.error) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-xl border border-red-200 bg-red-50 p-6 dark:border-red-800 dark:bg-red-900/20">
          <h2 className="text-lg font-semibold text-red-800 dark:text-red-300 mb-2">
            Error Loading Health Data
          </h2>
          <p className="text-sm text-red-600 dark:text-red-400">{store.error}</p>
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

  const hasAnyResults = store.productGroups.length > 0 || store.standaloneStacks.length > 0;

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

      {/* Summary Cards -- product-aware */}
      {store.healthSummary && (
        <div className="mb-6">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <SummaryCard
              label="Healthy"
              count={store.healthSummary.healthyCount}
              bgColor="bg-green-50 dark:bg-green-900/20"
              textColor="text-green-600 dark:text-green-400"
              labelColor="text-green-700 dark:text-green-300"
              dotColor="bg-green-500"
            />
            <SummaryCard
              label="Degraded"
              count={store.healthSummary.degradedCount}
              bgColor="bg-yellow-50 dark:bg-yellow-900/20"
              textColor="text-yellow-600 dark:text-yellow-400"
              labelColor="text-yellow-700 dark:text-yellow-300"
              dotColor="bg-yellow-500"
            />
            <SummaryCard
              label="Unhealthy"
              count={store.healthSummary.unhealthyCount}
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
                {store.healthSummary.totalStacks}
              </div>
              {store.productCount > 0 && (
                <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {store.productCount} {store.productCount === 1 ? 'product' : 'products'}, {store.standaloneCount} standalone
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
                onClick={() => store.setStatusFilter(filter)}
                className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                  store.statusFilter === filter
                    ? 'bg-brand-500 text-white'
                    : 'bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700'
                }`}
              >
                {filter.charAt(0).toUpperCase() + filter.slice(1)}
                {filter !== 'all' && store.healthSummary && (
                  <span className="ml-1.5 opacity-70">
                    (
                    {filter === 'healthy'
                      ? store.healthSummary.healthyCount
                      : filter === 'degraded'
                      ? store.healthSummary.degradedCount
                      : store.healthSummary.unhealthyCount}
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
            value={store.searchQuery}
            onChange={(e) => store.setSearchQuery(e.target.value)}
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

      {/* Stack List -- grouped by product */}
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
            {store.healthSummary?.totalStacks === 0
              ? 'No Deployments'
              : 'No Matching Stacks'}
          </h3>
          <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
            {store.healthSummary?.totalStacks === 0
              ? 'Deploy a stack to start monitoring its health.'
              : 'Try adjusting your filters or search query.'}
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {/* Product groups */}
          {store.productGroups.map((group) => (
            <ProductGroupCard
              key={group.productDeploymentId}
              group={group}
              isExpanded={store.expandedProducts.has(group.productDeploymentId)}
              onToggle={() => store.toggleProductExpanded(group.productDeploymentId)}
            />
          ))}

          {/* Standalone stacks section */}
          {store.standaloneStacks.length > 0 && store.productGroups.length > 0 && (
            <div className="pt-2">
              <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-3">
                Standalone Stacks
              </h3>
            </div>
          )}
          {store.standaloneStacks.map((stack) => (
            <HealthStackCard
              key={stack.deploymentId}
              stack={stack}
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

function ProductGroupCard({ group, isExpanded, onToggle }: {
  group: ProductGroup;
  isExpanded: boolean;
  onToggle: () => void;
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
