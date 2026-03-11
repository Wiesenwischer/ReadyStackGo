import { useState, useRef, useCallback, useEffect } from 'react';
import {
  getProductDeployment,
  redeployProduct,
  type GetProductDeploymentResponse,
  type DeployProductStackResult,
} from '../api/deployments';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { DeploymentProgressUpdate } from '../realtime/useDeploymentHub';

export type RedeployProductState = 'loading' | 'confirm' | 'redeploying' | 'success' | 'error';
export type StackRedeployStatus = 'pending' | 'removing' | 'deploying' | 'running' | 'failed';

export interface UseRedeployProductStoreReturn {
  state: RedeployProductState;
  deployment: GetProductDeploymentResponse | null;
  error: string;
  stackResults: DeployProductStackResult[];
  stackStatuses: Record<string, StackRedeployStatus>;
  progressUpdate: DeploymentProgressUpdate | null;
  connectionState: string;
  handleRedeploy: () => Promise<void>;
}

export function useRedeployProductStore(
  token: string | null,
  environmentId: string | undefined,
  productDeploymentId: string | undefined,
): UseRedeployProductStoreReturn {
  const [state, setState] = useState<RedeployProductState>('loading');
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [error, setError] = useState('');
  const [stackResults, setStackResults] = useState<DeployProductStackResult[]>([]);
  const [stackStatuses, setStackStatuses] = useState<Record<string, StackRedeployStatus>>({});
  const redeploySessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);
  const completedRef = useRef(false);

  const handleRedeployProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = redeploySessionIdRef.current;
    if (!currentSessionId || update.sessionId !== currentSessionId) return;

    // Only show product-level progress messages, not inner stack deployment details
    if (update.phase === 'ProductDeploy' || update.isComplete) {
      setProgressUpdate(update);
    }

    if (update.phase === 'ProductDeploy' && update.currentService) {
      const stackName = update.currentService;
      if (update.message?.startsWith('Removing stack')) {
        setStackStatuses(prev => ({ ...prev, [stackName]: 'removing' }));
      } else if (update.message?.startsWith('Redeploying stack')) {
        setStackStatuses(prev => ({ ...prev, [stackName]: 'deploying' }));
      } else if (update.message?.includes('redeployed successfully')) {
        setStackStatuses(prev => ({ ...prev, [stackName]: 'running' }));
      } else if (update.message?.includes('redeploy failed')) {
        setStackStatuses(prev => ({ ...prev, [stackName]: 'failed' }));
      }
    }

    if (update.isComplete && !completedRef.current) {
      completedRef.current = true;
      if (update.isError) {
        setError(update.errorMessage || 'Redeploy failed');
        setState('error');
      } else {
        setState('success');
      }
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub(token, {
    onDeploymentProgress: handleRedeployProgress,
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

        if (!response.canRedeploy) {
          setError(`Product "${response.productDisplayName}" cannot be redeployed in its current state (${response.status})`);
          setState('error');
          return;
        }

        const initialStatuses: Record<string, StackRedeployStatus> = {};
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

  const handleRedeploy = useCallback(async () => {
    if (!environmentId || !deployment) {
      setError('No deployment to redeploy');
      return;
    }

    const sessionId = `product-redeploy-${deployment.productName}-${Date.now()}`;
    redeploySessionIdRef.current = sessionId;
    completedRef.current = false;

    setState('redeploying');
    setError('');
    setProgressUpdate(null);

    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    redeployProduct(environmentId, deployment.productDeploymentId, { sessionId, continueOnError: true })
      .then(response => {
        setStackResults(response.stackResults || []);

        setTimeout(() => {
          if (!completedRef.current) {
            completedRef.current = true;

            const finalStatuses: Record<string, StackRedeployStatus> = {};
            for (const stack of deployment.stacks) {
              const result = response.stackResults.find(r => r.stackName === stack.stackName);
              finalStatuses[stack.stackName] = result?.success ? 'running' : 'failed';
            }
            setStackStatuses(finalStatuses);

            if (!response.success) {
              setError(response.message || 'Redeploy completed with errors');
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
            setError(err instanceof Error ? err.message : 'Redeploy failed');
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
    handleRedeploy,
  };
}
