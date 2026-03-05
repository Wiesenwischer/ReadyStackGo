import { useState, useEffect, useCallback } from 'react';
import {
  getRegistries,
  getRegistry,
  createRegistry,
  updateRegistry,
  deleteRegistry,
  setDefaultRegistry,
  type RegistryDto,
  type CreateRegistryRequest,
  type UpdateRegistryRequest,
} from '../api/registries';

export interface UseRegistryStoreReturn {
  registries: RegistryDto[];
  loading: boolean;
  error: string | null;
  actionLoading: string | null;
  refresh: () => Promise<void>;
  handleSetDefault: (id: string) => Promise<void>;
  clearError: () => void;
  getById: (id: string) => Promise<RegistryDto | null>;
  create: (request: CreateRegistryRequest) => Promise<boolean>;
  update: (id: string, request: UpdateRegistryRequest) => Promise<boolean>;
  remove: (id: string) => Promise<boolean>;
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

  const getById = useCallback(async (id: string): Promise<RegistryDto | null> => {
    try {
      setActionLoading(id);
      setError(null);
      const response = await getRegistry(id);
      if (response.success && response.registry) {
        return response.registry;
      } else {
        setError(response.message || 'Registry not found');
        return null;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load registry');
      return null;
    } finally {
      setActionLoading(null);
    }
  }, []);

  const create = useCallback(async (request: CreateRegistryRequest): Promise<boolean> => {
    try {
      setActionLoading('creating');
      setError(null);
      const response = await createRegistry(request);
      if (response.success) {
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to create registry');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create registry');
      return false;
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const update = useCallback(async (id: string, request: UpdateRegistryRequest): Promise<boolean> => {
    try {
      setActionLoading(id);
      setError(null);
      const response = await updateRegistry(id, request);
      if (response.success) {
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to update registry');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update registry');
      return false;
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const remove = useCallback(async (id: string): Promise<boolean> => {
    try {
      setActionLoading(id);
      setError(null);
      const response = await deleteRegistry(id);
      if (response.success) {
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to delete registry');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete registry');
      return false;
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
    getById,
    create,
    update,
    remove,
  };
}
