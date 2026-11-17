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
import {
  buildUpgradeVariableState,
  getStackSpecificVariables,
  parseEnvContent,
  withoutUntouchedSecrets,
} from '../lib/upgradeVariables';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { DeploymentProgressUpdate, InitContainerLogEntry, ConnectionState } from '../realtime/useDeploymentHub';

// Format phase names for display (PullingImages -> Pulling Images)
const formatPhase = (phase: string | undefined): string => {
  if (!phase) return '';
  return phase.replace(/([A-Z])/g, ' $1').trim();
};

export type UpgradeProductState = 'loading' | 'configure' | 'upgrading' | 'success' | 'error';

// Stack status for the upgrading view. Upgrade runs per-stack as remove → deploy,
// so a stack passes through 'removing' before 'upgrading' (mirrors Redeploy).
export type StackProgressStatus = 'pending' | 'removing' | 'upgrading' | 'running' | 'failed';

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
  /**
   * Secret variables that already have a stored value. Their input stays empty (the server withholds
   * the value); leaving it empty keeps the stored value, typing a new one replaces it.
   */
  storedSecretNames: Set<string>;
  /** Variable names the user chose NOT to persist. */
  excludeFromStorage: Set<string>;
  setVariableSave: (varName: string, save: boolean) => void;

  // Accordion state
  expandedStacks: Set<string>;

  // Progress state
  progressUpdate: DeploymentProgressUpdate | null;
  perStackProgress: Record<string, DeploymentProgressUpdate | null>;
  perStackLogs: Record<string, Record<string, string[]>>;
  selectedStack: string | null;
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
  handleStackSelect: (stackName: string) => void;
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

  // Names of secret variables that already have a stored value. The server withholds the value, so
  // the form cannot pre-fill it — it must still count as "set" for required-variable validation, and
  // must not be submitted as an empty string (which would overwrite the stored value on merge).
  const [storedSecretNames, setStoredSecretNames] = useState<Set<string>>(new Set());
  const storedSecretNamesRef = useRef<Set<string>>(new Set());

  // Per-variable "save value" opt-out, carried forward so an upgrade does not quietly start storing
  // a password the user declined to store at deploy time.
  const [excludeFromStorage, setExcludeFromStorage] = useState<Set<string>>(new Set());
  const excludeFromStorageRef = useRef<Set<string>>(new Set());

  // Computed shared variables
  const [sharedVars, setSharedVars] = useState<StackVariable[]>([]);
  const [sharedVarNames, setSharedVarNames] = useState<Set<string>>(new Set());

  // Upgrade progress state
  const upgradeSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);
  const [perStackProgress, setPerStackProgress] = useState<Record<string, DeploymentProgressUpdate | null>>({});
  const [perStackLogs, setPerStackLogs] = useState<Record<string, Record<string, string[]>>>({});
  const [selectedStack, setSelectedStack] = useState<string | null>(null);
  const currentDeployingStackRef = useRef<string | null>(null);
  const userSelectedStackRef = useRef(false);
  const completedRef = useRef(false);

  // Stack-level progress tracking for the upgrading view
  const [stackStatuses, setStackStatuses] = useState<Record<string, StackProgressStatus>>({});
  const [currentUpgradingStack, setCurrentUpgradingStack] = useState<string | null>(null);

  // Results after upgrade completes
  const [stackResults, setStackResults] = useState<UpgradeProductStackResult[]>([]);

  // SignalR hub
  const handleUpgradeProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = upgradeSessionIdRef.current;
    if (!currentSessionId || update.sessionId !== currentSessionId) return;

    setProgressUpdate(update);

    if (update.phase === 'ProductDeploy') {
      // Product-level orchestration events. The backend runs each carried-over
      // stack as remove → deploy, so we parse the message to drive the per-stack
      // status (mirrors the Redeploy store).
      if (update.currentService) {
        const stackName = update.currentService;
        setCurrentUpgradingStack(stackName);
        if (update.message?.startsWith('Removing stack')) {
          // Mark this stack active so subsequent per-container removal events
          // (non-'ProductDeploy' phase) are routed to its detail panel — mirrors
          // the Redeploy store so upgrade shows "Removing web-1 (2/8)" live.
          currentDeployingStackRef.current = stackName;
          setStackStatuses(prev => ({ ...prev, [stackName]: 'removing' }));
          if (!userSelectedStackRef.current) {
            setSelectedStack(stackName);
          }
        } else if (update.message?.startsWith('Upgrading stack')) {
          currentDeployingStackRef.current = stackName;
          setStackStatuses(prev => ({ ...prev, [stackName]: 'upgrading' }));
          if (!userSelectedStackRef.current) {
            setSelectedStack(stackName);
          }
        } else if (update.message?.includes('upgraded successfully')) {
          setStackStatuses(prev => ({ ...prev, [stackName]: 'running' }));
        } else if (update.message?.includes('upgrade failed')) {
          setStackStatuses(prev => ({ ...prev, [stackName]: 'failed' }));
        }
      }
    } else {
      // Inner stack deployment progress (PullingImages, etc.) → route to the
      // currently deploying stack's detail panel.
      const deployingStack = currentDeployingStackRef.current;
      if (deployingStack) {
        setPerStackProgress(prev => ({ ...prev, [deployingStack]: update }));
      }
    }

    if (update.isComplete && !completedRef.current) {
      completedRef.current = true;
      if (update.isError) {
        setError(update.errorMessage || 'Upgrade failed');
        setState('error');
      } else {
        setState('success');
      }
    }
  }, []);

  const handleInitContainerLog = useCallback((log: InitContainerLogEntry) => {
    const currentSessionId = upgradeSessionIdRef.current;
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
    onDeploymentProgress: handleUpgradeProgress,
    onInitContainerLog: handleInitContainerLog,
  });

  // Initialize variables from target product + current deployment values
  const initializeVariables = useCallback((
    product: Product,
    deployment: GetProductDeploymentResponse,
    newStacks: string[] | null | undefined,
  ) => {
    const next = buildUpgradeVariableState(product, deployment, newStacks);

    setSharedVars(next.sharedVars);
    setSharedVarNames(next.sharedVarNames);
    setSharedVariableValues(next.sharedVariableValues);
    setPerStackVariableValues(next.perStackVariableValues);
    setExpandedStacks(next.expandedStacks);
    setStoredSecretNames(next.storedSecretNames);
    storedSecretNamesRef.current = next.storedSecretNames;
    setExcludeFromStorage(next.excludeFromStorage);
    excludeFromStorageRef.current = next.excludeFromStorage;
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

  const handleStackSelect = useCallback((stackName: string) => {
    setSelectedStack(stackName);
    userSelectedStackRef.current = true;
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
      .filter(v => v.isRequired && !sharedVariableValues[v.name] && !storedSecretNames.has(v.name))
      .map(v => v.label || v.name);
    if (missingShared.length > 0) {
      setError(`Missing required shared variables: ${missingShared.join(', ')}`);
      return;
    }

    // Check required per-stack variables
    for (const stack of targetProduct.stacks) {
      const stackSpecific = getStackSpecificVariables(stack, sharedVarNames);
      const missing = stackSpecific
        .filter(v => v.isRequired
          && !perStackVariableValues[stack.id]?.[v.name]
          && !storedSecretNames.has(v.name))
        .map(v => v.label || v.name);
      if (missing.length > 0) {
        setError(`Missing required variables in "${stack.name}": ${missing.join(', ')}`);
        return;
      }
    }

    // Generate session ID
    const sessionId = `upgrade-product-${productDeployment.productName}-${Date.now()}`;
    upgradeSessionIdRef.current = sessionId;
    completedRef.current = false;
    currentDeployingStackRef.current = null;
    userSelectedStackRef.current = false;

    // Initialize stack statuses
    const initialStatuses: Record<string, StackProgressStatus> = {};
    for (const stack of targetProduct.stacks) {
      initialStatuses[stack.name] = 'pending';
    }
    setStackStatuses(initialStatuses);

    setState('upgrading');
    setError('');
    setProgressUpdate(null);
    setPerStackProgress({});
    setPerStackLogs({});
    setSelectedStack(null);
    setStackResults([]);
    setCurrentUpgradingStack(null);

    // Subscribe to SignalR
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    try {
      // Build stack configs. Untouched stored secrets are omitted rather than sent as empty
      // strings: an empty override would win over the stored value when the backend merges.
      const stackConfigs = targetProduct.stacks.map(stack => ({
        stackId: stack.id,
        variables: withoutUntouchedSecrets(
          perStackVariableValues[stack.id] || {}, storedSecretNamesRef.current),
      }));

      const response = await upgradeProduct(environmentId, productDeploymentId!, {
        targetProductId,
        stackConfigs,
        sharedVariables: withoutUntouchedSecrets(
          sharedVariableValues, storedSecretNamesRef.current),
        sessionId,
        continueOnError,
        excludeFromStorage: excludeFromStorageRef.current.size > 0
          ? [...excludeFromStorageRef.current]
          : undefined,
      });

      setStackResults(response.stackResults || []);

      // Finalize from the API response. When SignalR is connected we give it a
      // short grace period to deliver the terminal isComplete event first;
      // completedRef guards against a double finalization.
      const finalize = () => {
        if (completedRef.current) return;
        completedRef.current = true;

        const finalStatuses: Record<string, StackProgressStatus> = {};
        for (const result of response.stackResults || []) {
          finalStatuses[result.stackName] = result.success ? 'running' : 'failed';
        }
        setStackStatuses(prev => ({ ...prev, ...finalStatuses }));

        if (!response.success) {
          setError(response.message || 'Upgrade completed with errors');
          setState('error');
        } else {
          setState('success');
        }
      };

      if (connectionState === 'connected') {
        setTimeout(finalize, 3000);
      } else {
        finalize();
      }
    } catch (err) {
      if (!completedRef.current) {
        completedRef.current = true;
        setError(err instanceof Error ? err.message : 'Upgrade failed');
        setState('error');
      }
    }
  }, [
    targetProduct, environmentId, productDeployment, upgradeInfo,
    selectedVersion, sharedVars, sharedVariableValues, sharedVarNames,
    perStackVariableValues, storedSecretNames, continueOnError, connectionState,
    subscribeToDeployment, productDeploymentId,
  ]);

  const setVariableSave = useCallback((varName: string, save: boolean) => {
    setExcludeFromStorage(prev => {
      const next = new Set(prev);
      if (save) {
        next.delete(varName);
      } else {
        next.add(varName);
      }
      excludeFromStorageRef.current = next;
      return next;
    });
  }, []);

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
    storedSecretNames,
    excludeFromStorage,
    setVariableSave,
    expandedStacks,
    progressUpdate,
    perStackProgress,
    perStackLogs,
    selectedStack,
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
    handleStackSelect,
    handleUpgrade,
    getBackUrl,
    isNewStack,
    getStackSpecificVariables: getStackSpecificVars,
  };
}
