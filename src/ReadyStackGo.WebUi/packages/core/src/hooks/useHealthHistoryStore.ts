import { useState, useEffect, useCallback } from 'react';
import { getHealthHistory, type StackHealthSummaryDto } from '../api/health';

export interface UseHealthHistoryStoreReturn {
  history: StackHealthSummaryDto[];
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useHealthHistoryStore(
  deploymentId: string | undefined,
): UseHealthHistoryStoreReturn {
  const [history, setHistory] = useState<StackHealthSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchHistory = useCallback(async () => {
    if (!deploymentId) {
      setHistory([]);
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      const data = await getHealthHistory(deploymentId, 100);
      setHistory(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load history');
    } finally {
      setLoading(false);
    }
  }, [deploymentId]);

  useEffect(() => {
    fetchHistory();
  }, [fetchHistory]);

  return { history, loading, error, refresh: fetchHistory };
}
