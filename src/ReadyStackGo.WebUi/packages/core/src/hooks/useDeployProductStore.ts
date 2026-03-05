import { useState, useRef, useCallback, useEffect } from 'react';
import { deployProduct } from '../api/deployments';
import { getProduct } from '../api/stacks';
import { getEnvironmentVariables, saveEnvironmentVariables } from '../api/environments';
import { useDeploymentHub } from '../realtime/useDeploymentHub';
import type { DeployProductStackResult } from '../api/deployments';
import type { Product, ProductStack, StackVariable } from '../api/stacks';
import type { DeploymentProgressUpdate, InitContainerLogEntry, ConnectionState } from '../realtime/useDeploymentHub';

// Generate kebab-case deployment stack name
const toKebabCase = (name: string): string =>
  name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');

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

export type DeployProductState = 'loading' | 'configure' | 'deploying' | 'success' | 'error';

export type StackProgressStatus = 'pending' | 'deploying' | 'running' | 'failed';

export interface UseDeployProductStoreReturn {
  // State
  state: DeployProductState;
  product: Product | null;
  error: string;
  continueOnError: boolean;
  deploymentName: string;
  sharedVariableValues: Record<string, string>;
  perStackVariableValues: Record<string, Record<string, string>>;
  expandedStacks: Set<string>;
  sharedVars: StackVariable[];
  sharedVarNames: Set<string>;
  progressUpdate: DeploymentProgressUpdate | null;
  stackStatuses: Record<string, StackProgressStatus>;
  perStackProgress: Record<string, DeploymentProgressUpdate | null>;
  perStackLogs: Record<string, Record<string, string[]>>;
  selectedStack: string | null;
  stackResults: DeployProductStackResult[];
  connectionState: ConnectionState;

  // Actions
  setContinueOnError: (value: boolean) => void;
  setDeploymentName: (name: string) => void;
  setSharedVariableValue: (name: string, value: string) => void;
  setPerStackVariableValue: (stackId: string, name: string, value: string) => void;
  toggleStackExpanded: (stackId: string) => void;
  handleStackSelect: (stackName: string) => void;
  handleEnvFileContent: (content: string) => void;
  handleResetToDefaults: () => void;
  handleDeploy: () => Promise<void>;

  // Computed / helpers
  backUrl: string;
  toKebabCase: (name: string) => string;
  getStackSpecificVariables: (stack: ProductStack, sharedNames: Set<string>) => StackVariable[];
}

