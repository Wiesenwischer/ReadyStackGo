import { useState, useEffect, useCallback } from 'react';
import { getHealthTransitions, type HealthTransitionDto } from '../api/health';

export interface UseHealthTransitionsStoreReturn {
  transitions: HealthTransitionDto[];
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useHealthTransitionsStore(
  deploymentId: string | undefined,
): UseHealthTransitionsStoreReturn {
  const [transitions, setTransitions] = useState<HealthTransitionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchTransitions = useCallback(async () => {
    if (!deploymentId) {
      setTransitions([]);
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      const data = await getHealthTransitions(deploymentId);
      setTransitions(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load transitions');
    } finally {
      setLoading(false);
    }
  }, [deploymentId]);

  useEffect(() => {
    fetchTransitions();
  }, [fetchTransitions]);

  return { transitions, loading, error, refresh: fetchTransitions };
}
