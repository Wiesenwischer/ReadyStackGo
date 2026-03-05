import { useState, useEffect, useCallback } from 'react';
import { volumeApi, type Volume } from '../api/volumes';

export type VolumeDeleteState = 'idle' | 'confirm' | 'deleting';

export interface UseVolumeDetailStoreReturn {
  volume: Volume | null;
  loading: boolean;
  error: string | null;
  deleteState: VolumeDeleteState;
  setDeleteState: (state: VolumeDeleteState) => void;
  refresh: () => void;
  handleDelete: () => Promise<void>;
  formatSize: (bytes?: number) => string;
  formatDate: (dateStr?: string) => string;
}

export function useVolumeDetailStore(
  environmentId: string | undefined,
  volumeName: string | undefined,
  onDeleted: () => void,
): UseVolumeDetailStoreReturn {
  const [volume, setVolume] = useState<Volume | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deleteState, setDeleteState] = useState<VolumeDeleteState>('idle');

  const loadVolume = useCallback(async () => {
    if (!environmentId || !volumeName) {
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await volumeApi.get(environmentId, decodeURIComponent(volumeName));
      setVolume(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load volume');
    } finally {
      setLoading(false);
    }
  }, [environmentId, volumeName]);

  useEffect(() => {
    loadVolume();
  }, [loadVolume]);

  const handleDelete = useCallback(async () => {
    if (!environmentId || !volume) return;

    try {
      setDeleteState('deleting');
      await volumeApi.remove(environmentId, volume.name, volume.containerCount > 0);
      onDeleted();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to remove volume');
      setDeleteState('idle');
    }
  }, [environmentId, volume, onDeleted]);

  const formatSize = useCallback((bytes?: number) => {
    if (bytes === undefined || bytes === null) return 'Unknown';
    if (bytes === 0) return '0 B';
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${units[i]}`;
  }, []);

  const formatDate = useCallback((dateStr?: string) => {
    if (!dateStr) return '-';
    const date = new Date(dateStr);
    return date.toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  }, []);

  return {
    volume,
    loading,
    error,
    deleteState,
    setDeleteState,
    refresh: loadVolume,
    handleDelete,
    formatSize,
    formatDate,
  };
}
