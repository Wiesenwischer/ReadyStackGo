import { useState, useEffect, useCallback, useRef } from 'react';
import {
  getStackSources,
  getStackSource,
  createStackSource,
  deleteStackSource,
  getRegistrySources,
  addFromRegistry,
  updateStackSource,
  syncSource,
  syncAllSources,
  exportSources,
  importSources,
  type StackSourceDto,
  type StackSourceDetailDto,
  type CreateStackSourceRequest,
  type RegistrySourceDto,
} from '../api/stackSources';

export interface UseStackSourceStoreReturn {
  stackSources: StackSourceDto[];
  loading: boolean;
  error: string | null;
  actionLoading: string | null;
  fileInputRef: React.RefObject<HTMLInputElement | null>;
  refresh: () => Promise<void>;
  handleToggleSource: (source: StackSourceDto) => Promise<void>;
  handleSyncSource: (id: string) => Promise<void>;
  handleSyncAllSources: () => Promise<void>;
  handleExport: () => Promise<void>;
  handleImportFile: (e: React.ChangeEvent<HTMLInputElement>) => Promise<void>;
  clearError: () => void;
  getById: (id: string) => Promise<StackSourceDetailDto | null>;
  create: (request: CreateStackSourceRequest) => Promise<boolean>;
  remove: (id: string) => Promise<boolean>;
  getCuratedSources: () => Promise<RegistrySourceDto[]>;
  addFromCatalog: (sourceId: string) => Promise<boolean>;
}

export function useStackSourceStore(): UseStackSourceStoreReturn {
  const [stackSources, setStackSources] = useState<StackSourceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const refresh = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const sources = await getStackSources();
      setStackSources(sources);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load stack sources');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const handleToggleSource = useCallback(async (source: StackSourceDto) => {
    try {
      setActionLoading(source.id);
      const response = await updateStackSource(source.id, { enabled: !source.enabled });
      if (response.success) {
        await refresh();
      } else {
        setError(response.message || 'Failed to update stack source');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update stack source');
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const handleSyncSource = useCallback(async (id: string) => {
    try {
      setActionLoading(id);
      const result = await syncSource(id);
      if (result.success) {
        await refresh();
      } else {
        setError(result.errors.join(', ') || 'Failed to sync stack source');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to sync stack source');
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const handleSyncAllSources = useCallback(async () => {
    try {
      setActionLoading('all');
      const result = await syncAllSources();
      if (result.success) {
        await refresh();
      } else {
        setError(result.errors.join(', ') || 'Failed to sync stack sources');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to sync stack sources');
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const handleExport = useCallback(async () => {
    try {
      setActionLoading('export');
      const data = await exportSources();
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `rsgo-sources-${new Date().toISOString().slice(0, 10)}.json`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to export sources');
    } finally {
      setActionLoading(null);
    }
  }, []);

  const handleImportFile = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      setActionLoading('import');
      const text = await file.text();
      const data = JSON.parse(text);

      if (!data.version || !Array.isArray(data.sources)) {
        setError('Invalid import file format. Expected { version, sources[] }.');
        return;
      }

      const result = await importSources(data);
      if (result.success) {
        await refresh();
        setError(null);
      } else {
        setError(result.message || 'Failed to import sources');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to import sources');
    } finally {
      setActionLoading(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  }, [refresh]);

  const getById = useCallback(async (id: string): Promise<StackSourceDetailDto | null> => {
    try {
      setActionLoading(id);
      setError(null);
      return await getStackSource(id);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load stack source');
      return null;
    } finally {
      setActionLoading(null);
    }
  }, []);

  const create = useCallback(async (request: CreateStackSourceRequest): Promise<boolean> => {
    try {
      setActionLoading('creating');
      setError(null);
      const response = await createStackSource(request);
      if (response.success) {
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to create stack source');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create stack source');
      return false;
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const remove = useCallback(async (id: string): Promise<boolean> => {
    try {
      setActionLoading(id);
      setError(null);
      const response = await deleteStackSource(id);
      if (response.success) {
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to delete stack source');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete stack source');
      return false;
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const getCuratedSources = useCallback(async (): Promise<RegistrySourceDto[]> => {
    try {
      setActionLoading('catalog');
      setError(null);
      return await getRegistrySources();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load catalog');
      return [];
    } finally {
      setActionLoading(null);
    }
  }, []);

  const addFromCatalog = useCallback(async (sourceId: string): Promise<boolean> => {
    try {
      setActionLoading(sourceId);
      setError(null);
      const response = await addFromRegistry(sourceId);
      if (response.success) {
        await refresh();
        return true;
      } else {
        setError(response.message || 'Failed to add source from catalog');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add source from catalog');
      return false;
    } finally {
      setActionLoading(null);
    }
  }, [refresh]);

  const clearError = useCallback(() => setError(null), []);

  return {
    stackSources,
    loading,
    error,
    actionLoading,
    fileInputRef,
    refresh,
    handleToggleSource,
    handleSyncSource,
    handleSyncAllSources,
    handleExport,
    handleImportFile,
    clearError,
    getById,
    create,
    remove,
    getCuratedSources,
    addFromCatalog,
  };
}
