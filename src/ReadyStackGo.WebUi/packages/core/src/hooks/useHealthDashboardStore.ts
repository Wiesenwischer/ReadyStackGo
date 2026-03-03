import { useState, useEffect, useMemo, useCallback } from 'react';
import {
  getEnvironmentHealthSummary,
  type EnvironmentHealthSummaryDto,
  type StackHealthDto,
} from '../api/health';
import { useHealthHub, type ConnectionState } from '../realtime/useHealthHub';

export type StatusFilter = 'all' | 'healthy' | 'degraded' | 'unhealthy';

export interface ProductGroup {
  productDeploymentId: string;
  productDisplayName: string;
  stacks: StackHealthDto[];
  healthyStacks: number;
  totalStacks: number;
  healthyServices: number;
  totalServices: number;
  overallStatus: string;
}

export interface UseHealthDashboardStoreReturn {
  // State
  healthSummary: EnvironmentHealthSummaryDto | null;
  loading: boolean;
  error: string | null;
  connectionState: ConnectionState;

  // Filters
  statusFilter: StatusFilter;
  setStatusFilter: (filter: StatusFilter) => void;
  searchQuery: string;
  setSearchQuery: (query: string) => void;

  // Derived data
  filteredStacks: StackHealthDto[];
  productGroups: ProductGroup[];
  standaloneStacks: StackHealthDto[];
  productCount: number;
  standaloneCount: number;

  // Expanded state
  expandedProducts: Set<string>;
  toggleProductExpanded: (productId: string) => void;
}

function aggregateProductStatus(stacks: StackHealthDto[]): string {
  const statuses = stacks.map(s => s.overallStatus.toLowerCase());
  if (statuses.some(s => s === 'unhealthy')) return 'Unhealthy';
  if (statuses.some(s => s === 'degraded')) return 'Degraded';
  if (statuses.every(s => s === 'healthy')) return 'Healthy';
  return 'Unknown';
}

export function useHealthDashboardStore(
  token: string | null,
  environmentId: string | undefined,
): UseHealthDashboardStoreReturn {
  const [healthSummary, setHealthSummary] = useState<EnvironmentHealthSummaryDto | null>(null);
  const [expandedProducts, setExpandedProducts] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [searchQuery, setSearchQuery] = useState('');

  // SignalR for real-time updates
  const { connectionState, subscribeToEnvironment, unsubscribeFromEnvironment } = useHealthHub(token, {
    onEnvironmentHealthChanged: (summary) => {
      if (summary.environmentId === environmentId) {
        setHealthSummary(summary);
      }
    },
    onDeploymentHealthChanged: (health) => {
      setHealthSummary((prev) => {
        if (!prev) return prev;
        const updatedStacks = prev.stacks.map((s) =>
          s.deploymentId === health.deploymentId ? health : s
        );
        return {
          ...prev,
          stacks: updatedStacks,
          healthyCount: updatedStacks.filter(s => s.overallStatus.toLowerCase() === 'healthy').length,
          degradedCount: updatedStacks.filter(s => s.overallStatus.toLowerCase() === 'degraded').length,
          unhealthyCount: updatedStacks.filter(s => s.overallStatus.toLowerCase() === 'unhealthy').length,
        };
      });
    },
  });

  // Fetch initial data
  useEffect(() => {
    if (!environmentId) {
      setHealthSummary(null);
      setLoading(false);
      return;
    }

    const fetchHealth = async () => {
      try {
        setLoading(true);
        const data = await getEnvironmentHealthSummary(environmentId);
        setHealthSummary(data);
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load health data');
      } finally {
        setLoading(false);
      }
    };

    fetchHealth();
  }, [environmentId]);

  // Subscribe to SignalR updates
  useEffect(() => {
    if (connectionState === 'connected' && environmentId) {
      subscribeToEnvironment(environmentId);
      return () => {
        unsubscribeFromEnvironment(environmentId);
      };
    }
  }, [connectionState, environmentId, subscribeToEnvironment, unsubscribeFromEnvironment]);

  // Filter stacks -- also match product display name in search
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
    const standalone: StackHealthDto[] = [];

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

  const toggleProductExpanded = useCallback((productId: string) => {
    setExpandedProducts(prev => {
      const next = new Set(prev);
      if (next.has(productId)) {
        next.delete(productId);
      } else {
        next.add(productId);
      }
      return next;
    });
  }, []);

  return {
    healthSummary,
    loading,
    error,
    connectionState,
    statusFilter,
    setStatusFilter,
    searchQuery,
    setSearchQuery,
    filteredStacks,
    productGroups,
    standaloneStacks,
    productCount,
    standaloneCount,
    expandedProducts,
    toggleProductExpanded,
  };
}
