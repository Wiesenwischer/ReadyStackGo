import { useState, useRef, useCallback, useEffect } from 'react';
import {
  getProductDeployment,
  checkProductUpgrade,
  upgradeProduct,
  type GetProductDeploymentResponse,
  type CheckProductUpgradeResponse,
  type UpgradeProductStackResult,
} from '../api/deployments';
import { getProduct, type Product, type ProductStack, type StackVariable } from '../api/stacks';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { DeploymentProgressUpdate, InitContainerLogEntry, ConnectionState } from '../realtime/useDeploymentHub';

// Format phase names for display (PullingImages -> Pulling Images)
const formatPhase = (phase: string | undefined): string => {
  if (!phase) return '';
  return phase.replace(/([A-Z])/g, ' $1').trim();
};

// Compute shared variables (appear in 2+ stacks)
function computeSharedVariables(stacks: ProductStack[]): StackVariable[] {
  const varCount = new Map<string, { count: number; variable: StackVariable }>();
  for (const stack of stacks) {
    for (const v of stack.variables) {
      const existing = varCount.get(v.name);
      if (existing) {
        existing.count++;
      } else {
        varCount.set(v.name, { count: 1, variable: v });
      }
    }
  }
  return Array.from(varCount.values())
    .filter(e => e.count >= 2)
    .map(e => e.variable);
}

// Get stack-specific variables (not in shared set)
function getStackSpecificVariables(stack: ProductStack, sharedNames: Set<string>): StackVariable[] {
  return stack.variables.filter(v => !sharedNames.has(v.name));
}

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

export type UpgradeProductState = 'loading' | 'configure' | 'upgrading' | 'success' | 'error';

// Stack status for the upgrading view
export type StackProgressStatus = 'pending' | 'upgrading' | 'running' | 'failed';

export interface UseUpgradeProductStoreReturn {
  // State
  state: UpgradeProductState;
  error: string;
  productDeployment: GetProductDeploymentResponse | null;
  upgradeInfo: CheckProductUpgradeResponse | null;
  targetProduct: Product | null;
  selectedVersion: string | null;
  continueOnError: boolean;

  // Variable state
  sharedVariableValues: Record<string, string>;
  perStackVariableValues: Record<string, Record<string, string>>;
  sharedVars: StackVariable[];
  sharedVarNames: Set<string>;

  // Accordion state
  expandedStacks: Set<string>;

  // Progress state
  progressUpdate: DeploymentProgressUpdate | null;
  initContainerLogs: Record<string, string[]>;
  stackStatuses: Record<string, StackProgressStatus>;
  currentUpgradingStack: string | null;
  stackResults: UpgradeProductStackResult[];
  connectionState: ConnectionState;

  // Computed
  formattedPhase: string;

  // Actions
  setContinueOnError: (value: boolean) => void;
  setSharedVariableValue: (name: string, value: string) => void;
  setPerStackVariableValue: (stackId: string, name: string, value: string) => void;
  toggleStackExpanded: (stackId: string) => void;
  handleVersionChange: (newVersion: string) => Promise<void>;
  handleEnvFileContent: (content: string) => void;
  handleUpgrade: () => Promise<void>;

  // Computed helpers
  getBackUrl: () => string;
  isNewStack: (stackName: string) => boolean;
  getStackSpecificVariables: (stack: ProductStack) => StackVariable[];
}

