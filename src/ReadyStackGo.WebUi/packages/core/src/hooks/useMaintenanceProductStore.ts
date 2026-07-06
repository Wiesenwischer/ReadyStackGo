import { useState, useEffect, useCallback, useRef } from 'react';
import {
  getProductDeployment,
  type GetProductDeploymentResponse,
} from '../api/deployments';
import {
  enterProductMaintenanceMode,
  exitProductMaintenanceMode,
  type ChangeProductOperationModeResponse,
} from '../api/health';
import { useDeploymentHub, type MaintenanceProgressUpdate, type ConnectionState } from '../realtime/useDeploymentHub';

export type MaintenanceProductState = 'loading' | 'confirm' | 'processing' | 'success' | 'error';
export type MaintenanceAction = 'enter' | 'exit';

/**
 * Per-stack progress status shown on the processing view:
 * - pending: not touched yet
 * - active: containers currently being stopped/started
 * - done: finished
 * - failed: container operation failed for this stack
 */
export type MaintenanceStackStatus = 'pending' | 'active' | 'done' | 'failed';

export interface UseMaintenanceProductStoreReturn {
  state: MaintenanceProductState;
  deployment: GetProductDeploymentResponse | null;
  error: string;
  action: MaintenanceAction;
  result: ChangeProductOperationModeResponse | null;
  totalServices: number;
  // Live progress (only populated while a SignalR session is active)
  stackStatuses: Record<string, MaintenanceStackStatus>;
  activeStackName: string | null;
  progress: MaintenanceProgressUpdate | null;
  connectionState: ConnectionState;
  handleConfirm: () => Promise<void>;
}

export function useMaintenanceProductStore(
  token: string | null,
  environmentId: string | undefined,
  productDeploymentId: string | undefined,
  action: MaintenanceAction,
): UseMaintenanceProductStoreReturn {
  const [state, setState] = useState<MaintenanceProductState>('loading');
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [error, setError] = useState('');
  const [result, setResult] = useState<ChangeProductOperationModeResponse | null>(null);

  const [stackStatuses, setStackStatuses] = useState<Record<string, MaintenanceStackStatus>>({});
  const [activeStackName, setActiveStackName] = useState<string | null>(null);
  const [progress, setProgress] = useState<MaintenanceProgressUpdate | null>(null);

  const sessionIdRef = useRef<string | null>(null);
  const activeStackRef = useRef<string | null>(null);
  const deploymentRef = useRef<GetProductDeploymentResponse | null>(null);

  const totalServices = deployment?.stacks.reduce((sum, s) => sum + s.serviceCount, 0) ?? 0;

  // Mark every stack as done (used on successful completion / non-SignalR fallback).
  const markAllDone = useCallback(() => {
    const dep = deploymentRef.current;
    if (!dep) return;
    setStackStatuses(prev => {
      const next: Record<string, MaintenanceStackStatus> = { ...prev };
      for (const stack of dep.stacks) {
        if (next[stack.stackName] !== 'failed') {
          next[stack.stackName] = 'done';
        }
      }
      return next;
    });
    setActiveStackName(null);
  }, []);

  const handleMaintenanceProgress = useCallback((update: MaintenanceProgressUpdate) => {
    if (!sessionIdRef.current || update.sessionId !== sessionIdRef.current) return;

    setProgress(update);

    if (update.isComplete) {
      if (update.isError) {
        const failed = activeStackRef.current;
        if (failed) {
          setStackStatuses(prev => ({ ...prev, [failed]: 'failed' }));
        }
        setActiveStackName(null);
        setError(update.message || `Failed to ${action} maintenance mode`);
        setState('error');
      } else {
        markAllDone();
        setState('success');
      }
      return;
    }

    // InProgress update for a specific stack: promote the previously active stack to done
    // and mark the new one active. Keyed by stack name (backend and UI agree on the key).
    const stackName = update.currentStackName;
    if (stackName) {
      const previous = activeStackRef.current;
      setStackStatuses(prev => {
        const next = { ...prev };
        if (previous && previous !== stackName && next[previous] !== 'failed') {
          next[previous] = 'done';
        }
        next[stackName] = 'active';
        return next;
      });
      activeStackRef.current = stackName;
      setActiveStackName(stackName);
    }
  }, [action, markAllDone]);

  const { subscribeToDeployment, connectionState } = useDeploymentHub(token, {
    onMaintenanceProgress: handleMaintenanceProgress,
  });

  useEffect(() => {
    if (!environmentId || !productDeploymentId) {
      setState('error');
      setError('No environment or product deployment ID provided');
      return;
    }

    const loadDeployment = async () => {
      try {
        setState('loading');
        setError('');

        const response = await getProductDeployment(environmentId, productDeploymentId);
        setDeployment(response);
        deploymentRef.current = response;

        if (action === 'enter' && !response.canEnterMaintenance) {
          setError(`Product "${response.productDisplayName}" cannot enter maintenance mode in its current state (${response.operationMode})`);
          setState('error');
          return;
        }

        if (action === 'exit' && !response.canExitMaintenance) {
          setError(`Product "${response.productDisplayName}" cannot exit maintenance mode in its current state (${response.operationMode})`);
          setState('error');
          return;
        }

        setState('confirm');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load product deployment');
        setState('error');
      }
    };

    loadDeployment();
  }, [environmentId, productDeploymentId, action]);

  const handleConfirm = useCallback(async () => {
    if (!environmentId || !deployment) {
      setError('No deployment available');
      return;
    }

    // Generate a session id so the server can stream progress back to this page.
    const sessionId = `maintenance-${action}-${deployment.productDeploymentId}-${Date.now()}`;
    sessionIdRef.current = sessionId;
    activeStackRef.current = null;

    // Initialise every stack to pending for the processing view.
    const initialStatuses: Record<string, MaintenanceStackStatus> = {};
    for (const stack of deployment.stacks) {
      initialStatuses[stack.stackName] = 'pending';
    }
    setStackStatuses(initialStatuses);
    setActiveStackName(null);
    setProgress(null);

    setState('processing');
    setError('');

    // Subscribe before triggering so we don't miss early progress events.
    if (connectionState === 'connected') {
      try {
        await subscribeToDeployment(sessionId);
      } catch {
        // If subscription fails we still fall back to the response below.
      }
    }

    try {
      const response = action === 'enter'
        ? await enterProductMaintenanceMode(environmentId, deployment.productDeploymentId, { sessionId })
        : await exitProductMaintenanceMode(environmentId, deployment.productDeploymentId, { sessionId });

      setResult(response);

      if (!response.success) {
        setError(response.message || `Failed to ${action} maintenance mode`);
        setState('error');
        return;
      }

      // The response only returns after the server finished the transition (and already
      // emitted its live progress + terminal event). Resolving success from it here is both
      // correct and robust — it guarantees we leave the processing view even if the terminal
      // SignalR event is dropped, while the intermediate events still animated progress live.
      markAllDone();
      setState('success');
    } catch (err) {
      setError(err instanceof Error ? err.message : `Failed to ${action} maintenance mode`);
      setState('error');
    }
  }, [environmentId, deployment, action, connectionState, subscribeToDeployment, markAllDone]);

  return {
    state,
    deployment,
    error,
    action,
    result,
    totalServices,
    stackStatuses,
    activeStackName,
    progress,
    connectionState,
    handleConfirm,
  };
}
