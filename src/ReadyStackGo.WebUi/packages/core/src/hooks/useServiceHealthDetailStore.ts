import { useState, useEffect, useCallback } from 'react';
import {
  getServiceHealth,
  type ServiceHealthDetailResult,
} from '../api/health';

export interface UseServiceHealthDetailStoreReturn {
  result: ServiceHealthDetailResult | null;
  loading: boolean;
  error: string | null;
  refresh: (forceRefresh?: boolean) => void;
}

export function useServiceHealthDetailStore(
  environmentId: string | undefined,
  deploymentId: string | undefined,
  serviceName: string | undefined,
): UseServiceHealthDetailStoreReturn {
  const [result, setResult] = useState<ServiceHealthDetailResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(
    async (forceRefresh = false) => {
      if (!environmentId || !deploymentId || !serviceName) return;

      try {
        setLoading(true);
        setError(null);
        const data = await getServiceHealth(
          environmentId,
          deploymentId,
          decodeURIComponent(serviceName),
          forceRefresh,
        );
        setResult(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load service health');
      } finally {
        setLoading(false);
      }
    },
    [environmentId, deploymentId, serviceName],
  );

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Auto-refresh every 30 seconds
  useEffect(() => {
    const interval = setInterval(() => loadData(), 30000);
    return () => clearInterval(interval);
  }, [loadData]);

  return { result, loading, error, refresh: loadData };
}
