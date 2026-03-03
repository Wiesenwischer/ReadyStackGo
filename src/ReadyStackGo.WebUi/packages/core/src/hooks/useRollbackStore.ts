import { useState, useRef, useCallback, useEffect } from 'react';
import { getDeployment, getRollbackInfo, rollbackDeployment } from '../api/deployments';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { GetDeploymentResponse, RollbackInfoResponse } from '../api/deployments';
import type { DeploymentProgressUpdate, InitContainerLogEntry, ConnectionState } from '../realtime/useDeploymentHub';

// Format phase names for display (PullingImages -> Pulling Images)
const formatPhase = (phase: string | undefined): string => {
  if (!phase) return '';
  return phase.replace(/([A-Z])/g, ' $1').trim();
};

export type RollbackState = 'loading' | 'confirm' | 'rolling_back' | 'success' | 'error';

export interface UseRollbackStoreReturn {
  // State
  state: RollbackState;
  deployment: GetDeploymentResponse | null;
  rollbackInfo: RollbackInfoResponse | null;
  error: string;
  progressUpdate: DeploymentProgressUpdate | null;
  initContainerLogs: Record<string, string[]>;
  connectionState: ConnectionState;

  // Computed
  formattedPhase: string;

  // Actions
  handleRollback: () => Promise<void>;
}

export function useRollbackStore(
  token: string | null,
  environmentId: string | undefined,
  stackName: string | undefined,
): UseRollbackStoreReturn {
  const [state, setState] = useState<RollbackState>('loading');
  const [deployment, setDeployment] = useState<GetDeploymentResponse | null>(null);
  const [rollbackInfo, setRollbackInfo] = useState<RollbackInfoResponse | null>(null);
  const [error, setError] = useState('');

  // Progress state
  const rollbackSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);
  const [initContainerLogs, setInitContainerLogs] = useState<Record<string, string[]>>({});

  // SignalR hub for real-time rollback progress
  const handleRollbackProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = rollbackSessionIdRef.current;
    if (currentSessionId && update.sessionId === currentSessionId) {
      setProgressUpdate(update);

      if (update.isComplete) {
        if (update.isError) {
          setError(update.errorMessage || 'Rollback failed');
          setState('error');
        } else {
          setState('success');
        }
      }
    }
  }, []);

  const handleInitContainerLog = useCallback((log: InitContainerLogEntry) => {
    const currentSessionId = rollbackSessionIdRef.current;
    if (currentSessionId && log.sessionId === currentSessionId) {
      setInitContainerLogs(prev => ({
        ...prev,
        [log.containerName]: [...(prev[log.containerName] || []), log.logLine]
      }));
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub(token, {
    onDeploymentProgress: handleRollbackProgress,
    onInitContainerLog: handleInitContainerLog,
  });

  // Load deployment and rollback info
  useEffect(() => {
    if (!stackName || !environmentId) {
      return;
    }

    const loadData = async () => {
      try {
        setState('loading');
        setError('');

        // Load deployment
        const deploymentResponse = await getDeployment(environmentId, decodeURIComponent(stackName));
        if (!deploymentResponse.success || !deploymentResponse.deploymentId) {
          setError(deploymentResponse.message || 'Deployment not found');
          setState('error');
          return;
        }
        setDeployment(deploymentResponse);

        // Load rollback info
        const rollbackInfoResponse = await getRollbackInfo(environmentId, deploymentResponse.deploymentId);
        setRollbackInfo(rollbackInfoResponse);

        if (!rollbackInfoResponse.canRollback) {
          setError('Rollback is not available for this deployment. The deployment must be in a Failed state to rollback.');
          setState('error');
          return;
        }

        setState('confirm');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load rollback data');
        setState('error');
      }
    };

    loadData();
  }, [stackName, environmentId]);

  const handleRollback = useCallback(async () => {
    if (!environmentId || !deployment?.deploymentId) {
      setError('Missing required data');
      return;
    }

    // Generate session ID before API call
    const sessionId = `rollback-${deployment.stackName}-${Date.now()}`;
    rollbackSessionIdRef.current = sessionId;

    setState('rolling_back');
    setError('');
    setProgressUpdate(null);
    setInitContainerLogs({});

    // Subscribe to SignalR before starting
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    try {
      const response = await rollbackDeployment(environmentId, deployment.deploymentId, { sessionId });

      if (!response.success) {
        setError(response.message || 'Rollback failed');
        setState('error');
        return;
      }

      // State will be set to 'success' by SignalR callback
      // But if no SignalR connection, set it immediately
      if (connectionState !== 'connected') {
        setState('success');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Rollback failed');
      setState('error');
    }
  }, [environmentId, deployment, connectionState, subscribeToDeployment]);

  return {
    state,
    deployment,
    rollbackInfo,
    error,
    progressUpdate,
    initContainerLogs,
    connectionState,
    formattedPhase: formatPhase(progressUpdate?.phase),
    handleRollback,
  };
}
