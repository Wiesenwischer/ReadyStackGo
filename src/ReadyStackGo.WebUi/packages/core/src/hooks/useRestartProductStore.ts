import { useState, useEffect, useCallback } from 'react';
import {
  getProductDeployment,
  restartProductContainers,
  type GetProductDeploymentResponse,
  type StackRestartResult,
} from '../api/deployments';

export type RestartProductState = 'loading' | 'confirm' | 'restarting' | 'success' | 'error';

export interface UseRestartProductStoreReturn {
  state: RestartProductState;
  deployment: GetProductDeploymentResponse | null;
  error: string;
  restartResults: StackRestartResult[];
  totalServices: number;
  handleRestart: () => Promise<void>;
}

export function useRestartProductStore(
  _token: string | null,
  environmentId: string | undefined,
  productDeploymentId: string | undefined,
): UseRestartProductStoreReturn {
  const [state, setState] = useState<RestartProductState>('loading');
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [error, setError] = useState('');
  const [restartResults, setRestartResults] = useState<StackRestartResult[]>([]);

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

        if (!response.canRestart) {
          setError(`Product "${response.productDisplayName}" cannot be restarted in its current state (${response.status})`);
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

  const handleRestart = useCallback(async () => {
    if (!environmentId || !deployment) {
      setError('No deployment to restart');
      return;
    }

    setState('restarting');
    setError('');

    try {
      const response = await restartProductContainers(
        environmentId,
        deployment.productDeploymentId
      );
      setRestartResults(response.results);

      if (response.success) {
        setState('success');
      } else {
        setError(response.message || 'Restart completed with errors');
        setState('error');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to restart containers');
      setState('error');
    }
  }, [environmentId, deployment]);

  return {
    state,
    deployment,
    error,
    restartResults,
    totalServices,
    handleRestart,
  };
}
