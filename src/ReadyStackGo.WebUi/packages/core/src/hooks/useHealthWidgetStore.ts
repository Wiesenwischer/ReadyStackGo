import { useState, useEffect, useMemo } from 'react';
import {
  getEnvironmentHealthSummary,
  type EnvironmentHealthSummaryDto,
  type StackHealthDto,
} from '../api/health';
import { useHealthHub, type ConnectionState } from '../realtime/useHealthHub';

export interface ProductHealthGroup {
  productDeploymentId: string;
  productDisplayName: string;
  stacks: StackHealthDto[];
  healthyStacks: number;
  totalStacks: number;
  overallStatus: string;
}

export interface UseHealthWidgetStoreReturn {
  healthSummary: EnvironmentHealthSummaryDto | null;
  loading: boolean;
  error: string | null;
  connectionState: ConnectionState;
  productGroups: ProductHealthGroup[];
  standaloneStacks: StackHealthDto[];
}

function aggregateProductStatus(stacks: StackHealthDto[]): string {
  const statuses = stacks.map(s => s.overallStatus.toLowerCase());
  if (statuses.some(s => s === 'unhealthy')) return 'Unhealthy';
  if (statuses.some(s => s === 'degraded')) return 'Degraded';
  if (statuses.every(s => s === 'healthy')) return 'Healthy';
  return 'Unknown';
}

export function useHealthWidgetStore(
  token: string | null,
  environmentId: string | undefined,
): UseHealthWidgetStoreReturn {
  const [healthSummary, setHealthSummary] = useState<EnvironmentHealthSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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

  // Group stacks by product, filter out stacks with no services
  const { productGroups, standaloneStacks } = useMemo(() => {
    if (!healthSummary) return { productGroups: [], standaloneStacks: [] };

    const groups = new Map<string, ProductHealthGroup>();
    const standalone: StackHealthDto[] = [];

    for (const stack of healthSummary.stacks) {
      if (stack.totalServices === 0) continue;

      if (stack.productDeploymentId && stack.productDisplayName) {
        const existing = groups.get(stack.productDeploymentId);
        const isHealthy = stack.overallStatus.toLowerCase() === 'healthy';
        if (existing) {
          existing.stacks.push(stack);
          existing.totalStacks += 1;
          if (isHealthy) existing.healthyStacks += 1;
        } else {
          groups.set(stack.productDeploymentId, {
            productDeploymentId: stack.productDeploymentId,
            productDisplayName: stack.productDisplayName,
            stacks: [stack],
            healthyStacks: isHealthy ? 1 : 0,
            totalStacks: 1,
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

    return { productGroups: productGroupList, standaloneStacks: standalone };
  }, [healthSummary]);

  return {
    healthSummary,
    loading,
    error,
    connectionState,
    productGroups,
    standaloneStacks,
  };
}
