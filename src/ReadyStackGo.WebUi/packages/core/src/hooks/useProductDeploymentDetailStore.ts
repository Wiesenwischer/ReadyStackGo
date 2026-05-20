import { useState, useEffect, useCallback, useRef } from 'react';
import {
  getProductDeployment,
  type GetProductDeploymentResponse,
} from '../api/deployments';
import { useHealthHub } from '../realtime/useHealthHub';


export type ProductDeploymentDetailState = 'loading' | 'loaded' | 'error';

export interface UseProductDeploymentDetailStoreReturn {
  state: ProductDeploymentDetailState;
  deployment: GetProductDeploymentResponse | null;
  error: string | null;
  showVariables: boolean;
  toggleVariables: () => void;
  formatDate: (dateString: string) => string;
  formatDuration: (seconds?: number) => string;
  formatStackDuration: (startedAt?: string, completedAt?: string) => string;
}

export function useProductDeploymentDetailStore(
  token: string | null,
  environmentId: string | undefined,
  productDeploymentId: string | undefined,
): UseProductDeploymentDetailStoreReturn {
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showVariables, setShowVariables] = useState(false);
  // Stack deployment ids that belong to the currently loaded product, used to
  // decide whether an incoming HealthHub event is relevant to this page.
  const relevantDeploymentIdsRef = useRef<Set<string>>(new Set());

  const loadDeployment = useCallback(async () => {
    if (!environmentId || !productDeploymentId) {
      setError(!environmentId ? 'No active environment' : 'No deployment ID provided');
      setLoading(false);
      return;
    }

    try {
      // Don't flip the loading flag on refreshes — the UI then keeps showing
      // the previous content until the new data arrives.
      setError(null);
      const data = await getProductDeployment(environmentId, productDeploymentId);
      setDeployment(data);
      relevantDeploymentIdsRef.current = new Set(
        (data.stacks ?? [])
          .map((s: { deploymentId?: string | null }) => s.deploymentId)
          .filter((id: string | null | undefined): id is string => Boolean(id))
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load product deployment');
    } finally {
      setLoading(false);
    }
  }, [environmentId, productDeploymentId]);

  useEffect(() => {
    setLoading(true);
    loadDeployment();
  }, [loadDeployment]);

  // Live-update: subscribe to the environment health hub and refresh whenever
  // a stack belonging to this product changes its health/operation mode. The
  // backend also pushes these for OperationMode transitions, so this catches
  // observer-triggered maintenance flips and manual Enter/Exit Maintenance
  // actions performed elsewhere (issue #393).
  const healthHub = useHealthHub(token, {
    onDeploymentHealthChanged: (health) => {
      const id = (health as { deploymentId?: string }).deploymentId;
      if (!id) return;
      if (relevantDeploymentIdsRef.current.has(id)) {
        loadDeployment();
      }
    },
  });

  useEffect(() => {
    if (!environmentId) return;
    if (healthHub.connectionState !== 'connected') return;
    healthHub.subscribeToEnvironment(environmentId);
    return () => {
      void healthHub.unsubscribeFromEnvironment(environmentId);
    };
  }, [healthHub, environmentId, healthHub.connectionState]);

  const toggleVariables = useCallback(() => {
    setShowVariables(prev => !prev);
  }, []);

  const formatDate = useCallback((dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }, []);

  const formatDuration = useCallback((seconds?: number): string => {
    if (seconds == null) return '-';
    const rounded = Math.round(seconds);
    if (rounded < 60) return `${rounded}s`;
    const minutes = Math.floor(rounded / 60);
    const remainingSeconds = rounded % 60;
    return remainingSeconds > 0 ? `${minutes}m ${remainingSeconds}s` : `${minutes}m`;
  }, []);

  const formatStackDuration = useCallback((startedAt?: string, completedAt?: string): string => {
    if (!startedAt || !completedAt) return '-';
    const seconds = (new Date(completedAt).getTime() - new Date(startedAt).getTime()) / 1000;
    return formatDuration(seconds);
  }, [formatDuration]);

  const state: ProductDeploymentDetailState = loading ? 'loading' : error ? 'error' : 'loaded';

  return {
    state,
    deployment,
    error,
    showVariables,
    toggleVariables,
    formatDate,
    formatDuration,
    formatStackDuration,
  };
}
