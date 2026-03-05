import { useState, useEffect, useCallback } from 'react';
import {
  getApiKeys,
  createApiKey,
  revokeApiKey,
  type ApiKeyDto,
  type CreateApiKeyRequest,
} from '../api/apiKeys';

export interface UseApiKeyStoreReturn {
  apiKeys: ApiKeyDto[];
  loading: boolean;
  error: string | null;
  actionLoading: boolean;
  createdKey: string | null;
  refresh: () => Promise<void>;
  handleCreate: (request: CreateApiKeyRequest) => Promise<boolean>;
  handleRevoke: (keyId: string, reason?: string) => Promise<boolean>;
  clearError: () => void;
  clearCreatedKey: () => void;
}

export function useApiKeyStore(): UseApiKeyStoreReturn {
  const [apiKeys, setApiKeys] = useState<ApiKeyDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [createdKey, setCreatedKey] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await getApiKeys();
      setApiKeys(response.apiKeys);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load API keys');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const handleCreate = useCallback(async (request: CreateApiKeyRequest): Promise<boolean> => {
    try {
      setActionLoading(true);
      setError(null);
      const response = await createApiKey(request);
      if (response.success && response.apiKey) {
        setCreatedKey(response.apiKey.fullKey);
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to create API key');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create API key');
      return false;
    } finally {
      setActionLoading(false);
    }
  }, [refresh]);

  const handleRevoke = useCallback(async (keyId: string, reason?: string): Promise<boolean> => {
    try {
      setActionLoading(true);
      setError(null);
      const response = await revokeApiKey(keyId, reason);
      if (response.success) {
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to revoke API key');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to revoke API key');
      return false;
    } finally {
      setActionLoading(false);
    }
  }, [refresh]);

  const clearError = useCallback(() => setError(null), []);
  const clearCreatedKey = useCallback(() => setCreatedKey(null), []);

  return {
    apiKeys,
    loading,
    error,
    actionLoading,
    createdKey,
    refresh,
    handleCreate,
    handleRevoke,
    clearError,
    clearCreatedKey,
  };
}
