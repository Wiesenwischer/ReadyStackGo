import { useState, useEffect, useCallback } from 'react';
import {
  getDeployment,
  getRollbackInfo,
  checkUpgrade,
  markDeploymentFailed,
  type GetDeploymentResponse,
  type RollbackInfoResponse,
  type CheckUpgradeResponse,
} from '../api/deployments';
import {
  getHealthStatusPresentation,
  getOperationModePresentation,
  getStackHealth,
  enterMaintenanceMode,
  exitMaintenanceMode,
  type StackHealthDto,
  type StatusPresentation,
} from '../api/health';
import { useHealthHub, type ConnectionState } from '../realtime/useHealthHub';

export interface UseDeploymentDetailStoreReturn {
  // State
  deployment: GetDeploymentResponse | null;
  health: StackHealthDto | null;
  loading: boolean;
  error: string | null;
  modeActionLoading: boolean;
  modeActionError: string | null;
  rollbackInfo: RollbackInfoResponse | null;
  upgradeInfo: CheckUpgradeResponse | null;
  markFailedLoading: boolean;
  markFailedError: string | null;
  connectionState: ConnectionState;

  // Computed
  statusPresentation: StatusPresentation;
  modePresentation: StatusPresentation | null;

  // Actions
  handleEnterMaintenance: () => Promise<void>;
  handleExitMaintenance: () => Promise<void>;
  handleMarkAsFailed: () => Promise<void>;
  clearModeActionError: () => void;

  // Helpers
  formatDate: (dateString?: string) => string;
}

