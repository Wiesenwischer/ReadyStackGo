import { useState, useRef, useCallback, useEffect } from 'react';
import { deployCompose, deployStack } from '../api/deployments';
import { getStack, getProduct } from '../api/stacks';
import { getEnvironmentVariables, saveEnvironmentVariables } from '../api/environments';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { StackDetail, Product, ProductVersion } from '../api/stacks';
import type { DeploymentProgressUpdate, InitContainerLogEntry, ConnectionState } from '../realtime/useDeploymentHub';

// Parse .env file content and return key-value pairs
const parseEnvContent = (content: string): Record<string, string> => {
  const result: Record<string, string> = {};
  const lines = content.split('\n');
  for (const line of lines) {
    const trimmed = line.trim();
    // Skip empty lines and comments
    if (!trimmed || trimmed.startsWith('#')) continue;
    // Find first = sign
    const eqIndex = trimmed.indexOf('=');
    if (eqIndex === -1) continue;
    const key = trimmed.substring(0, eqIndex).trim();
    let value = trimmed.substring(eqIndex + 1).trim();
    // Remove surrounding quotes if present
    if ((value.startsWith('"') && value.endsWith('"')) ||
        (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    if (key) {
      result[key] = value;
    }
  }
  return result;
};

export type DeployState = 'loading' | 'configure' | 'deploying' | 'success' | 'error';

export interface UseDeployStackStoreReturn {
  // State
  state: DeployState;
  stack: StackDetail | null;
  product: Product | null;
  isCustomDeploy: boolean;
  stackName: string;
  yamlContent: string;
  variableValues: Record<string, string>;
  error: string;
  deployWarnings: string[];
  progressUpdate: DeploymentProgressUpdate | null;
  initContainerLogs: Record<string, string[]>;
  connectionState: ConnectionState;
  selectedStackId: string | null;

  // Actions
  setStackName: (name: string) => void;
  setYamlContent: (yaml: string) => void;
  setVariableValue: (name: string, value: string) => void;
  handleVersionChange: (version: ProductVersion) => Promise<void>;
  handleEnvFileContent: (content: string) => void;
  handleResetToDefaults: () => void;
  handleDeploy: () => Promise<void>;

  // Computed
  backUrl: string;
}

export interface DeployStackRestoreState {
  restoreVariables?: Record<string, string>;
  restoreStackName?: string;
}

export function useDeployStackStore(
  token: string | null,
  environmentId: string | undefined,
  stackIdParam: string | undefined,
  restoreState?: DeployStackRestoreState | null,
): UseDeployStackStoreReturn {
  const isCustomDeploy = stackIdParam === 'custom';

  const [state, setState] = useState<DeployState>(isCustomDeploy ? 'configure' : 'loading');
  const [stack, setStack] = useState<StackDetail | null>(null);
  const [product, setProduct] = useState<Product | null>(null);
  const [selectedStackId, setSelectedStackId] = useState<string | null>(stackIdParam || null);
  const [stackName, setStackName] = useState('');
  const [yamlContent, setYamlContent] = useState('');
  const [variableValues, setVariableValues] = useState<Record<string, string>>({});
  const [error, setError] = useState('');
  const [deployWarnings, setDeployWarnings] = useState<string[]>([]);

  // Deployment progress state
  const deploymentSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);
  const [initContainerLogs, setInitContainerLogs] = useState<Record<string, string[]>>({});

  // SignalR hub for real-time deployment progress
  const handleDeploymentProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = deploymentSessionIdRef.current;
    if (currentSessionId && update.sessionId === currentSessionId) {
      setProgressUpdate(update);

      if (update.isComplete) {
        if (update.isError) {
          setError(update.errorMessage || 'Deployment failed');
          setState('error');
        } else {
          setState('success');
        }
      }
    }
  }, []);

  const handleInitContainerLog = useCallback((log: InitContainerLogEntry) => {
    const currentSessionId = deploymentSessionIdRef.current;
    if (currentSessionId && log.sessionId === currentSessionId) {
      setInitContainerLogs(prev => ({
        ...prev,
        [log.containerName]: [...(prev[log.containerName] || []), log.logLine]
      }));
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub(token, {
    onDeploymentProgress: handleDeploymentProgress,
    onInitContainerLog: handleInitContainerLog,
  });

  // Load stack details (only if not custom)
  useEffect(() => {
    if (isCustomDeploy) {
      return;
    }

    if (!stackIdParam) {
      setError('No stack ID provided');
      setState('error');
      return;
    }

    const loadStack = async () => {
      try {
        setState('loading');
        setError('');

        const detail = await getStack(stackIdParam);
        setStack(detail);
        setStackName(detail.name);
        setSelectedStackId(stackIdParam);

        // Load product to get available versions
        if (detail.productId) {
          try {
            const productData = await getProduct(detail.productId);
            setProduct(productData);
          } catch {
            // Product load failure is not critical
          }
        }

        // Initialize variable values with defaults
        const initialValues: Record<string, string> = {};
        detail.variables.forEach(v => {
          initialValues[v.name] = v.defaultValue || '';
        });

        // Load saved environment variables and merge with defaults
        if (environmentId) {
          try {
            const savedVars = await getEnvironmentVariables(environmentId);
            Object.keys(savedVars.variables).forEach(key => {
              if (Object.prototype.hasOwnProperty.call(initialValues, key)) {
                initialValues[key] = savedVars.variables[key];
              }
            });
          } catch {
            // If loading fails, just use defaults
          }
        }

        // Restore variable values from precheck back-navigation if available
        if (restoreState?.restoreVariables) {
          const restored = { ...initialValues };
          for (const key of Object.keys(restoreState.restoreVariables)) {
            if (Object.prototype.hasOwnProperty.call(restored, key)) {
              restored[key] = restoreState.restoreVariables[key];
            }
          }
          setVariableValues(restored);
        } else {
          setVariableValues(initialValues);
        }

        if (restoreState?.restoreStackName) {
          setStackName(restoreState.restoreStackName);
        }

        setState('configure');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load stack');
        setState('error');
      }
    };

    loadStack();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stackIdParam, isCustomDeploy, environmentId]);

  // Handle version change
  const handleVersionChange = useCallback(async (version: ProductVersion) => {
    if (!product) return;

    try {
      const newStackId = version.defaultStackId;
      setSelectedStackId(newStackId);

      const detail = await getStack(newStackId);
      setStack(detail);
      setStackName(detail.name);

      // Re-initialize variables, keeping values where applicable
      const initialValues: Record<string, string> = {};
      detail.variables.forEach(v => {
        if (variableValues[v.name] !== undefined) {
          initialValues[v.name] = variableValues[v.name];
        } else {
          initialValues[v.name] = v.defaultValue || '';
        }
      });
      setVariableValues(initialValues);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load version');
    }
  }, [product, variableValues]);

  // Handle .env file content (page handles FileReader, store handles parsing)
  const handleEnvFileContent = useCallback((content: string) => {
    if (!stack) return;
    const envValues = parseEnvContent(content);
    setVariableValues(prev => {
      const updated = { ...prev };
      for (const v of stack.variables) {
        if (envValues[v.name] !== undefined) {
          updated[v.name] = envValues[v.name];
        }
      }
      return updated;
    });
  }, [stack]);

  // Handle reset to defaults
  const handleResetToDefaults = useCallback(() => {
    if (!stack) return;
    const defaultValues: Record<string, string> = {};
    stack.variables.forEach(v => {
      defaultValues[v.name] = v.defaultValue || '';
    });
    setVariableValues(defaultValues);
  }, [stack]);

  const setVariableValue = useCallback((name: string, value: string) => {
    setVariableValues(prev => ({ ...prev, [name]: value }));
  }, []);

  const handleDeploy = useCallback(async () => {
    if (!stackName.trim()) {
      setError('Please provide a stack name');
      return;
    }

    if (!environmentId) {
      setError('No environment selected');
      return;
    }

    if (isCustomDeploy && !yamlContent.trim()) {
      setError('Please provide docker-compose YAML content');
      return;
    }

    if (!isCustomDeploy && !stack) {
      setError('Stack not loaded');
      return;
    }

    // Check required variables (only for stack deploy)
    if (stack) {
      const missingRequired = stack.variables
        .filter(v => v.isRequired && !variableValues[v.name])
        .map(v => v.label || v.name);

      if (missingRequired.length > 0) {
        setError(`Missing required variables: ${missingRequired.join(', ')}`);
        return;
      }
    }

    // Generate session ID BEFORE the API call
    const sessionId = `${stackName}-${Date.now()}`;
    deploymentSessionIdRef.current = sessionId;

    setState('deploying');
    setError('');
    setProgressUpdate(null);
    setInitContainerLogs({});

    // Subscribe to SignalR group BEFORE starting the API call
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    try {
      let response;

      if (isCustomDeploy) {
        response = await deployCompose(environmentId, {
          stackName,
          yamlContent,
          variables: variableValues,
          sessionId,
        });
      } else {
        response = await deployStack(environmentId, selectedStackId!, {
          stackName,
          variables: variableValues,
          sessionId,
        });
      }

      if (!response.success) {
        setError(response.errors.join('\n') || response.message || 'Deployment failed');
        setState('error');
        return;
      }

      setDeployWarnings(response.warnings || []);

      // Save variable values for this environment (fire and forget)
      if (environmentId) {
        try {
          await saveEnvironmentVariables(environmentId, {
            variables: variableValues
          });
        } catch {
          // Ignore save errors
        }
      }

      // State will be set to 'success' by SignalR callback when deployment completes
      if (connectionState !== 'connected') {
        setState('success');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Deployment failed');
      setState('error');
    }
  }, [stackName, environmentId, isCustomDeploy, yamlContent, stack, variableValues, selectedStackId, connectionState, subscribeToDeployment]);

  // Compute back URL
  const backUrl = stack?.productId
    ? `/catalog/${encodeURIComponent(stack.productId)}`
    : '/catalog';

  return {
    state,
    stack,
    product,
    isCustomDeploy,
    stackName,
    yamlContent,
    variableValues,
    error,
    deployWarnings,
    progressUpdate,
    initContainerLogs,
    connectionState,
    selectedStackId,
    setStackName,
    setYamlContent,
    setVariableValue,
    handleVersionChange,
    handleEnvFileContent,
    handleResetToDefaults,
    handleDeploy,
    backUrl,
  };
}
