import { useState, useEffect, useCallback } from 'react';
import {
  getProductDeployment,
  type GetProductDeploymentResponse,
} from '../api/deployments';
import {
  enterProductMaintenanceMode,
  exitProductMaintenanceMode,
} from '../api/health';

export type ProductDeploymentDetailState = 'loading' | 'loaded' | 'error';

export interface UseProductDeploymentDetailStoreReturn {
  state: ProductDeploymentDetailState;
  deployment: GetProductDeploymentResponse | null;
  error: string | null;
  showVariables: boolean;
  toggleVariables: () => void;
  formatDate: (dateString: string) => string;
  formatDuration: (seconds?: number) => string;
  formatStackDuration: (startedAt?: string, completedAt?: string) => string;
  // Maintenance mode
  modeActionLoading: boolean;
  modeActionError: string | null;
  handleEnterMaintenance: (reason?: string) => Promise<void>;
  handleExitMaintenance: () => Promise<void>;
  clearModeActionError: () => void;
}

export function useProductDeploymentDetailStore(
  _token: string | null,
  environmentId: string | undefined,
  productDeploymentId: string | undefined,
): UseProductDeploymentDetailStoreReturn {
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showVariables, setShowVariables] = useState(false);
  const [modeActionLoading, setModeActionLoading] = useState(false);
  const [modeActionError, setModeActionError] = useState<string | null>(null);

  const loadDeployment = useCallback(async () => {
    if (!environmentId || !productDeploymentId) {
      setError(!environmentId ? 'No active environment' : 'No deployment ID provided');
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const data = await getProductDeployment(environmentId, productDeploymentId);
      setDeployment(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load product deployment');
    } finally {
      setLoading(false);
    }
  }, [environmentId, productDeploymentId]);

  useEffect(() => {
    loadDeployment();
  }, [loadDeployment]);

  const toggleVariables = useCallback(() => {
    setShowVariables(prev => !prev);
  }, []);

  const formatDate = useCallback((dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }, []);

  const formatDuration = useCallback((seconds?: number): string => {
    if (seconds == null) return '-';
    const rounded = Math.round(seconds);
    if (rounded < 60) return `${rounded}s`;
    const minutes = Math.floor(rounded / 60);
    const remainingSeconds = rounded % 60;
    return remainingSeconds > 0 ? `${minutes}m ${remainingSeconds}s` : `${minutes}m`;
  }, []);

  const formatStackDuration = useCallback((startedAt?: string, completedAt?: string): string => {
    if (!startedAt || !completedAt) return '-';
    const seconds = (new Date(completedAt).getTime() - new Date(startedAt).getTime()) / 1000;
    return formatDuration(seconds);
  }, [formatDuration]);

  const handleEnterMaintenance = useCallback(async (reason?: string) => {
    if (!environmentId || !productDeploymentId) return;
    setModeActionLoading(true);
    setModeActionError(null);
    try {
      const response = await enterProductMaintenanceMode(environmentId, productDeploymentId, reason);
      if (!response.success) {
        setModeActionError(response.message || 'Failed to enter maintenance mode');
        return;
      }
      await loadDeployment();
    } catch (err) {
      setModeActionError(err instanceof Error ? err.message : 'Failed to enter maintenance mode');
    } finally {
      setModeActionLoading(false);
    }
  }, [environmentId, productDeploymentId, loadDeployment]);

  const handleExitMaintenance = useCallback(async () => {
    if (!environmentId || !productDeploymentId) return;
    setModeActionLoading(true);
    setModeActionError(null);
    try {
      const response = await exitProductMaintenanceMode(environmentId, productDeploymentId);
      if (!response.success) {
        setModeActionError(response.message || 'Failed to exit maintenance mode');
        return;
      }
      await loadDeployment();
    } catch (err) {
      setModeActionError(err instanceof Error ? err.message : 'Failed to exit maintenance mode');
    } finally {
      setModeActionLoading(false);
    }
  }, [environmentId, productDeploymentId, loadDeployment]);

  const clearModeActionError = useCallback(() => {
    setModeActionError(null);
  }, []);

  const state: ProductDeploymentDetailState = loading ? 'loading' : error ? 'error' : 'loaded';

  return {
    state,
    deployment,
    error,
    showVariables,
    toggleVariables,
    formatDate,
    formatDuration,
    formatStackDuration,
    modeActionLoading,
    modeActionError,
    handleEnterMaintenance,
    handleExitMaintenance,
    clearModeActionError,
  };
}
