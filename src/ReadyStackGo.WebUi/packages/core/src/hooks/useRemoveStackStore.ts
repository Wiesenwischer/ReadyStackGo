import { useState, useRef, useCallback, useEffect } from 'react';
import { getDeployment, removeDeployment } from '../api/deployments';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { GetDeploymentResponse } from '../api/deployments';
import type { DeploymentProgressUpdate, ConnectionState } from '../realtime/useDeploymentHub';

// Format phase names for display (RemovingContainers -> Removing Containers)
const formatPhase = (phase: string | undefined): string => {
  if (!phase) return '';
  return phase.replace(/([A-Z])/g, ' $1').trim();
};

export type RemoveState = 'loading' | 'confirm' | 'removing' | 'success' | 'error';

export interface UseRemoveStackStoreReturn {
  // State
  state: RemoveState;
  deployment: GetDeploymentResponse | null;
  error: string;
  progressUpdate: DeploymentProgressUpdate | null;
  connectionState: ConnectionState;

  // Computed
  formattedPhase: string;

  // Actions
  handleRemove: () => Promise<void>;
}

export function useRemoveStackStore(
  token: string | null,
  environmentId: string | undefined,
  stackName: string | undefined,
): UseRemoveStackStoreReturn {
  const [state, setState] = useState<RemoveState>('loading');
  const [deployment, setDeployment] = useState<GetDeploymentResponse | null>(null);
  const [error, setError] = useState('');

  // Progress state
  const removeSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);

  // Prevent race condition: first completion (SignalR or API) wins
  const completedRef = useRef(false);

  // SignalR hub for real-time progress
  const handleRemoveProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = removeSessionIdRef.current;
    if (currentSessionId && update.sessionId === currentSessionId) {
      setProgressUpdate(update);

      // Check if removal completed (success or error) — first completion wins
      if (update.isComplete && !completedRef.current) {
        completedRef.current = true;
        if (update.isError) {
          setError(update.errorMessage || 'Removal failed');
          setState('error');
        } else {
          setState('success');
        }
      }
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub(token, {
    onDeploymentProgress: handleRemoveProgress,
  });

  // Load deployment details
  useEffect(() => {
    if (!environmentId || !stackName) {
      setState('error');
      setError('No environment or stack name provided');
      return;
    }

    const loadDeployment = async () => {
      try {
        setState('loading');
        setError('');

        const response = await getDeployment(environmentId, decodeURIComponent(stackName));
        if (response.success) {
          setDeployment(response);
          setState('confirm');
        } else {
          setError(response.message || 'Deployment not found');
          setState('error');
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load deployment');
        setState('error');
      }
    };

    loadDeployment();
  }, [environmentId, stackName]);

  const handleRemove = useCallback(async () => {
    if (!environmentId || !deployment?.deploymentId) {
      setError('No deployment to remove');
      return;
    }

    // Generate session ID BEFORE the API call
    const sessionId = `remove-${deployment.stackName}-${Date.now()}`;
    removeSessionIdRef.current = sessionId;
    completedRef.current = false;

    setState('removing');
    setError('');
    setProgressUpdate(null);

    // Subscribe to SignalR group BEFORE starting the API call
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    // Fire-and-forget: Don't block on API response.
    // SignalR delivers live progress; API response serves as fallback.
    removeDeployment(environmentId, deployment.deploymentId, { sessionId })
      .then(response => {
        // Only drive state if SignalR hasn't already completed
        if (!completedRef.current) {
          completedRef.current = true;
          if (!response.success) {
            setError(response.errors?.join('\n') || response.message || 'Removal failed');
            setState('error');
          } else {
            setState('success');
          }
        }
      })
      .catch(err => {
        if (!completedRef.current) {
          completedRef.current = true;
          setError(err instanceof Error ? err.message : 'Removal failed');
          setState('error');
        }
      });
  }, [environmentId, deployment, connectionState, subscribeToDeployment]);

  return {
    state,
    deployment,
    error,
    progressUpdate,
    connectionState,
    formattedPhase: formatPhase(progressUpdate?.phase),
    handleRemove,
  };
}
