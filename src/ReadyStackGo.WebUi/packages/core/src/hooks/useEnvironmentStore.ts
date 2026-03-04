import { useState, useEffect, useCallback } from 'react';
import {
  getEnvironments,
  getEnvironment,
  createEnvironment,
  deleteEnvironment,
  setDefaultEnvironment,
  type EnvironmentResponse,
  type CreateEnvironmentRequest,
} from '../api/environments';
import { getWizardStatus } from '../api/wizard';

export interface UseEnvironmentStoreReturn {
  environments: EnvironmentResponse[];
  loading: boolean;
  error: string | null;
  actionLoading: string | null;
  defaultSocketPath: string;
  refresh: () => Promise<void>;
  loadDefaultSocketPath: () => Promise<void>;
  handleSetDefault: (id: string) => Promise<boolean>;
  clearError: () => void;
  getById: (id: string) => Promise<EnvironmentResponse | null>;
  create: (request: CreateEnvironmentRequest) => Promise<boolean>;
  remove: (id: string) => Promise<boolean>;
}

export function useEnvironmentStore(): UseEnvironmentStoreReturn {
  const [environments, setEnvironments] = useState<EnvironmentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [defaultSocketPath, setDefaultSocketPath] = useState('');

  const loadDefaultSocketPath = useCallback(async () => {
    try {
      const status = await getWizardStatus();
      setDefaultSocketPath(status.defaultDockerSocketPath || 'unix:///var/run/docker.sock');
    } catch {
      setDefaultSocketPath('unix:///var/run/docker.sock');
    }
  }, []);

  const refresh = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await getEnvironments();
      if (response.success) {
        setEnvironments(response.environments);
      } else {
        setError('Failed to load environments');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load environments');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const handleSetDefault = useCallback(async (id: string): Promise<boolean> => {
    try {
      setActionLoading(id);
      setError(null);
      const response = await setDefaultEnvironment(id);
      if (response.success) {
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to set default environment');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to set default environment');
      return false;
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const getById = useCallback(async (id: string): Promise<EnvironmentResponse | null> => {
    try {
      setActionLoading(id);
      setError(null);
      const env = await getEnvironment(id);
      return env;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load environment');
      return null;
    } finally {
      setActionLoading(null);
    }
  }, []);

  const create = useCallback(async (request: CreateEnvironmentRequest): Promise<boolean> => {
    try {
      setActionLoading('creating');
      setError(null);
      const response = await createEnvironment(request);
      if (response.success) {
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to create environment');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create environment');
      return false;
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const remove = useCallback(async (id: string): Promise<boolean> => {
    try {
      setActionLoading(id);
      setError(null);
      const response = await deleteEnvironment(id);
      if (response.success) {
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to delete environment');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete environment');
      return false;
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const clearError = useCallback(() => setError(null), []);

  return {
    environments,
    loading,
    error,
    actionLoading,
    defaultSocketPath,
    refresh,
    loadDefaultSocketPath,
    handleSetDefault,
    clearError,
    getById,
    create,
    remove,
  };
}
