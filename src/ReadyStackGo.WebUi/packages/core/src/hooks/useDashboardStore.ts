import { useState, useEffect, useCallback } from 'react';
import { dashboardApi, type DashboardStats } from '../api/dashboard';

export interface UseDashboardStoreReturn {
  stats: DashboardStats | null;
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

const POLL_INTERVAL = 10000;

export function useDashboardStore(
  environmentId: string | undefined,
): UseDashboardStoreReturn {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchStats = useCallback(async () => {
    if (!environmentId) {
      setStats(null);
      setLoading(false);
      return;
    }

    try {
      const data = await dashboardApi.getStats(environmentId);
      setStats(data);
      if (data.errorMessage) {
        setError(data.errorMessage);
      } else {
        setError(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred');
    } finally {
      setLoading(false);
    }
  }, [environmentId]);

  useEffect(() => {
    fetchStats();
    const interval = setInterval(fetchStats, POLL_INTERVAL);
    return () => clearInterval(interval);
  }, [fetchStats]);

  return {
    stats,
    loading,
    error,
    refresh: fetchStats,
  };
}
