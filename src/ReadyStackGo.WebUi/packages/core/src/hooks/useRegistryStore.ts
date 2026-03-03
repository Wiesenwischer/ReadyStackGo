import { useState, useEffect, useCallback } from 'react';
import {
  getRegistries,
  setDefaultRegistry,
  type RegistryDto,
} from '../api/registries';

export interface UseRegistryStoreReturn {
  registries: RegistryDto[];
  loading: boolean;
  error: string | null;
  actionLoading: string | null;
  refresh: () => Promise<void>;
  handleSetDefault: (id: string) => Promise<void>;
  clearError: () => void;
}

export function useRegistryStore(): UseRegistryStoreReturn {
  const [registries, setRegistries] = useState<RegistryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await getRegistries();
      setRegistries(response.registries);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load registries');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const handleSetDefault = useCallback(async (id: string) => {
    try {
      setActionLoading(id);
      const response = await setDefaultRegistry(id);
      if (response.success) {
        await refresh();
      } else {
        setError(response.message || 'Failed to set default registry');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to set default registry');
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const clearError = useCallback(() => setError(null), []);

  return {
    registries,
    loading,
    error,
    actionLoading,
    refresh,
    handleSetDefault,
    clearError,
  };
}
