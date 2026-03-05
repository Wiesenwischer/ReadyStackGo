import { useState, useEffect, useCallback, useMemo } from 'react';
import { volumeApi, type Volume } from '../api/volumes';

export interface UseVolumeStoreReturn {
  // State
  volumes: Volume[];
  loading: boolean;
  error: string | null;
  showOrphanedOnly: boolean;
  actionLoading: string | null;
  confirmDelete: string | null;
  confirmBulkDelete: boolean;
  showCreateForm: boolean;
  createName: string;
  createDriver: string;
  createLoading: boolean;

  // Derived data
  filteredVolumes: Volume[];
  orphanedCount: number;

  // Actions
  refresh: () => void;
  handleDelete: (name: string, force: boolean) => Promise<void>;
  handleBulkDeleteOrphaned: () => Promise<void>;
  handleCreate: (e: React.FormEvent) => Promise<void>;
  setShowOrphanedOnly: (value: boolean) => void;
  setConfirmDelete: (name: string | null) => void;
  setConfirmBulkDelete: (value: boolean) => void;
  setShowCreateForm: (value: boolean) => void;
  setCreateName: (value: string) => void;
  setCreateDriver: (value: string) => void;

  // Helpers
  formatDate: (dateStr?: string) => string;
}

export function useVolumeStore(
  environmentId: string | undefined,
): UseVolumeStoreReturn {
  const [volumes, setVolumes] = useState<Volume[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showOrphanedOnly, setShowOrphanedOnly] = useState(false);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [confirmBulkDelete, setConfirmBulkDelete] = useState(false);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [createName, setCreateName] = useState('');
  const [createDriver, setCreateDriver] = useState('');
  const [createLoading, setCreateLoading] = useState(false);

  const loadVolumes = useCallback(async () => {
    if (!environmentId) {
      setVolumes([]);
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await volumeApi.list(environmentId);
      setVolumes(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load volumes');
    } finally {
      setLoading(false);
    }
  }, [environmentId]);

  useEffect(() => {
    loadVolumes();
  }, [loadVolumes]);

  const handleDelete = useCallback(async (name: string, force: boolean = false) => {
    if (!environmentId) return;

    try {
      setActionLoading(name);
      await volumeApi.remove(environmentId, name, force);
      setConfirmDelete(null);
      await loadVolumes();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to remove volume');
    } finally {
      setActionLoading(null);
    }
  }, [environmentId, loadVolumes]);

  const handleBulkDeleteOrphaned = useCallback(async () => {
    if (!environmentId) return;

    const orphaned = volumes.filter((v) => v.isOrphaned);
    setConfirmBulkDelete(false);
    setActionLoading('bulk');

    try {
      for (const vol of orphaned) {
        await volumeApi.remove(environmentId, vol.name, false);
      }
      await loadVolumes();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to remove orphaned volumes');
    } finally {
      setActionLoading(null);
    }
  }, [environmentId, volumes, loadVolumes]);

  const handleCreate = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!environmentId || !createName.trim()) return;

    try {
      setCreateLoading(true);
      setError(null);
      await volumeApi.create(environmentId, {
        name: createName.trim(),
        driver: createDriver.trim() || undefined,
      });
      setCreateName('');
      setCreateDriver('');
      setShowCreateForm(false);
      await loadVolumes();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create volume');
    } finally {
      setCreateLoading(false);
    }
  }, [environmentId, createName, createDriver, loadVolumes]);

  const filteredVolumes = useMemo(
    () => (showOrphanedOnly ? volumes.filter((v) => v.isOrphaned) : volumes),
    [volumes, showOrphanedOnly],
  );

  const orphanedCount = useMemo(
    () => volumes.filter((v) => v.isOrphaned).length,
    [volumes],
  );

  const formatDate = useCallback((dateStr?: string) => {
    if (!dateStr) return '-';
    const date = new Date(dateStr);
    return date.toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }, []);

  return {
    volumes,
    loading,
    error,
    showOrphanedOnly,
    actionLoading,
    confirmDelete,
    confirmBulkDelete,
    showCreateForm,
    createName,
    createDriver,
    createLoading,
    filteredVolumes,
    orphanedCount,
    refresh: loadVolumes,
    handleDelete,
    handleBulkDeleteOrphaned,
    handleCreate,
    setShowOrphanedOnly,
    setConfirmDelete,
    setConfirmBulkDelete,
    setShowCreateForm,
    setCreateName,
    setCreateDriver,
    formatDate,
  };
}
