import { useState, useRef, useCallback, useEffect } from 'react';
import {
  getProductDeployment,
  retryProduct,
  type GetProductDeploymentResponse,
  type DeployProductStackResult,
} from '../api/deployments';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { DeploymentProgressUpdate } from '../realtime/useDeploymentHub';

export type RetryProductState = 'loading' | 'confirm' | 'retrying' | 'success' | 'error';
export type StackRetryStatus = 'skipped' | 'pending' | 'deploying' | 'running' | 'failed';

export interface UseRetryProductStoreReturn {
  state: RetryProductState;
  deployment: GetProductDeploymentResponse | null;
  error: string;
  stackResults: DeployProductStackResult[];
  stackStatuses: Record<string, StackRetryStatus>;
  progressUpdate: DeploymentProgressUpdate | null;
  connectionState: string;
  failedStacks: GetProductDeploymentResponse['stacks'];
  runningStacks: GetProductDeploymentResponse['stacks'];
  handleRetry: () => Promise<void>;
}

export function useRetryProductStore(
  token: string | null,
  environmentId: string | undefined,
  productDeploymentId: string | undefined,
): UseRetryProductStoreReturn {
  const [state, setState] = useState<RetryProductState>('loading');
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [error, setError] = useState('');
  const [stackResults, setStackResults] = useState<DeployProductStackResult[]>([]);
  const [stackStatuses, setStackStatuses] = useState<Record<string, StackRetryStatus>>({});
  const retrySessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);
  const completedRef = useRef(false);

  const failedStacks = deployment?.stacks.filter(s => s.status === 'Failed' || s.status === 'Pending') ?? [];
  const runningStacks = deployment?.stacks.filter(s => s.status === 'Running') ?? [];

  const handleRetryProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = retrySessionIdRef.current;
    if (!currentSessionId || update.sessionId !== currentSessionId) return;

    setProgressUpdate(update);

    if (update.phase === 'ProductDeploy' && update.currentService) {
      const stackName = update.currentService;
      if (update.message?.startsWith('Retrying stack')) {
        setStackStatuses(prev => ({ ...prev, [stackName]: 'deploying' }));
      } else if (update.message?.includes('retried successfully')) {
        setStackStatuses(prev => ({ ...prev, [stackName]: 'running' }));
      } else if (update.message?.includes('retry failed')) {
        setStackStatuses(prev => ({ ...prev, [stackName]: 'failed' }));
      }
    }

    if (update.isComplete && !completedRef.current) {
      completedRef.current = true;
      if (update.isError) {
        setError(update.errorMessage || 'Retry failed');
        setState('error');
      } else {
        setState('success');
      }
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub(token, {
    onDeploymentProgress: handleRetryProgress,
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

        if (!response.canRetry) {
          setError(`Product "${response.productDisplayName}" cannot be retried in its current state (${response.status})`);
          setState('error');
          return;
        }

        const initialStatuses: Record<string, StackRetryStatus> = {};
        for (const stack of response.stacks) {
          initialStatuses[stack.stackName] = stack.status === 'Running' ? 'skipped' : 'pending';
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

  const handleRetry = useCallback(async () => {
    if (!environmentId || !deployment) {
      setError('No deployment to retry');
      return;
    }

    const sessionId = `product-retry-${deployment.productName}-${Date.now()}`;
    retrySessionIdRef.current = sessionId;
    completedRef.current = false;

    setState('retrying');
    setError('');
    setProgressUpdate(null);

    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    retryProduct(environmentId, deployment.productDeploymentId, { sessionId, continueOnError: true })
      .then(response => {
        setStackResults(response.stackResults || []);

        setTimeout(() => {
          if (!completedRef.current) {
            completedRef.current = true;

            const finalStatuses: Record<string, StackRetryStatus> = {};
            for (const stack of deployment.stacks) {
              if (stack.status === 'Running') {
                finalStatuses[stack.stackName] = 'skipped';
              } else {
                const result = response.stackResults.find(r => r.stackName === stack.stackName);
                finalStatuses[stack.stackName] = result?.success ? 'running' : 'failed';
              }
            }
            setStackStatuses(finalStatuses);

            if (!response.success) {
              setError(response.message || 'Retry completed with errors');
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
            setError(err instanceof Error ? err.message : 'Retry failed');
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
    connectionState,
    failedStacks,
    runningStacks,
    handleRetry,
  };
}
