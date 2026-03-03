import { useState, useRef, useCallback, useEffect } from 'react';
import { getDeployment, checkUpgrade, upgradeDeployment } from '../api/deployments';
import { getStack } from '../api/stacks';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { GetDeploymentResponse, CheckUpgradeResponse } from '../api/deployments';
import type { StackDetail } from '../api/stacks';
import type { DeploymentProgressUpdate, InitContainerLogEntry, ConnectionState } from '../realtime/useDeploymentHub';

// Parse .env file content and return key-value pairs
const parseEnvContent = (content: string): Record<string, string> => {
  const result: Record<string, string> = {};
  const lines = content.split('\n');
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const eqIndex = trimmed.indexOf('=');
    if (eqIndex === -1) continue;
    const key = trimmed.substring(0, eqIndex).trim();
    let value = trimmed.substring(eqIndex + 1).trim();
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

// Format phase names for display (PullingImages -> Pulling Images)
const formatPhase = (phase: string | undefined): string => {
  if (!phase) return '';
  return phase.replace(/([A-Z])/g, ' $1').trim();
};

export type UpgradeState = 'loading' | 'configure' | 'upgrading' | 'success' | 'error';

export interface UseUpgradeStackStoreReturn {
  // State
  state: UpgradeState;
  deployment: GetDeploymentResponse | null;
  upgradeInfo: CheckUpgradeResponse | null;
  targetStack: StackDetail | null;
  selectedVersion: string | null;
  variableValues: Record<string, string>;
  error: string;
  progressUpdate: DeploymentProgressUpdate | null;
  initContainerLogs: Record<string, string[]>;
  connectionState: ConnectionState;

  // Computed
  formattedPhase: string;

  // Actions
  setVariableValue: (name: string, value: string) => void;
  handleVersionChange: (newVersion: string) => Promise<void>;
  handleEnvFileContent: (content: string) => void;
  handleUpgrade: () => Promise<void>;
}

export function useUpgradeStackStore(
  token: string | null,
  environmentId: string | undefined,
  stackName: string | undefined,
  targetVersionParam: string | null,
): UseUpgradeStackStoreReturn {
  const [state, setState] = useState<UpgradeState>('loading');
  const [deployment, setDeployment] = useState<GetDeploymentResponse | null>(null);
  const [upgradeInfo, setUpgradeInfo] = useState<CheckUpgradeResponse | null>(null);
  const [targetStack, setTargetStack] = useState<StackDetail | null>(null);
  const [selectedVersion, setSelectedVersion] = useState<string | null>(targetVersionParam);
  const [variableValues, setVariableValues] = useState<Record<string, string>>({});
  const [error, setError] = useState('');

  // Upgrade progress state
  const upgradeSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);
  const [initContainerLogs, setInitContainerLogs] = useState<Record<string, string[]>>({});

  // SignalR hub for real-time upgrade progress
  const handleUpgradeProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = upgradeSessionIdRef.current;
    if (currentSessionId && update.sessionId === currentSessionId) {
      setProgressUpdate(update);

      if (update.isComplete) {
        if (update.isError) {
          setError(update.errorMessage || 'Upgrade failed');
          setState('error');
        } else {
          setState('success');
        }
      }
    }
  }, []);

  const handleInitContainerLog = useCallback((log: InitContainerLogEntry) => {
    const currentSessionId = upgradeSessionIdRef.current;
    if (currentSessionId && log.sessionId === currentSessionId) {
      setInitContainerLogs(prev => ({
        ...prev,
        [log.containerName]: [...(prev[log.containerName] || []), log.logLine]
      }));
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub(token, {
    onDeploymentProgress: handleUpgradeProgress,
    onInitContainerLog: handleInitContainerLog,
  });

  // Load deployment and upgrade info
  useEffect(() => {
    if (!stackName || !environmentId) {
      return;
    }

    const loadData = async () => {
      try {
        setState('loading');
        setError('');

        // Load deployment
        const deploymentResponse = await getDeployment(environmentId, decodeURIComponent(stackName));
        if (!deploymentResponse.success || !deploymentResponse.deploymentId) {
          setError(deploymentResponse.message || 'Deployment not found');
          setState('error');
          return;
        }
        setDeployment(deploymentResponse);

        // Load upgrade info
        const upgradeInfoResponse = await checkUpgrade(environmentId, deploymentResponse.deploymentId);
        setUpgradeInfo(upgradeInfoResponse);

        if (!upgradeInfoResponse.upgradeAvailable) {
          setError('No upgrade available for this deployment');
          setState('error');
          return;
        }

        if (!upgradeInfoResponse.canUpgrade) {
          setError(upgradeInfoResponse.cannotUpgradeReason || 'Cannot upgrade this deployment');
          setState('error');
          return;
        }

        // Determine target version
        const version = targetVersionParam || upgradeInfoResponse.latestVersion;
        setSelectedVersion(version || null);

        // Get the target stack ID
        const targetStackId = version
          ? upgradeInfoResponse.availableVersions?.find(v => v.version === version)?.stackId
          : upgradeInfoResponse.latestStackId;

        if (!targetStackId) {
          setError('Could not determine target stack for upgrade');
          setState('error');
          return;
        }

        // Load target stack definition
        const stackDetail = await getStack(targetStackId);
        setTargetStack(stackDetail);

        // Initialize variables: current deployment values > stack defaults
        const initialVariables: Record<string, string> = {};

        // First, apply stack defaults
        for (const variable of stackDetail.variables) {
          if (variable.defaultValue !== undefined) {
            initialVariables[variable.name] = variable.defaultValue;
          }
        }

        // Then overlay with current deployment values
        if (deploymentResponse.configuration) {
          for (const [key, value] of Object.entries(deploymentResponse.configuration)) {
            initialVariables[key] = value;
          }
        }

        setVariableValues(initialVariables);
        setState('configure');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load upgrade data');
        setState('error');
      }
    };

    loadData();
  }, [stackName, environmentId, targetVersionParam]);

  // Handle version change
  const handleVersionChange = useCallback(async (newVersion: string) => {
    if (!upgradeInfo || !deployment?.configuration) return;

    setSelectedVersion(newVersion);

    const targetStackId = upgradeInfo.availableVersions?.find(v => v.version === newVersion)?.stackId;
    if (!targetStackId) return;

    try {
      const stackDetail = await getStack(targetStackId);
      setTargetStack(stackDetail);

      // Re-initialize variables with new stack defaults, keeping current values
      const initialVariables: Record<string, string> = {};

      for (const variable of stackDetail.variables) {
        if (variable.defaultValue !== undefined) {
          initialVariables[variable.name] = variable.defaultValue;
        }
      }

      // Overlay with current values
      for (const [key, value] of Object.entries(variableValues)) {
        if (stackDetail.variables.some(v => v.name === key)) {
          initialVariables[key] = value;
        }
      }

      setVariableValues(initialVariables);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load stack configuration');
    }
  }, [upgradeInfo, deployment, variableValues]);

  // Handle .env file content
  const handleEnvFileContent = useCallback((content: string) => {
    if (!targetStack) return;
    const envValues = parseEnvContent(content);
    setVariableValues(prev => {
      const updated = { ...prev };
      for (const v of targetStack.variables) {
        if (envValues[v.name] !== undefined) {
          updated[v.name] = envValues[v.name];
        }
      }
      return updated;
    });
  }, [targetStack]);

  const setVariableValue = useCallback((name: string, value: string) => {
    setVariableValues(prev => ({ ...prev, [name]: value }));
  }, []);

  const handleUpgrade = useCallback(async () => {
    if (!environmentId || !deployment?.deploymentId || !upgradeInfo) {
      setError('Missing required data');
      return;
    }

    // Get the target stack ID
    const targetStackId = selectedVersion
      ? upgradeInfo.availableVersions?.find(v => v.version === selectedVersion)?.stackId
      : upgradeInfo.latestStackId;

    if (!targetStackId) {
      setError('Could not determine target stack');
      return;
    }

    // Check required variables
    if (targetStack) {
      const missingRequired = targetStack.variables
        .filter(v => v.isRequired && !variableValues[v.name])
        .map(v => v.label || v.name);

      if (missingRequired.length > 0) {
        setError(`Missing required variables: ${missingRequired.join(', ')}`);
        return;
      }
    }

    // Generate session ID before API call
    const sessionId = `upgrade-${deployment.stackName}-${Date.now()}`;
    upgradeSessionIdRef.current = sessionId;

    setState('upgrading');
    setError('');
    setProgressUpdate(null);
    setInitContainerLogs({});

    // Subscribe to SignalR before starting
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    try {
      const response = await upgradeDeployment(environmentId, deployment.deploymentId, {
        stackId: targetStackId,
        variables: variableValues,
        sessionId,
      });

      if (!response.success) {
        setError(response.message || 'Upgrade failed');
        setState('error');
        return;
      }

      // State will be set to 'success' by SignalR callback
      if (connectionState !== 'connected') {
        setState('success');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Upgrade failed');
      setState('error');
    }
  }, [environmentId, deployment, upgradeInfo, selectedVersion, targetStack, variableValues, connectionState, subscribeToDeployment]);

  return {
    state,
    deployment,
    upgradeInfo,
    targetStack,
    selectedVersion,
    variableValues,
    error,
    progressUpdate,
    initContainerLogs,
    connectionState,
    formattedPhase: formatPhase(progressUpdate?.phase),
    setVariableValue,
    handleVersionChange,
    handleEnvFileContent,
    handleUpgrade,
  };
}