export function useDeploymentDetailStore(
  token: string | null,
  environmentId: string | undefined,
  stackName: string | undefined,
): UseDeploymentDetailStoreReturn {
  const [deployment, setDeployment] = useState<GetDeploymentResponse | null>(null);
  const [health, setHealth] = useState<StackHealthDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modeActionLoading, setModeActionLoading] = useState(false);
  const [modeActionError, setModeActionError] = useState<string | null>(null);

  // Rollback state
  const [rollbackInfo, setRollbackInfo] = useState<RollbackInfoResponse | null>(null);

  // Upgrade state
  const [upgradeInfo, setUpgradeInfo] = useState<CheckUpgradeResponse | null>(null);

  // Mark as Failed state
  const [markFailedLoading, setMarkFailedLoading] = useState(false);
  const [markFailedError, setMarkFailedError] = useState<string | null>(null);

  // SignalR Health Hub connection
  const { connectionState, subscribeToDeployment, unsubscribeFromDeployment } = useHealthHub(token, {
    onDeploymentHealthChanged: (healthData) => {
      if (deployment?.deploymentId && healthData.deploymentId === deployment.deploymentId) {
        setHealth(healthData);
      }
    }
  });

  // Subscribe to deployment when connected and deployment is loaded
  useEffect(() => {
    const deploymentId = deployment?.deploymentId;
    if (deploymentId && connectionState === 'connected') {
      subscribeToDeployment(deploymentId);
      return () => {
        unsubscribeFromDeployment(deploymentId);
      };
    }
  }, [deployment?.deploymentId, connectionState, subscribeToDeployment, unsubscribeFromDeployment]);

  // Load deployment + health data
  useEffect(() => {
    const loadDeployment = async () => {
      if (!environmentId || !stackName) {
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        setError(null);
        const response = await getDeployment(environmentId, decodeURIComponent(stackName));
        if (response.success) {
          setDeployment(response);

          // Load health data (force refresh for immediate data)
          if (response.deploymentId) {
            try {
              const healthData = await getStackHealth(environmentId, response.deploymentId, true);
              setHealth(healthData);
            } catch (healthErr) {
              console.warn("Could not load health data:", healthErr);
            }
          }
        } else {
          setError(response.message || "Deployment not found");
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load deployment");
      } finally {
        setLoading(false);
      }
    };

    loadDeployment();
  }, [environmentId, stackName]);

  // Load rollback info when deployment is loaded
  useEffect(() => {
    const loadRollbackInfo = async () => {
      if (!environmentId || !deployment?.deploymentId) return;

      try {
        const info = await getRollbackInfo(environmentId, deployment.deploymentId);
        setRollbackInfo(info);
      } catch (err) {
        console.warn("Could not load rollback info:", err);
      }
    };

    loadRollbackInfo();
  }, [environmentId, deployment?.deploymentId]);

  // Load upgrade info when deployment is loaded
  useEffect(() => {
    const loadUpgradeInfo = async () => {
      if (!environmentId || !deployment?.deploymentId) return;

      try {
        const info = await checkUpgrade(environmentId, deployment.deploymentId);
        setUpgradeInfo(info);
      } catch (err) {
        console.warn("Could not load upgrade info:", err);
      }
    };

    loadUpgradeInfo();
  }, [environmentId, deployment?.deploymentId]);

  const formatDate = useCallback((dateString?: string) => {
    if (!dateString) return "-";
    const date = new Date(dateString);
    return date.toLocaleDateString() + " " + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }, []);

  const handleEnterMaintenance = useCallback(async () => {
    if (!deployment?.deploymentId || !environmentId) return;
    try {
      setModeActionLoading(true);
      setModeActionError(null);
      const response = await enterMaintenanceMode(environmentId, deployment.deploymentId);
      if (!response.success) {
        setModeActionError(response.message || 'Failed to enter maintenance mode');
      } else {
        // Refresh health data from server
        try {
          const healthData = await getStackHealth(environmentId, deployment.deploymentId, true);
          setHealth(healthData);
        } catch {
          // Optimistic fallback
          setHealth(prev => prev ? { ...prev, operationMode: 'Maintenance' } : prev);
        }
      }
    } catch (err) {
      setModeActionError(err instanceof Error ? err.message : 'Failed to enter maintenance mode');
    } finally {
      setModeActionLoading(false);
    }
  }, [deployment?.deploymentId, environmentId]);

  const handleExitMaintenance = useCallback(async () => {
    if (!deployment?.deploymentId || !environmentId) return;
    try {
      setModeActionLoading(true);
      setModeActionError(null);
      const response = await exitMaintenanceMode(environmentId, deployment.deploymentId);
      if (!response.success) {
        setModeActionError(response.message || 'Failed to exit maintenance mode');
      } else {
        // Refresh health data from server
        try {
          const healthData = await getStackHealth(environmentId, deployment.deploymentId, true);
          setHealth(healthData);
        } catch {
          // Optimistic fallback
          setHealth(prev => prev ? { ...prev, operationMode: 'Normal' } : prev);
        }
      }
    } catch (err) {
      setModeActionError(err instanceof Error ? err.message : 'Failed to exit maintenance mode');
    } finally {
      setModeActionLoading(false);
    }
  }, [deployment?.deploymentId, environmentId]);

  const handleMarkAsFailed = useCallback(async () => {
    if (!deployment?.deploymentId || !environmentId) return;
    try {
      setMarkFailedLoading(true);
      setMarkFailedError(null);
      const response = await markDeploymentFailed(
        environmentId,
        deployment.deploymentId,
        "Manually marked as failed by user"
      );
      if (!response.success) {
        setMarkFailedError(response.message || 'Failed to mark deployment as failed');
      } else {
        // Refresh the deployment data to get updated status
        const refreshed = await getDeployment(environmentId, decodeURIComponent(stackName || ''));
        if (refreshed.success) {
          setDeployment(refreshed);
        }
        // Also reload rollback info since status changed
        if (deployment.deploymentId) {
          try {
            const info = await getRollbackInfo(environmentId, deployment.deploymentId);
            setRollbackInfo(info);
          } catch {
            // Ignore
          }
        }
      }
    } catch (err) {
      setMarkFailedError(err instanceof Error ? err.message : 'Failed to mark deployment as failed');
    } finally {
      setMarkFailedLoading(false);
    }
  }, [deployment?.deploymentId, environmentId, stackName]);

  const clearModeActionError = useCallback(() => setModeActionError(null), []);

  // Computed values
  const statusPresentation = health
    ? getHealthStatusPresentation(health.overallStatus)
    : getHealthStatusPresentation('unknown');

  const modePresentation = health
    ? getOperationModePresentation(health.operationMode)
    : null;

  return {
    deployment,
    health,
    loading,
    error,
    modeActionLoading,
    modeActionError,
    rollbackInfo,
    upgradeInfo,
    markFailedLoading,
    markFailedError,
    connectionState,
    statusPresentation,
    modePresentation,
    handleEnterMaintenance,
    handleExitMaintenance,
    handleMarkAsFailed,
    clearModeActionError,
    formatDate,
  };
}
