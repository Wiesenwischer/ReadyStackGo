import { useState, useRef, useCallback, useEffect } from 'react';
import {
  getProductDeployment,
  redeployProduct,
  type GetProductDeploymentResponse,
  type DeployProductStackResult,
} from '../api/deployments';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { DeploymentProgressUpdate, InitContainerLogEntry, ConnectionState } from '../realtime/useDeploymentHub';

export type RedeployProductState = 'loading' | 'confirm' | 'redeploying' | 'success' | 'error';
export type StackRedeployStatus = 'pending' | 'removing' | 'deploying' | 'running' | 'failed';

export interface UseRedeployProductStoreReturn {
  state: RedeployProductState;
  deployment: GetProductDeploymentResponse | null;
  error: string;
  stackResults: DeployProductStackResult[];
  stackStatuses: Record<string, StackRedeployStatus>;
  progressUpdate: DeploymentProgressUpdate | null;
  perStackProgress: Record<string, DeploymentProgressUpdate | null>;
  perStackLogs: Record<string, Record<string, string[]>>;
  selectedStack: string | null;
  connectionState: ConnectionState;
  handleStackSelect: (stackName: string) => void;
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
  const [perStackProgress, setPerStackProgress] = useState<Record<string, DeploymentProgressUpdate | null>>({});
  const [perStackLogs, setPerStackLogs] = useState<Record<string, Record<string, string[]>>>({});
  const [selectedStack, setSelectedStack] = useState<string | null>(null);
  const currentDeployingStackRef = useRef<string | null>(null);
  const userSelectedStackRef = useRef(false);
  const completedRef = useRef(false);

  const handleRedeployProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = redeploySessionIdRef.current;
    if (!currentSessionId || update.sessionId !== currentSessionId) return;

    setProgressUpdate(update);

    if (update.phase === 'ProductDeploy') {
      if (update.currentService) {
        const stackName = update.currentService;
        if (update.message?.startsWith('Removing stack')) {
          setStackStatuses(prev => ({ ...prev, [stackName]: 'removing' }));
        } else if (update.message?.startsWith('Redeploying stack')) {
          currentDeployingStackRef.current = stackName;
          setStackStatuses(prev => ({ ...prev, [stackName]: 'deploying' }));
          if (!userSelectedStackRef.current) {
            setSelectedStack(stackName);
          }
        } else if (update.message?.includes('redeployed successfully')) {
          setStackStatuses(prev => ({ ...prev, [stackName]: 'running' }));
        } else if (update.message?.includes('redeploy failed')) {
          setStackStatuses(prev => ({ ...prev, [stackName]: 'failed' }));
        }
      }
    } else {
      // Route inner stack deployment events to the currently deploying stack
      const deployingStack = currentDeployingStackRef.current;
      if (deployingStack) {
        setPerStackProgress(prev => ({ ...prev, [deployingStack]: update }));
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

  const handleInitContainerLog = useCallback((log: InitContainerLogEntry) => {
    const currentSessionId = redeploySessionIdRef.current;
    if (currentSessionId && log.sessionId === currentSessionId) {
      const deployingStack = currentDeployingStackRef.current;
      if (deployingStack) {
        setPerStackLogs(prev => ({
          ...prev,
          [deployingStack]: {
            ...prev[deployingStack],
            [log.containerName]: [...(prev[deployingStack]?.[log.containerName] || []), log.logLine],
          },
        }));
      }
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub(token, {
    onDeploymentProgress: handleRedeployProgress,
    onInitContainerLog: handleInitContainerLog,
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

  const handleStackSelect = useCallback((stackName: string) => {
    setSelectedStack(stackName);
    userSelectedStackRef.current = true;
  }, []);

  const handleRedeploy = useCallback(async () => {
    if (!environmentId || !deployment) {
      setError('No deployment to redeploy');
      return;
    }

    const sessionId = `product-redeploy-${deployment.productName}-${Date.now()}`;
    redeploySessionIdRef.current = sessionId;
    completedRef.current = false;
    currentDeployingStackRef.current = null;
    userSelectedStackRef.current = false;

    setState('redeploying');
    setError('');
    setProgressUpdate(null);
    setPerStackProgress({});
    setPerStackLogs({});
    setSelectedStack(null);

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
    perStackProgress,
    perStackLogs,
    selectedStack,
    connectionState,
    handleStackSelect,
    handleRedeploy,
  };
}