export function useDeployProductStore(
  token: string | null,
  environmentId: string | undefined,
  productSourceId: string | undefined,
): UseDeployProductStoreReturn {
  const [state, setState] = useState<DeployProductState>('loading');
  const [product, setProduct] = useState<Product | null>(null);
  const [error, setError] = useState('');
  const [continueOnError, setContinueOnError] = useState(true);

  // Variable state: shared + per-stack
  const [sharedVariableValues, setSharedVariableValues] = useState<Record<string, string>>({});
  const [perStackVariableValues, setPerStackVariableValues] = useState<Record<string, Record<string, string>>>({});
  const [deploymentName, setDeploymentName] = useState('');

  // Accordion: which stacks are expanded
  const [expandedStacks, setExpandedStacks] = useState<Set<string>>(new Set());

  // Computed shared variables
  const [sharedVars, setSharedVars] = useState<StackVariable[]>([]);
  const [sharedVarNames, setSharedVarNames] = useState<Set<string>>(new Set());

  // Deployment progress state
  const deploymentSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);

  // Stack-level progress tracking for the deploying view
  const [stackStatuses, setStackStatuses] = useState<Record<string, StackProgressStatus>>({});
  const [perStackProgress, setPerStackProgress] = useState<Record<string, DeploymentProgressUpdate | null>>({});
  const [perStackLogs, setPerStackLogs] = useState<Record<string, Record<string, string[]>>>({});
  const [selectedStack, setSelectedStack] = useState<string | null>(null);
  const currentDeployingStackRef = useRef<string | null>(null);
  const userSelectedStackRef = useRef(false);

  // Results after deployment completes
  const [stackResults, setStackResults] = useState<DeployProductStackResult[]>([]);

  // SignalR hub
  const handleDeploymentProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = deploymentSessionIdRef.current;
    if (currentSessionId && update.sessionId === currentSessionId) {
      setProgressUpdate(update);

      // Parse product-level progress from the message
      // Backend sends: "Deploying stack X/N: StackName" with phase "ProductDeploy"
      if (update.phase === 'ProductDeploy' && update.currentService) {
        const stackName = update.currentService;

        // Mark previous deploying stack as running
        const prevStack = currentDeployingStackRef.current;
        if (prevStack && prevStack !== stackName) {
          setStackStatuses(prev => ({
            ...prev,
            [prevStack]: 'running'
          }));
        }

        currentDeployingStackRef.current = stackName;
        setStackStatuses(prev => ({
          ...prev,
          [stackName]: 'deploying'
        }));

        // Auto-select the newly deploying stack (unless user manually selected)
        if (!userSelectedStackRef.current) {
          setSelectedStack(stackName);
        }
      } else {
        // Route stack-level progress updates to per-stack state
        const deployingStack = currentDeployingStackRef.current;
        if (deployingStack) {
          setPerStackProgress(prev => ({
            ...prev,
            [deployingStack]: update
          }));
        }
      }

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
      // Per-stack logs
      const deployingStack = currentDeployingStackRef.current;
      if (deployingStack) {
        setPerStackLogs(prev => ({
          ...prev,
          [deployingStack]: {
            ...prev[deployingStack],
            [log.containerName]: [...(prev[deployingStack]?.[log.containerName] || []), log.logLine]
          }
        }));
      }
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub(token, {
    onDeploymentProgress: handleDeploymentProgress,
    onInitContainerLog: handleInitContainerLog,
  });

  // Load product
  useEffect(() => {
    if (!productSourceId) {
      setError('No product ID provided');
      setState('error');
      return;
    }

    const loadProduct = async () => {
      try {
        setState('loading');
        setError('');

        const productData = await getProduct(productSourceId);
        setProduct(productData);

        // Compute shared variables
        const shared = computeSharedVariables(productData.stacks);
        setSharedVars(shared);
        const sharedNames = new Set(shared.map(v => v.name));
        setSharedVarNames(sharedNames);

        // Initialize shared variable values with defaults
        const sharedInit: Record<string, string> = {};
        for (const v of shared) {
          sharedInit[v.name] = v.defaultValue || '';
        }

        // Initialize per-stack variable values and deployment name
        const perStackInit: Record<string, Record<string, string>> = {};
        const expandInit = new Set<string>();

        for (const stack of productData.stacks) {
          const stackVars = getStackSpecificVariables(stack, sharedNames);
          const varValues: Record<string, string> = {};
          let hasRequired = false;
          for (const v of stackVars) {
            varValues[v.name] = v.defaultValue || '';
            if (v.isRequired && !v.defaultValue) hasRequired = true;
          }
          perStackInit[stack.id] = varValues;

          // Expand stacks with required variables by default
          if (hasRequired || stackVars.length > 0) {
            expandInit.add(stack.id);
          }
        }

        // Load saved environment variables and merge
        if (environmentId) {
          try {
            const savedVars = await getEnvironmentVariables(environmentId);
            // Merge into shared values
            for (const key of Object.keys(savedVars.variables)) {
              if (Object.prototype.hasOwnProperty.call(sharedInit, key)) {
                sharedInit[key] = savedVars.variables[key];
              }
            }
            // Merge into per-stack values
            for (const stackId of Object.keys(perStackInit)) {
              for (const key of Object.keys(savedVars.variables)) {
                if (Object.prototype.hasOwnProperty.call(perStackInit[stackId], key)) {
                  perStackInit[stackId][key] = savedVars.variables[key];
                }
              }
            }
          } catch {
            // If loading fails, just use defaults
          }
        }

        setSharedVariableValues(sharedInit);
        setPerStackVariableValues(perStackInit);
        setDeploymentName(toKebabCase(productData.name));
        setExpandedStacks(expandInit);

        setState('configure');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load product');
        setState('error');
      }
    };

    loadProduct();
  }, [productSourceId, environmentId]);

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

  // Handle .env file content (page handles FileReader, store handles parsing)
  const handleEnvFileContent = useCallback((content: string) => {
    if (!product) return;
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
      for (const stack of product.stacks) {
        const stackSpecific = getStackSpecificVariables(stack, sharedVarNames);
        const stackVarValues = { ...updated[stack.id] };
        for (const v of stackSpecific) {
          if (envValues[v.name] !== undefined) {
            stackVarValues[v.name] = envValues[v.name];
          }
        }
        updated[stack.id] = stackVarValues;
      }
      return updated;
    });
  }, [product, sharedVars, sharedVarNames]);

  // Handle reset to defaults
  const handleResetToDefaults = useCallback(() => {
    if (!product) return;

    const sharedInit: Record<string, string> = {};
    for (const v of sharedVars) {
      sharedInit[v.name] = v.defaultValue || '';
    }
    setSharedVariableValues(sharedInit);

    const perStackInit: Record<string, Record<string, string>> = {};
    for (const stack of product.stacks) {
      const stackVars = getStackSpecificVariables(stack, sharedVarNames);
      const varValues: Record<string, string> = {};
      for (const v of stackVars) {
        varValues[v.name] = v.defaultValue || '';
      }
      perStackInit[stack.id] = varValues;
    }
    setPerStackVariableValues(perStackInit);
  }, [product, sharedVars, sharedVarNames]);

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

  const handleDeploy = useCallback(async () => {
    if (!product || !environmentId) {
      setError('Product or environment not available');
      return;
    }

    // Validate deployment name
    if (!deploymentName.trim()) {
      setError('Please provide a deployment name');
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
    for (const stack of product.stacks) {
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
    const sessionId = `product-${product.name}-${Date.now()}`;
    deploymentSessionIdRef.current = sessionId;

    // Initialize stack statuses
    const initialStatuses: Record<string, StackProgressStatus> = {};
    for (const stack of product.stacks) {
      initialStatuses[stack.name] = 'pending';
    }
    setStackStatuses(initialStatuses);

    setState('deploying');
    setError('');
    setProgressUpdate(null);
    setStackResults([]);
    currentDeployingStackRef.current = null;
    setPerStackProgress({});
    setPerStackLogs({});
    setSelectedStack(null);
    userSelectedStackRef.current = false;

    // Subscribe to SignalR
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    try {
      const response = await deployProduct(environmentId, {
        productId: product.id,
        deploymentName,
        stackConfigs: product.stacks.map(stack => ({
          stackId: stack.id,
          variables: perStackVariableValues[stack.id] || {},
        })),
        sharedVariables: sharedVariableValues,
        sessionId,
        continueOnError,
      });

      setStackResults(response.stackResults || []);

      // Update stack statuses from results (keyed by stackName to match SignalR updates)
      const finalStatuses: Record<string, StackProgressStatus> = {};
      for (const result of response.stackResults || []) {
        finalStatuses[result.stackName] = result.success ? 'running' : 'failed';
      }
      setStackStatuses(finalStatuses);

      if (!response.success && response.status === 'Failed') {
        setError(response.message || 'Product deployment failed');
        setState('error');
        return;
      }

      // Save variable values for this environment
      try {
        const allVars = { ...sharedVariableValues };
        for (const vars of Object.values(perStackVariableValues)) {
          Object.assign(allVars, vars);
        }
        await saveEnvironmentVariables(environmentId, { variables: allVars });
      } catch {
        // Ignore save errors
      }

      // If SignalR is not connected, set state based on response
      if (connectionState !== 'connected') {
        if (response.success) {
          setState('success');
        } else {
          setError(response.message || 'Deployment completed with errors');
          setState('error');
        }
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Deployment failed');
      setState('error');
    }
  }, [product, environmentId, deploymentName, sharedVars, sharedVariableValues, sharedVarNames, perStackVariableValues, continueOnError, connectionState, subscribeToDeployment]);

  // Compute back URL
  const backUrl = product?.id
    ? `/catalog/${encodeURIComponent(product.id)}`
    : '/catalog';

  return {
    state,
    product,
    error,
    continueOnError,
    deploymentName,
    sharedVariableValues,
    perStackVariableValues,
    expandedStacks,
    sharedVars,
    sharedVarNames,
    progressUpdate,
    stackStatuses,
    perStackProgress,
    perStackLogs,
    selectedStack,
    stackResults,
    connectionState,
    setContinueOnError,
    setDeploymentName,
    setSharedVariableValue,
    setPerStackVariableValue,
    toggleStackExpanded,
    handleStackSelect,
    handleEnvFileContent,
    handleResetToDefaults,
    handleDeploy,
    backUrl,
    toKebabCase,
    getStackSpecificVariables,
  };
}
