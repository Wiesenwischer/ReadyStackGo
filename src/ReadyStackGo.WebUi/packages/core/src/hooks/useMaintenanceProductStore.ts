import { useState, useEffect, useCallback } from 'react';
import {
  getProductDeployment,
  type GetProductDeploymentResponse,
} from '../api/deployments';
import {
  enterProductMaintenanceMode,
  exitProductMaintenanceMode,
  type ChangeProductOperationModeResponse,
} from '../api/health';

export type MaintenanceProductState = 'loading' | 'confirm' | 'processing' | 'success' | 'error';
export type MaintenanceAction = 'enter' | 'exit';

export interface UseMaintenanceProductStoreReturn {
  state: MaintenanceProductState;
  deployment: GetProductDeploymentResponse | null;
  error: string;
  action: MaintenanceAction;
  result: ChangeProductOperationModeResponse | null;
  totalServices: number;
  handleConfirm: () => Promise<void>;
}

export function useMaintenanceProductStore(
  _token: string | null,
  environmentId: string | undefined,
  productDeploymentId: string | undefined,
  action: MaintenanceAction,
): UseMaintenanceProductStoreReturn {
  const [state, setState] = useState<MaintenanceProductState>('loading');
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [error, setError] = useState('');
  const [result, setResult] = useState<ChangeProductOperationModeResponse | null>(null);

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

    setState('processing');
    setError('');

    try {
      const response = action === 'enter'
        ? await enterProductMaintenanceMode(environmentId, deployment.productDeploymentId)
        : await exitProductMaintenanceMode(environmentId, deployment.productDeploymentId);

      setResult(response);

      if (response.success) {
        setState('success');
      } else {
        setError(response.message || `Failed to ${action} maintenance mode`);
        setState('error');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : `Failed to ${action} maintenance mode`);
      setState('error');
    }
  }, [environmentId, deployment, action]);

  return {
    state,
    deployment,
    error,
    action,
    result,
    totalServices,
    handleConfirm,
  };
}