export function useUpgradeProductStore(
  token: string | null,
  environmentId: string | undefined,
  productDeploymentId: string | undefined,
  targetVersionParam: string | null,
): UseUpgradeProductStoreReturn {
  const [state, setState] = useState<UpgradeProductState>('loading');
  const [error, setError] = useState('');
  const [continueOnError, setContinueOnError] = useState(true);

  // Data loaded in loading phase
  const [productDeployment, setProductDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [upgradeInfo, setUpgradeInfo] = useState<CheckProductUpgradeResponse | null>(null);
  const [targetProduct, setTargetProduct] = useState<Product | null>(null);
  const [selectedVersion, setSelectedVersion] = useState<string | null>(targetVersionParam);

  // Variable state: shared + per-stack
  const [sharedVariableValues, setSharedVariableValues] = useState<Record<string, string>>({});
  const [perStackVariableValues, setPerStackVariableValues] = useState<Record<string, Record<string, string>>>({});

  // Accordion: which stacks are expanded
  const [expandedStacks, setExpandedStacks] = useState<Set<string>>(new Set());

  // Computed shared variables
  const [sharedVars, setSharedVars] = useState<StackVariable[]>([]);
  const [sharedVarNames, setSharedVarNames] = useState<Set<string>>(new Set());

  // Upgrade progress state
  const upgradeSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);
  const [initContainerLogs, setInitContainerLogs] = useState<Record<string, string[]>>({});

  // Stack-level progress tracking for the upgrading view
  const [stackStatuses, setStackStatuses] = useState<Record<string, StackProgressStatus>>({});
  const [currentUpgradingStack, setCurrentUpgradingStack] = useState<string | null>(null);

  // Results after upgrade completes
  const [stackResults, setStackResults] = useState<UpgradeProductStackResult[]>([]);

  // SignalR hub
  const handleUpgradeProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = upgradeSessionIdRef.current;
    if (currentSessionId && update.sessionId === currentSessionId) {
      setProgressUpdate(update);

      // Parse product-level progress from the message
      if (update.phase === 'ProductDeploy' && update.currentService) {
        const stackName = update.currentService;
        setCurrentUpgradingStack(stackName);
        setStackStatuses(prev => ({
          ...prev,
          [stackName]: 'upgrading'
        }));
      }

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

  // Initialize variables from target product + current deployment values
  const initializeVariables = useCallback((
    product: Product,
    deployment: GetProductDeploymentResponse,
    newStacks: string[] | null | undefined,
  ) => {
    // Compute shared variables
    const shared = computeSharedVariables(product.stacks);
    setSharedVars(shared);
    const sharedNames = new Set(shared.map(v => v.name));
    setSharedVarNames(sharedNames);

    // Initialize shared variable values with target defaults
    const sharedInit: Record<string, string> = {};
    for (const v of shared) {
      sharedInit[v.name] = v.defaultValue || '';
    }

    // Overlay with current deployment shared variables
    if (deployment.sharedVariables) {
      for (const [key, value] of Object.entries(deployment.sharedVariables)) {
        if (Object.prototype.hasOwnProperty.call(sharedInit, key)) {
          sharedInit[key] = value;
        }
      }
    }

    // Initialize per-stack variable values
    const perStackInit: Record<string, Record<string, string>> = {};
    const expandInit = new Set<string>();

    for (const stack of product.stacks) {
      const stackVars = getStackSpecificVariables(stack, sharedNames);
      const varValues: Record<string, string> = {};
      let hasRequiredMissing = false;

      for (const v of stackVars) {
        varValues[v.name] = v.defaultValue || '';
        if (v.isRequired && !v.defaultValue) hasRequiredMissing = true;
      }

      // Find matching existing stack deployment and overlay its variables
      const _existingStack = deployment.stacks.find(
        s => s.stackName.toLowerCase() === stack.name.toLowerCase()
      );
      // existingStack is found for potential future use (overlay per-stack vars)
      void _existingStack;

      perStackInit[stack.id] = varValues;

      // Expand stacks that are new or have required variables missing values
      const isNew = newStacks?.some(n => n.toLowerCase() === stack.name.toLowerCase());
      if (isNew || hasRequiredMissing || stackVars.length > 0) {
        expandInit.add(stack.id);
      }
    }

    setSharedVariableValues(sharedInit);
    setPerStackVariableValues(perStackInit);
    setExpandedStacks(expandInit);
  }, []);

  // Load product deployment and upgrade info
  useEffect(() => {
    if (!productDeploymentId || !environmentId) {
      setError('Missing product deployment ID or environment');
      setState('error');
      return;
    }

    const loadData = async () => {
      try {
        setState('loading');
        setError('');

        // 1. Load product deployment
        const deployment = await getProductDeployment(environmentId, productDeploymentId);
        setProductDeployment(deployment);

        // 2. Check upgrade availability
        const upgradeCheck = await checkProductUpgrade(environmentId, productDeploymentId);
        setUpgradeInfo(upgradeCheck);

        if (!upgradeCheck.success) {
          setError(upgradeCheck.message || 'Failed to check upgrade availability');
          setState('error');
          return;
        }

        if (!upgradeCheck.upgradeAvailable) {
          setError('No upgrade available for this product deployment');
          setState('error');
          return;
        }

        if (!upgradeCheck.canUpgrade) {
          setError(upgradeCheck.cannotUpgradeReason || 'Cannot upgrade this product deployment');
          setState('error');
          return;
        }

        // 3. Determine target version
        const version = targetVersionParam || upgradeCheck.latestVersion;
        setSelectedVersion(version || null);

        // Get target product ID
        const targetProductId = version
          ? upgradeCheck.availableVersions?.find(v => v.version === version)?.productId
          : upgradeCheck.latestProductId;

        if (!targetProductId) {
          setError('Could not determine target product for upgrade');
          setState('error');
          return;
        }

        // 4. Load target product from catalog
        const product = await getProduct(targetProductId);
        setTargetProduct(product);

        // 5. Initialize variables
        initializeVariables(product, deployment, upgradeCheck.newStacks);

        setState('configure');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load upgrade data');
        setState('error');
      }
    };

    loadData();
  }, [productDeploymentId, environmentId, targetVersionParam, initializeVariables]);

  // Handle version change
  const handleVersionChange = useCallback(async (newVersion: string) => {
    if (!upgradeInfo || !productDeployment) return;

    setSelectedVersion(newVersion);

    const targetProductId = upgradeInfo.availableVersions?.find(v => v.version === newVersion)?.productId;
    if (!targetProductId) return;

    try {
      const product = await getProduct(targetProductId);
      setTargetProduct(product);
      initializeVariables(product, productDeployment, upgradeInfo.newStacks);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load target version');
    }
  }, [upgradeInfo, productDeployment, initializeVariables]);

  const toggleStackExpanded = useCallback((stackId: string) => {
    setExpandedStacks(prev => {
      const next = new Set(prev);
      if (next.has(stackId)) {
        next.delete(stackId);
      } else {
        next.add(stackId);
      }
      return next;
    });
  }, []);

  // Handle .env file content (parsed content, not the raw file event)
  const handleEnvFileContent = useCallback((content: string) => {
    if (!targetProduct) return;
    const envValues = parseEnvContent(content);

    // Update shared variable values
    setSharedVariableValues(prev => {
      const updated = { ...prev };
      for (const v of sharedVars) {
        if (envValues[v.name] !== undefined) {
          updated[v.name] = envValues[v.name];
        }
      }
      return updated;
    });

    // Update per-stack variable values
    setPerStackVariableValues(prev => {
      const updated = { ...prev };
      for (const stack of targetProduct.stacks) {
        const stackSpecific = getStackSpecificVariables(stack, sharedVarNames);
        const stackVars = { ...updated[stack.id] };
        for (const v of stackSpecific) {
          if (envValues[v.name] !== undefined) {
            stackVars[v.name] = envValues[v.name];
          }
        }
        updated[stack.id] = stackVars;
      }
      return updated;
    });
  }, [targetProduct, sharedVars, sharedVarNames]);

  const setSharedVariableValue = useCallback((name: string, value: string) => {
    setSharedVariableValues(prev => ({ ...prev, [name]: value }));
  }, []);

  const setPerStackVariableValue = useCallback((stackId: string, name: string, value: string) => {
    setPerStackVariableValues(prev => ({
      ...prev,
      [stackId]: {
        ...prev[stackId],
        [name]: value,
      },
    }));
  }, []);

  const handleUpgrade = useCallback(async () => {
    if (!targetProduct || !environmentId || !productDeployment || !upgradeInfo) {
      setError('Missing required data for upgrade');
      return;
    }

    // Get target product ID
    const targetProductId = selectedVersion
      ? upgradeInfo.availableVersions?.find(v => v.version === selectedVersion)?.productId
      : upgradeInfo.latestProductId;

    if (!targetProductId) {
      setError('Could not determine target product');
      return;
    }

    // Check required shared variables
    const missingShared = sharedVars
      .filter(v => v.isRequired && !sharedVariableValues[v.name])
      .map(v => v.label || v.name);
    if (missingShared.length > 0) {
      setError(`Missing required shared variables: ${missingShared.join(', ')}`);
      return;
    }

    // Check required per-stack variables
    for (const stack of targetProduct.stacks) {
      const stackSpecific = getStackSpecificVariables(stack, sharedVarNames);
      const missing = stackSpecific
        .filter(v => v.isRequired && !perStackVariableValues[stack.id]?.[v.name])
        .map(v => v.label || v.name);
      if (missing.length > 0) {
        setError(`Missing required variables in "${stack.name}": ${missing.join(', ')}`);
        return;
      }
    }

    // Generate session ID
    const sessionId = `upgrade-product-${productDeployment.productName}-${Date.now()}`;
    upgradeSessionIdRef.current = sessionId;

    // Initialize stack statuses
    const initialStatuses: Record<string, StackProgressStatus> = {};
    for (const stack of targetProduct.stacks) {
      initialStatuses[stack.name] = 'pending';
    }
    setStackStatuses(initialStatuses);

    setState('upgrading');
    setError('');
    setProgressUpdate(null);
    setInitContainerLogs({});
    setStackResults([]);
    setCurrentUpgradingStack(null);

    // Subscribe to SignalR
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    try {
      // Build stack configs
      const stackConfigs = targetProduct.stacks.map(stack => ({
        stackId: stack.id,
        variables: perStackVariableValues[stack.id] || {},
      }));

      const response = await upgradeProduct(environmentId, productDeploymentId!, {
        targetProductId,
        stackConfigs,
        sharedVariables: sharedVariableValues,
        sessionId,
        continueOnError,
      });

      setStackResults(response.stackResults || []);

      // Update stack statuses from results
      const finalStatuses: Record<string, StackProgressStatus> = {};
      for (const result of response.stackResults || []) {
        finalStatuses[result.stackDisplayName] = result.success ? 'running' : 'failed';
      }
      setStackStatuses(finalStatuses);

      if (!response.success && response.status === 'Failed') {
        setError(response.message || 'Product upgrade failed');
        setState('error');
        return;
      }

      // If SignalR is not connected, set state based on response
      if (connectionState !== 'connected') {
        if (response.success) {
          setState('success');
        } else {
          setError(response.message || 'Upgrade completed with errors');
          setState('error');
        }
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Upgrade failed');
      setState('error');
    }
  }, [
    targetProduct, environmentId, productDeployment, upgradeInfo,
    selectedVersion, sharedVars, sharedVariableValues, sharedVarNames,
    perStackVariableValues, continueOnError, connectionState,
    subscribeToDeployment, productDeploymentId,
  ]);

  const getBackUrl = useCallback(() => {
    if (productDeployment?.productId) {
      return `/catalog/${encodeURIComponent(productDeployment.productId)}`;
    }
    return '/catalog';
  }, [productDeployment]);

  const isNewStack = useCallback((stackName: string): boolean => {
    return upgradeInfo?.newStacks?.some(
      n => n.toLowerCase() === stackName.toLowerCase()
    ) ?? false;
  }, [upgradeInfo]);

  const getStackSpecificVars = useCallback((stack: ProductStack): StackVariable[] => {
    return getStackSpecificVariables(stack, sharedVarNames);
  }, [sharedVarNames]);

  return {
    state,
    error,
    productDeployment,
    upgradeInfo,
    targetProduct,
    selectedVersion,
    continueOnError,
    sharedVariableValues,
    perStackVariableValues,
    sharedVars,
    sharedVarNames,
    expandedStacks,
    progressUpdate,
    initContainerLogs,
    stackStatuses,
    currentUpgradingStack,
    stackResults,
    connectionState,
    formattedPhase: formatPhase(progressUpdate?.phase),
    setContinueOnError,
    setSharedVariableValue,
    setPerStackVariableValue,
    toggleStackExpanded,
    handleVersionChange,
    handleEnvFileContent,
    handleUpgrade,
    getBackUrl,
    isNewStack,
    getStackSpecificVariables: getStackSpecificVars,
  };
}
