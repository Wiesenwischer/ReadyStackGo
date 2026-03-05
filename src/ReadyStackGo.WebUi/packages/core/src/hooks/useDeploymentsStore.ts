import { useState, useEffect, useCallback } from 'react';
import {
  listDeployments,
  listProductDeployments,
  type DeploymentSummary,
  type ProductDeploymentSummaryDto,
} from '../api/deployments';
import { type StackHealthDto } from '../api/health';
import { useHealthHub, type ConnectionState } from '../realtime/useHealthHub';

export interface UseDeploymentsStoreReturn {
  // State
  deployments: DeploymentSummary[];
  productDeployments: ProductDeploymentSummaryDto[];
  healthData: Map<string, StackHealthDto>;
  loading: boolean;
  error: string | null;
  connectionState: ConnectionState;

  // Actions
  refresh: () => Promise<void>;

  // Helpers
  formatDate: (dateString: string) => string;
}

export function useDeploymentsStore(
  token: string | null,
  environmentId: string | undefined,
): UseDeploymentsStoreReturn {
  const [deployments, setDeployments] = useState<DeploymentSummary[]>([]);
  const [productDeployments, setProductDeployments] = useState<ProductDeploymentSummaryDto[]>([]);
  const [healthData, setHealthData] = useState<Map<string, StackHealthDto>>(new Map());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // SignalR Health Hub connection
  const { connectionState, subscribeToEnvironment, unsubscribeFromEnvironment } = useHealthHub(token, {
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
    if (environmentId && connectionState === 'connected') {
      subscribeToEnvironment(environmentId);
      return () => {
        unsubscribeFromEnvironment(environmentId);
      };
    }
  }, [environmentId, connectionState, subscribeToEnvironment, unsubscribeFromEnvironment]);

  const loadDeployments = useCallback(async () => {
    if (!environmentId) {
      setDeployments([]);
      setProductDeployments([]);
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const [stackResponse, productResponse] = await Promise.all([
        listDeployments(environmentId),
        listProductDeployments(environmentId),
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
  }, [environmentId]);

  useEffect(() => {
    loadDeployments();
  }, [loadDeployments]);

  const formatDate = useCallback((dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString() + " " + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }, []);

  return {
    deployments,
    productDeployments,
    healthData,
    loading,
    error,
    connectionState,
    refresh: loadDeployments,
    formatDate,
  };
}
