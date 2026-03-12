import { useState, useRef, useCallback, useEffect } from 'react';
import {
  getProductDeployment,
  removeProductDeployment,
  type GetProductDeploymentResponse,
  type RemoveProductStackResult,
} from '../api/deployments';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { DeploymentProgressUpdate } from '../realtime/useDeploymentHub';

export type RemoveProductState = 'loading' | 'confirm' | 'removing' | 'success' | 'error';
export type StackRemoveStatus = 'pending' | 'removing' | 'removed' | 'failed';

export interface UseRemoveProductStoreReturn {
  state: RemoveProductState;
  deployment: GetProductDeploymentResponse | null;
  error: string;
  stackResults: RemoveProductStackResult[];
  stackStatuses: Record<string, StackRemoveStatus>;
  progressUpdate: DeploymentProgressUpdate | null;
  selectedStack: string | null;
  connectionState: string;
  totalServices: number;
  handleStackSelect: (stackName: string) => void;
  handleRemove: () => Promise<void>;
}

export function useRemoveProductStore(
  token: string | null,
  environmentId: string | undefined,
  productDeploymentId: string | undefined,
): UseRemoveProductStoreReturn {
  const [state, setState] = useState<RemoveProductState>('loading');
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [error, setError] = useState('');
  const [stackResults, setStackResults] = useState<RemoveProductStackResult[]>([]);
  const [stackStatuses, setStackStatuses] = useState<Record<string, StackRemoveStatus>>({});
  const removeSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);
  const [selectedStack, setSelectedStack] = useState<string | null>(null);
  const userSelectedStackRef = useRef(false);
  const completedRef = useRef(false);

  const totalServices = deployment?.stacks.reduce((sum, s) => sum + s.serviceCount, 0) ?? 0;

  const handleRemoveProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = removeSessionIdRef.current;
    if (!currentSessionId || update.sessionId !== currentSessionId) return;

    setProgressUpdate(update);

    if (update.phase === 'ProductRemoval' && update.currentService) {
      const stackName = update.currentService;
      if (update.message?.startsWith('Removing stack')) {
        setStackStatuses(prev => ({ ...prev, [stackName]: 'removing' }));
        if (!userSelectedStackRef.current) {
          setSelectedStack(stackName);
        }
      } else if (update.message?.startsWith('Stack removed:')) {
        setStackStatuses(prev => ({ ...prev, [stackName]: 'removed' }));
      } else if (update.message?.startsWith('Stack removal failed:')) {
        setStackStatuses(prev => ({ ...prev, [stackName]: 'failed' }));
      }
    }

    if (update.isComplete && !completedRef.current) {
      completedRef.current = true;
      if (update.isError) {
        setError(update.errorMessage || 'Removal failed');
        setState('error');
      } else {
        setState('success');
      }
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub(token, {
    onDeploymentProgress: handleRemoveProgress,
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

        if (!response.canRemove) {
          setError(`Product "${response.productDisplayName}" cannot be removed in its current state (${response.status})`);
          setState('error');
          return;
        }

        const initialStatuses: Record<string, StackRemoveStatus> = {};
        for (const stack of response.stacks) {
          initialStatuses[stack.stackName] = 'pending';
        }
        setStackStatuses(initialStatuses);

        setState('confirm');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load product deployment');
        setState('error');
      }
    };

    loadDeployment();
  }, [environmentId, productDeploymentId]);

  const handleStackSelect = useCallback((stackName: string) => {
    setSelectedStack(stackName);
    userSelectedStackRef.current = true;
  }, []);

  const handleRemove = useCallback(async () => {
    if (!environmentId || !deployment) {
      setError('No deployment to remove');
      return;
    }

    const sessionId = `product-remove-${deployment.productName}-${Date.now()}`;
    removeSessionIdRef.current = sessionId;
    completedRef.current = false;
    userSelectedStackRef.current = false;

    setState('removing');
    setError('');
    setProgressUpdate(null);
    setSelectedStack(null);

    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    removeProductDeployment(environmentId, deployment.productDeploymentId, { sessionId })
      .then(response => {
        setStackResults(response.stackResults || []);

        setTimeout(() => {
          if (!completedRef.current) {
            completedRef.current = true;

            const finalStatuses: Record<string, StackRemoveStatus> = {};
            for (const result of response.stackResults) {
              finalStatuses[result.stackName] = result.success ? 'removed' : 'failed';
            }
            setStackStatuses(finalStatuses);

            if (!response.success) {
              setError(response.message || 'Removal completed with errors');
              setState('error');
            } else {
              setState('success');
            }
          }
        }, 3000);
      })
      .catch(err => {
        setTimeout(() => {
          if (!completedRef.current) {
            completedRef.current = true;
            setError(err instanceof Error ? err.message : 'Removal failed');
            setState('error');
          }
        }, 3000);
      });
  }, [environmentId, deployment, connectionState, subscribeToDeployment]);

  return {
    state,
    deployment,
    error,
    stackResults,
    stackStatuses,
    progressUpdate,
    selectedStack,
    connectionState,
    totalServices,
    handleStackSelect,
    handleRemove,
  };
}
