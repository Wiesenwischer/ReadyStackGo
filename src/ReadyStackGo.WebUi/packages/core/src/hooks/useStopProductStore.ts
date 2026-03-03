import { useState, useEffect, useCallback } from 'react';
import {
  getProductDeployment,
  stopProductContainers,
  type GetProductDeploymentResponse,
  type StackContainerResult,
} from '../api/deployments';

export type StopProductState = 'loading' | 'confirm' | 'stopping' | 'success' | 'error';

export interface UseStopProductStoreReturn {
  state: StopProductState;
  deployment: GetProductDeploymentResponse | null;
  error: string;
  stopResults: StackContainerResult[];
  totalServices: number;
  handleStop: () => Promise<void>;
}

export function useStopProductStore(
  _token: string | null,
  environmentId: string | undefined,
  productDeploymentId: string | undefined,
): UseStopProductStoreReturn {
  const [state, setState] = useState<StopProductState>('loading');
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [error, setError] = useState('');
  const [stopResults, setStopResults] = useState<StackContainerResult[]>([]);

  const totalServices = deployment?.stacks.reduce((sum, s) => sum + s.serviceCount, 0) ?? 0;

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

        if (!response.canStop) {
          setError(`Product "${response.productDisplayName}" cannot be stopped in its current state (${response.status})`);
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
  }, [environmentId, productDeploymentId]);

  const handleStop = useCallback(async () => {
    if (!environmentId || !deployment) {
      setError('No deployment to stop');
      return;
    }

    setState('stopping');
    setError('');

    try {
      const response = await stopProductContainers(
        environmentId,
        deployment.productDeploymentId
      );
      setStopResults(response.results);

      if (response.success) {
        setState('success');
      } else {
        setError(response.message || 'Stop completed with errors');
        setState('error');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to stop containers');
      setState('error');
    }
  }, [environmentId, deployment]);

  return {
    state,
    deployment,
    error,
    stopResults,
    totalServices,
    handleStop,
  };
}
