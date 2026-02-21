import { useEffect, useState, useRef, useCallback } from 'react';
import { useParams, Link } from 'react-router';
import { deployProduct, type DeployProductStackResult } from '../../api/deployments';
import { useEnvironment } from '../../context/EnvironmentContext';
import { type Product, type ProductStack, type StackVariable, getProduct } from '../../api/stacks';
import VariableInput, { groupVariables } from '../../components/variables/VariableInput';
import { useDeploymentHub, type DeploymentProgressUpdate, type InitContainerLogEntry } from '../../hooks/useDeploymentHub';
import { getEnvironmentVariables, saveEnvironmentVariables } from '../../api/environments';
import { DeploymentProgressPanel } from '../../components/deployments/DeploymentProgressPanel';

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

type DeployState = 'loading' | 'configure' | 'deploying' | 'success' | 'error';

// Stack status for the deploying view
type StackProgressStatus = 'pending' | 'deploying' | 'running' | 'failed';

export default function DeployProduct() {
  const { productId } = useParams<{ productId: string }>();
  const { activeEnvironment } = useEnvironment();

  const [state, setState] = useState<DeployState>('loading');
  const [product, setProduct] = useState<Product | null>(null);
  const [error, setError] = useState('');
  const [continueOnError, setContinueOnError] = useState(true);

  // Variable state: shared + per-stack
  const [sharedVariableValues, setSharedVariableValues] = useState<Record<string, string>>({});
  const [perStackVariableValues, setPerStackVariableValues] = useState<Record<string, Record<string, string>>>({});
  const [stackNames, setStackNames] = useState<Record<string, string>>({});

  // Accordion: which stacks are expanded
  const [expandedStacks, setExpandedStacks] = useState<Set<string>>(new Set());

  // Computed shared variables
  const [sharedVars, setSharedVars] = useState<StackVariable[]>([]);
  const [sharedVarNames, setSharedVarNames] = useState<Set<string>>(new Set());

  // Deployment progress state
  const deploymentSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);
  const envFileInputRef = useRef<HTMLInputElement>(null);

  // Stack-level progress tracking for the deploying view
  const [stackStatuses, setStackStatuses] = useState<Record<string, StackProgressStatus>>({});
  // Per-stack progress and logs for split-view
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

  const { subscribeToDeployment, connectionState } = useDeploymentHub({
    onDeploymentProgress: handleDeploymentProgress,
    onInitContainerLog: handleInitContainerLog,
  });

  // Load product
  useEffect(() => {
    if (!productId) {
      setError('No product ID provided');
      setState('error');
      return;
    }

    const loadProduct = async () => {
      try {
        setState('loading');
        setError('');

        const productData = await getProduct(productId);
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

        // Initialize per-stack variable values and stack names
        const perStackInit: Record<string, Record<string, string>> = {};
        const namesInit: Record<string, string> = {};
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
          namesInit[stack.id] = toKebabCase(`${productData.name}-${stack.name}`);

          // Expand stacks with required variables by default
          if (hasRequired || stackVars.length > 0) {
            expandInit.add(stack.id);
          }
        }

        // Load saved environment variables and merge
        if (activeEnvironment) {
          try {
            const savedVars = await getEnvironmentVariables(activeEnvironment.id);
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
        setStackNames(namesInit);
        setExpandedStacks(expandInit);

        setState('configure');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load product');
        setState('error');
      }
    };

    loadProduct();
  }, [productId]);

  const toggleStackExpanded = (stackId: string) => {
    setExpandedStacks(prev => {
      const next = new Set(prev);
      if (next.has(stackId)) {
        next.delete(stackId);
      } else {
        next.add(stackId);
      }
      return next;
    });
  };

  // Handle .env file import
  const handleEnvFileImport = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file && product) {
      const reader = new FileReader();
      reader.onload = (event) => {
        const content = event.target?.result as string;
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
      };
      reader.readAsText(file);
    }
    if (envFileInputRef.current) {
      envFileInputRef.current.value = '';
    }
  };

  // Handle reset to defaults
  const handleResetToDefaults = () => {
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
  };

  const handleDeploy = async () => {
    if (!product || !activeEnvironment) {
      setError('Product or environment not available');
      return;
    }

    // Validate stack names
    for (const stack of product.stacks) {
      const name = stackNames[stack.id];
      if (!name?.trim()) {
        setError(`Please provide a stack name for "${stack.name}"`);
        return;
      }
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
      const response = await deployProduct(activeEnvironment.id, {
        productId: product.id,
        stackConfigs: product.stacks.map(stack => ({
          stackId: stack.id,
          deploymentStackName: stackNames[stack.id],
          variables: perStackVariableValues[stack.id] || {},
        })),
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
        await saveEnvironmentVariables(activeEnvironment.id, { variables: allVars });
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
  };

  const getBackUrl = () => {
    if (product?.id) {
      return `/catalog/${encodeURIComponent(product.id)}`;
    }
    return '/catalog';
  };

  // ─── Loading State ────────────────────────────────────────────────────────

  if (state === 'loading') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-12">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-600 mx-auto mb-4"></div>
            <p className="text-gray-600 dark:text-gray-400">Loading product...</p>
          </div>
        </div>
      </div>
    );
  }

  // ─── Error State (no product loaded) ──────────────────────────────────────

  if (state === 'error' && !product) {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to="/catalog"
            className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back to Catalog
          </Link>
        </div>
        <div className="rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
        </div>
      </div>
    );
  }

  // ─── Success State ────────────────────────────────────────────────────────

  if (state === 'success') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="flex items-center justify-center w-16 h-16 mb-6 bg-green-100 rounded-full dark:bg-green-900/30">
              <svg className="w-8 h-8 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Product Deployed Successfully!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              {product?.name} v{product?.version} has been deployed to {activeEnvironment?.name}
            </p>

            {/* Stack Results */}
            {stackResults.length > 0 && (
              <div className="w-full max-w-lg mb-6">
                <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
                  Stack Results
                </h3>
                <div className="space-y-2">
                  {stackResults.map((result) => (
                    <div
                      key={result.stackName}
                      className="flex items-center justify-between p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50"
                    >
                      <div className="flex items-center gap-2">
                        <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                        </svg>
                        <span className="text-sm font-medium text-gray-900 dark:text-white">
                          {result.stackDisplayName}
                        </span>
                      </div>
                      <span className="text-xs text-gray-500 dark:text-gray-400">
                        {result.serviceCount} service{result.serviceCount !== 1 ? 's' : ''}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div className="flex gap-4">
              <Link
                to="/deployments"
                className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
              >
                View Deployments
              </Link>
              <Link
                to={getBackUrl()}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Back to Product
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ─── Error State (after deployment) ───────────────────────────────────────

  if (state === 'error' && product) {
    const successCount = stackResults.filter(r => r.success).length;
    const failedCount = stackResults.filter(r => !r.success).length;

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="flex items-center justify-center w-16 h-16 mb-6 bg-red-100 rounded-full dark:bg-red-900/30">
              <svg className="w-8 h-8 text-red-600 dark:text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Deployment {failedCount > 0 && successCount > 0 ? 'Partially' : ''} Failed
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-2">
              {product.name} v{product.version}
            </p>
            <p className="text-sm text-red-600 dark:text-red-400 mb-6 max-w-md text-center">
              {error}
            </p>

            {/* Stack Results */}
            {stackResults.length > 0 && (
              <div className="w-full max-w-lg mb-6">
                <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
                  Stack Results ({successCount} succeeded, {failedCount} failed)
                </h3>
                <div className="space-y-2">
                  {stackResults.map((result) => (
                    <div
                      key={result.stackName}
                      className={`flex items-center justify-between p-3 rounded-lg ${
                        result.success
                          ? 'bg-green-50 dark:bg-green-900/10'
                          : 'bg-red-50 dark:bg-red-900/10'
                      }`}
                    >
                      <div className="flex items-center gap-2">
                        {result.success ? (
                          <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                          </svg>
                        ) : (
                          <svg className="w-4 h-4 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                          </svg>
                        )}
                        <span className="text-sm font-medium text-gray-900 dark:text-white">
                          {result.stackDisplayName}
                        </span>
                      </div>
                      {result.errorMessage && (
                        <span className="text-xs text-red-600 dark:text-red-400 max-w-xs truncate">
                          {result.errorMessage}
                        </span>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div className="flex gap-4">
              <Link
                to="/deployments"
                className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
              >
                View Deployments
              </Link>
              <Link
                to={getBackUrl()}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Back to Product
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ─── Deploying State (Split-View) ────────────────────────────────────────

  if (state === 'deploying') {
    const totalStacks = product?.stacks.length || 0;
    const completedCount = Object.values(stackStatuses).filter(s => s === 'running').length;
    const failedCount = Object.values(stackStatuses).filter(s => s === 'failed').length;
    const overallPercent = totalStacks > 0
      ? Math.round(((completedCount + failedCount) / totalStacks) * 100)
      : progressUpdate?.percentComplete ?? 0;

    const handleStackSelect = (stackName: string) => {
      setSelectedStack(stackName);
      userSelectedStackRef.current = true;
    };

    const selectedStatus = selectedStack ? (stackStatuses[selectedStack] || 'pending') : null;
    const selectedProgress = selectedStack ? (perStackProgress[selectedStack] || null) : null;
    const selectedLogs = selectedStack ? (perStackLogs[selectedStack] || {}) : {};

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
          {/* Header */}
          <div className="flex items-center gap-4 mb-4">
            <div className="w-10 h-10 border-4 border-brand-600 border-t-transparent rounded-full animate-spin flex-shrink-0"></div>
            <div>
              <h1 className="text-xl font-bold text-gray-900 dark:text-white">
                Deploying Product...
              </h1>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                {product?.name} v{product?.version} — {completedCount}/{totalStacks} stacks completed
              </p>
            </div>
          </div>

          {/* Overall Progress Bar (thin) */}
          <div className="mb-6">
            <div className="h-2 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
              <div
                className="h-full bg-brand-600 rounded-full transition-all duration-500 ease-out"
                style={{ width: `${overallPercent}%` }}
              />
            </div>
          </div>

          {/* Split View: Stack List (left) + Detail Panel (right) */}
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* LEFT: Stack Overview List */}
            <div className="lg:col-span-1">
              <h3 className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
                Stacks
              </h3>
              <div className="space-y-1">
                {product?.stacks.map((stack) => {
                  const status = stackStatuses[stack.name] || 'pending';
                  const isSelected = selectedStack === stack.name;
                  return (
                    <button
                      key={stack.id}
                      onClick={() => handleStackSelect(stack.name)}
                      className={`w-full flex items-center justify-between p-3 rounded-lg text-left transition-colors ${
                        isSelected
                          ? 'bg-brand-50 border-l-4 border-l-brand-600 dark:bg-brand-900/20'
                          : 'bg-gray-50 hover:bg-gray-100 dark:bg-gray-800/50 dark:hover:bg-gray-800'
                      }`}
                    >
                      <div className="flex items-center gap-2">
                        {status === 'pending' && (
                          <span className="w-4 h-4 rounded-full border-2 border-gray-300 dark:border-gray-600 flex-shrink-0" />
                        )}
                        {status === 'deploying' && (
                          <div className="w-4 h-4 border-2 border-brand-600 border-t-transparent rounded-full animate-spin flex-shrink-0" />
                        )}
                        {status === 'running' && (
                          <svg className="w-4 h-4 text-green-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                          </svg>
                        )}
                        {status === 'failed' && (
                          <svg className="w-4 h-4 text-red-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                          </svg>
                        )}
                        <span className={`text-sm ${
                          status === 'deploying' ? 'font-medium text-brand-600 dark:text-brand-400' :
                          status === 'running' ? 'text-green-700 dark:text-green-400' :
                          status === 'failed' ? 'text-red-700 dark:text-red-400' :
                          'text-gray-600 dark:text-gray-400'
                        }`}>
                          {stack.name}
                        </span>
                      </div>
                      <span className={`text-xs px-2 py-0.5 rounded-full flex-shrink-0 ${
                        status === 'pending' ? 'bg-gray-100 text-gray-500 dark:bg-gray-700 dark:text-gray-400' :
                        status === 'deploying' ? 'bg-brand-100 text-brand-700 dark:bg-brand-900/30 dark:text-brand-300' :
                        status === 'running' ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300' :
                        'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300'
                      }`}>
                        {status === 'pending' ? 'Pending' :
                         status === 'deploying' ? 'Deploying' :
                         status === 'running' ? 'Running' : 'Failed'}
                      </span>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* RIGHT: Detail Panel */}
            <div className="lg:col-span-2">
              {!selectedStack && (
                <div className="flex flex-col items-center justify-center h-full py-12 text-gray-400 dark:text-gray-500">
                  <svg className="w-12 h-12 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                  </svg>
                  <p className="text-sm">Waiting for deployment to start...</p>
                </div>
              )}

              {selectedStack && selectedStatus === 'pending' && (
                <div className="flex flex-col items-center justify-center h-full py-12 text-gray-400 dark:text-gray-500">
                  <span className="w-12 h-12 rounded-full border-2 border-gray-300 dark:border-gray-600 mb-3" />
                  <p className="text-sm font-medium text-gray-600 dark:text-gray-400">{selectedStack}</p>
                  <p className="text-sm">Waiting to deploy...</p>
                </div>
              )}

              {selectedStack && selectedStatus === 'deploying' && (
                <div>
                  <h3 className="text-sm font-medium text-gray-900 dark:text-white mb-4">
                    {selectedStack}
                    <span className="ml-2 text-xs font-normal text-brand-600 dark:text-brand-400">Deploying</span>
                  </h3>
                  <DeploymentProgressPanel
                    progressUpdate={selectedProgress}
                    initContainerLogs={selectedLogs}
                    connectionState={connectionState}
                    defaultMessage={`Deploying ${selectedStack}...`}
                  />
                </div>
              )}

              {selectedStack && selectedStatus === 'running' && (
                <div>
                  <h3 className="text-sm font-medium text-gray-900 dark:text-white mb-4">
                    {selectedStack}
                    <span className="ml-2 text-xs font-normal text-green-600 dark:text-green-400">Deployed</span>
                  </h3>
                  <div className="flex items-center gap-3 mb-4 p-4 rounded-lg bg-green-50 dark:bg-green-900/10">
                    <svg className="w-6 h-6 text-green-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                    </svg>
                    <p className="text-sm text-green-700 dark:text-green-300">
                      Stack deployed successfully
                    </p>
                  </div>
                  {/* Show last known progress snapshot */}
                  {selectedProgress && (
                    <DeploymentProgressPanel
                      progressUpdate={selectedProgress}
                      initContainerLogs={selectedLogs}
                      connectionState={connectionState}
                    />
                  )}
                </div>
              )}

              {selectedStack && selectedStatus === 'failed' && (
                <div>
                  <h3 className="text-sm font-medium text-gray-900 dark:text-white mb-4">
                    {selectedStack}
                    <span className="ml-2 text-xs font-normal text-red-600 dark:text-red-400">Failed</span>
                  </h3>
                  <div className="flex items-center gap-3 mb-4 p-4 rounded-lg bg-red-50 dark:bg-red-900/10">
                    <svg className="w-6 h-6 text-red-500 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                    </svg>
                    <p className="text-sm text-red-700 dark:text-red-300">
                      Stack deployment failed
                    </p>
                  </div>
                  {/* Show last known progress snapshot */}
                  {selectedProgress && (
                    <DeploymentProgressPanel
                      progressUpdate={selectedProgress}
                      initContainerLogs={selectedLogs}
                      connectionState={connectionState}
                    />
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ─── Configure State ──────────────────────────────────────────────────────

  return (
    <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6">
        <Link
          to={getBackUrl()}
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back
        </Link>
      </div>

      {/* Header */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
          Deploy {product?.name}
        </h1>
        <p className="text-gray-600 dark:text-gray-400">
          Configure and deploy all {product?.stacks.length} stacks to{' '}
          <span className="font-medium">{activeEnvironment?.name}</span>
        </p>
      </div>

      {/* Error Display */}
      {error && (
        <div className="mb-6 p-4 text-sm text-red-800 bg-red-100 rounded-lg dark:bg-red-900/30 dark:text-red-400">
          <p className="font-medium mb-1">Error</p>
          <p>{error}</p>
        </div>
      )}

      {!activeEnvironment && (
        <div className="mb-6 rounded-md bg-yellow-50 p-4 dark:bg-yellow-900/20">
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            No environment selected. Please select an environment to deploy.
          </p>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Main Configuration */}
        <div className="lg:col-span-2 space-y-6">
          {/* Product Configuration */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Product Configuration
            </h2>

            {/* Continue on Error */}
            <div className="flex items-center gap-3">
              <input
                type="checkbox"
                id="continueOnError"
                checked={continueOnError}
                onChange={(e) => setContinueOnError(e.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-brand-600 focus:ring-brand-500"
              />
              <label htmlFor="continueOnError" className="text-sm text-gray-700 dark:text-gray-300">
                Continue deploying remaining stacks if one fails
              </label>
            </div>
          </div>

          {/* Shared Variables */}
          {sharedVars.length > 0 && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
                Shared Variables
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">
                These variables are used by multiple stacks. Configure them once here.
              </p>

              {(() => {
                const groups = groupVariables(sharedVars);
                return Array.from(groups.entries()).map(([groupName, groupVars]) => (
                  <div key={groupName} className="mb-6 last:mb-0">
                    {groups.size > 1 && (
                      <h3 className="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-4 pb-2 border-b border-gray-200 dark:border-gray-700">
                        {groupName}
                      </h3>
                    )}
                    <div className="space-y-4">
                      {groupVars.map((v) => (
                        <VariableInput
                          key={v.name}
                          variable={v}
                          value={sharedVariableValues[v.name] || ''}
                          onChange={(newValue) =>
                            setSharedVariableValues(prev => ({ ...prev, [v.name]: newValue }))
                          }
                        />
                      ))}
                    </div>
                  </div>
                ));
              })()}
            </div>
          )}

          {/* Per-Stack Configuration (Accordion) */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
              Stack Configuration
            </h2>
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">
              Configure the deployment name and stack-specific variables for each stack.
            </p>

            <div className="space-y-3">
              {product?.stacks.map((stack) => {
                const isExpanded = expandedStacks.has(stack.id);
                const stackSpecific = getStackSpecificVariables(stack, sharedVarNames);
                const hasRequiredMissing = stackSpecific.some(v =>
                  v.isRequired && !perStackVariableValues[stack.id]?.[v.name]
                );

                return (
                  <div
                    key={stack.id}
                    className="border border-gray-200 rounded-lg dark:border-gray-700 overflow-hidden"
                  >
                    {/* Accordion Header */}
                    <button
                      onClick={() => toggleStackExpanded(stack.id)}
                      className="w-full flex items-center justify-between p-4 text-left hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors"
                    >
                      <div className="flex items-center gap-3">
                        <svg
                          className={`w-4 h-4 text-gray-400 transition-transform ${isExpanded ? 'rotate-90' : ''}`}
                          fill="none"
                          stroke="currentColor"
                          viewBox="0 0 24 24"
                        >
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                        </svg>
                        <span className="font-medium text-gray-900 dark:text-white">
                          {stack.name}
                        </span>
                        <span className="inline-flex items-center rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-800 dark:bg-blue-900/30 dark:text-blue-300">
                          {stack.services.length} service{stack.services.length !== 1 ? 's' : ''}
                        </span>
                        {stackSpecific.length > 0 && (
                          <span className="inline-flex items-center rounded-full bg-purple-100 px-2 py-0.5 text-xs font-medium text-purple-800 dark:bg-purple-900/30 dark:text-purple-300">
                            {stackSpecific.length} variable{stackSpecific.length !== 1 ? 's' : ''}
                          </span>
                        )}
                        {hasRequiredMissing && (
                          <span className="inline-flex items-center rounded-full bg-red-100 px-2 py-0.5 text-xs font-medium text-red-800 dark:bg-red-900/30 dark:text-red-300">
                            required
                          </span>
                        )}
                      </div>
                    </button>

                    {/* Accordion Content */}
                    {isExpanded && (
                      <div className="border-t border-gray-200 dark:border-gray-700 p-4 space-y-4">
                        {/* Stack Name */}
                        <div>
                          <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                            Deployment Name <span className="text-red-500">*</span>
                          </label>
                          <input
                            type="text"
                            value={stackNames[stack.id] || ''}
                            onChange={(e) =>
                              setStackNames(prev => ({ ...prev, [stack.id]: e.target.value }))
                            }
                            className="w-full px-4 py-3 border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                            placeholder={`${product?.name}-${stack.name}`}
                          />
                          <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                            Used to identify this stack deployment
                          </p>
                        </div>

                        {/* Stack-Specific Variables */}
                        {stackSpecific.length > 0 && (
                          <div>
                            {(() => {
                              const groups = groupVariables(stackSpecific);
                              return Array.from(groups.entries()).map(([groupName, groupVars]) => (
                                <div key={groupName} className="mb-4 last:mb-0">
                                  {groups.size > 1 && (
                                    <h4 className="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3 pb-2 border-b border-gray-200 dark:border-gray-700">
                                      {groupName}
                                    </h4>
                                  )}
                                  <div className="space-y-4">
                                    {groupVars.map((v) => (
                                      <VariableInput
                                        key={v.name}
                                        variable={v}
                                        value={perStackVariableValues[stack.id]?.[v.name] || ''}
                                        onChange={(newValue) =>
                                          setPerStackVariableValues(prev => ({
                                            ...prev,
                                            [stack.id]: {
                                              ...prev[stack.id],
                                              [v.name]: newValue,
                                            },
                                          }))
                                        }
                                      />
                                    ))}
                                  </div>
                                </div>
                              ));
                            })()}
                          </div>
                        )}

                        {stackSpecific.length === 0 && (
                          <p className="text-sm text-gray-500 dark:text-gray-400 italic">
                            No stack-specific variables. All variables are shared.
                          </p>
                        )}

                        {/* Services */}
                        {stack.services.length > 0 && (
                          <div className="pt-3 border-t border-gray-100 dark:border-gray-700">
                            <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
                              Services
                            </p>
                            <div className="flex flex-wrap gap-2">
                              {stack.services.map((service) => (
                                <span
                                  key={service}
                                  className="inline-flex items-center gap-1 rounded bg-gray-100 px-2 py-1 text-xs text-gray-700 dark:bg-gray-700 dark:text-gray-300"
                                >
                                  <svg className="w-2.5 h-2.5 text-green-500" fill="currentColor" viewBox="0 0 8 8">
                                    <circle cx="4" cy="4" r="3" />
                                  </svg>
                                  {service}
                                </span>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Actions */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            {/* Import .env Button */}
            {product && product.totalVariables > 0 && (
              <>
                <input
                  ref={envFileInputRef}
                  type="file"
                  onChange={handleEnvFileImport}
                  className="hidden"
                  id="env-file-input"
                />
                <label
                  htmlFor="env-file-input"
                  className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 cursor-pointer hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 mb-3"
                >
                  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
                  </svg>
                  Import .env
                </label>

                <button
                  onClick={handleResetToDefaults}
                  className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600 mb-3"
                >
                  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                  Reset to Defaults
                </button>
              </>
            )}

            {/* Deploy Button */}
            <button
              onClick={handleDeploy}
              disabled={!activeEnvironment || !product}
              className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
              </svg>
              Deploy All Stacks to {activeEnvironment?.name || 'Environment'}
            </button>
            <p className="mt-2 text-xs text-center text-gray-500 dark:text-gray-400">
              This will deploy {product?.stacks.length} stack{(product?.stacks.length || 0) !== 1 ? 's' : ''} sequentially
            </p>
          </div>

          {/* Product Info */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Product Info
            </h2>

            {product?.description && (
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
                {product.description}
              </p>
            )}

            <div className="space-y-3">
              {product?.version && (
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Version</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {product.version}
                  </span>
                </div>
              )}
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Stacks</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {product?.stacks.length || 0}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Total Services</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {product?.totalServices || 0}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Total Variables</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {product?.totalVariables || 0}
                </span>
              </div>
              {sharedVars.length > 0 && (
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Shared Variables</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {sharedVars.length}
                  </span>
                </div>
              )}
            </div>

            {/* Stack List */}
            {product && product.stacks.length > 0 && (
              <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
                <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
                  Stacks
                </p>
                <div className="space-y-1">
                  {product.stacks.map((stack) => (
                    <div
                      key={stack.id}
                      className="flex items-center justify-between text-sm text-gray-700 dark:text-gray-300"
                    >
                      <div className="flex items-center gap-2">
                        <svg className="w-3 h-3 text-green-500" fill="currentColor" viewBox="0 0 8 8">
                          <circle cx="4" cy="4" r="3" />
                        </svg>
                        {stack.name}
                      </div>
                      <span className="text-xs text-gray-400">
                        {stack.services.length} svc
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
