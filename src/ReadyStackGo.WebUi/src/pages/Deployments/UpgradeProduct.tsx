import { useEffect, useState, useRef, useCallback } from 'react';
import { useParams, Link, useSearchParams } from 'react-router';
import {
  getProductDeployment,
  checkProductUpgrade,
  upgradeProduct,
  type GetProductDeploymentResponse,
  type CheckProductUpgradeResponse,
  type UpgradeProductStackResult,
} from '../../api/deployments';
import { useEnvironment } from '../../context/EnvironmentContext';
import { type Product, type ProductStack, type StackVariable, getProduct } from '../../api/stacks';
import VariableInput, { groupVariables } from '../../components/variables/VariableInput';
import { useDeploymentHub, type DeploymentProgressUpdate, type InitContainerLogEntry } from '../../hooks/useDeploymentHub';

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

type UpgradeState = 'loading' | 'configure' | 'upgrading' | 'success' | 'error';

// Stack status for the upgrading view
type StackProgressStatus = 'pending' | 'upgrading' | 'running' | 'failed';

export default function UpgradeProduct() {
  const { productDeploymentId } = useParams<{ productDeploymentId: string }>();
  const [searchParams] = useSearchParams();
  const { activeEnvironment } = useEnvironment();

  // Get optional target version from URL params
  const targetVersionParam = searchParams.get('version');

  const [state, setState] = useState<UpgradeState>('loading');
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
  const logEndRef = useRef<HTMLDivElement>(null);
  const envFileInputRef = useRef<HTMLInputElement>(null);

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

  const { subscribeToDeployment, connectionState } = useDeploymentHub({
    onDeploymentProgress: handleUpgradeProgress,
    onInitContainerLog: handleInitContainerLog,
  });

  // Auto-scroll init container logs
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [initContainerLogs]);

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
      const existingStack = deployment.stacks.find(
        s => s.stackName.toLowerCase() === stack.name.toLowerCase()
      );

      // For existing stacks, overlay current deployment values onto shared vars
      if (existingStack && deployment.sharedVariables) {
        // The deployment's shared variables were applied to all stacks,
        // but stack-specific vars may have per-stack overrides stored in the deployment
      }

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
    if (!productDeploymentId || !activeEnvironment) {
      setError('Missing product deployment ID or environment');
      setState('error');
      return;
    }

    const loadData = async () => {
      try {
        setState('loading');
        setError('');

        // 1. Load product deployment
        const deployment = await getProductDeployment(activeEnvironment.id, productDeploymentId);
        setProductDeployment(deployment);

        // 2. Check upgrade availability
        const upgradeCheck = await checkProductUpgrade(activeEnvironment.id, productDeploymentId);
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
  }, [productDeploymentId, activeEnvironment, targetVersionParam, initializeVariables]);

  // Handle version change
  const handleVersionChange = async (newVersion: string) => {
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
  };

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
    if (file && targetProduct) {
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
      };
      reader.readAsText(file);
    }
    if (envFileInputRef.current) {
      envFileInputRef.current.value = '';
    }
  };

  const handleUpgrade = async () => {
    if (!targetProduct || !activeEnvironment || !productDeployment || !upgradeInfo) {
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
      // Build stack configs: for existing stacks use current deployment name, for new stacks generate name
      const stackConfigs = targetProduct.stacks.map(stack => {
        const existingStack = productDeployment.stacks.find(
          s => s.stackName.toLowerCase() === stack.name.toLowerCase()
        );
        return {
          stackId: stack.id,
          deploymentStackName: existingStack?.deploymentStackName || `${productDeployment.productName}-${stack.name}`.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, ''),
          variables: perStackVariableValues[stack.id] || {},
        };
      });

      const response = await upgradeProduct(activeEnvironment.id, productDeploymentId!, {
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
  };

  const getBackUrl = () => {
    if (productDeployment?.productId) {
      return `/catalog/${encodeURIComponent(productDeployment.productId)}`;
    }
    return '/catalog';
  };

  // Check if a stack is new in this upgrade version
  const isNewStack = (stackName: string): boolean => {
    return upgradeInfo?.newStacks?.some(
      n => n.toLowerCase() === stackName.toLowerCase()
    ) ?? false;
  };

  // ─── Loading State ────────────────────────────────────────────────────────

  if (state === 'loading') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-12">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-600 mx-auto mb-4"></div>
            <p className="text-gray-600 dark:text-gray-400">Loading upgrade data...</p>
          </div>
        </div>
      </div>
    );
  }

  // ─── Error State (no product loaded) ──────────────────────────────────────

  if (state === 'error' && !targetProduct) {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to="/deployments"
            className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back to Deployments
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
              Product Upgraded Successfully!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-2">
              {productDeployment?.productName} has been upgraded to v{selectedVersion || upgradeInfo?.latestVersion}
            </p>
            {productDeployment?.productVersion && (
              <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
                Previous version: v{productDeployment.productVersion}
              </p>
            )}

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
                        {result.isNewInUpgrade && (
                          <span className="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-300">
                            New
                          </span>
                        )}
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

  // ─── Error State (after upgrade) ───────────────────────────────────────

  if (state === 'error' && targetProduct) {
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
              Upgrade {failedCount > 0 && successCount > 0 ? 'Partially' : ''} Failed
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-2">
              {productDeployment?.productName} — v{productDeployment?.productVersion} to v{selectedVersion || upgradeInfo?.latestVersion}
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
                        {result.isNewInUpgrade && (
                          <span className="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-300">
                            New
                          </span>
                        )}
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

  // ─── Upgrading State ──────────────────────────────────────────────────────

  if (state === 'upgrading') {
    const totalStacks = targetProduct?.stacks.length || 0;
    const completedCount = Object.values(stackStatuses).filter(s => s === 'running').length;
    const failedCount = Object.values(stackStatuses).filter(s => s === 'failed').length;
    const overallPercent = totalStacks > 0
      ? Math.round(((completedCount + failedCount) / totalStacks) * 100)
      : progressUpdate?.percentComplete ?? 0;

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-brand-600 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Upgrading Product...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Upgrading {productDeployment?.productName} from v{productDeployment?.productVersion} to v{selectedVersion || upgradeInfo?.latestVersion}
            </p>

            {/* Overall Progress */}
            <div className="w-full max-w-lg">
              <div className="mb-4">
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-gray-600 dark:text-gray-400">
                    {progressUpdate?.phase === 'ProductDeploy'
                      ? progressUpdate.message || 'Upgrading stacks...'
                      : currentUpgradingStack
                        ? `Upgrading: ${currentUpgradingStack}`
                        : formatPhase(progressUpdate?.phase) || 'Initializing'}
                  </span>
                  <span className="text-gray-600 dark:text-gray-400">
                    {completedCount}/{totalStacks} stacks
                  </span>
                </div>
                <div className="h-3 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
                  <div
                    className="h-full bg-brand-600 rounded-full transition-all duration-500 ease-out"
                    style={{ width: `${overallPercent}%` }}
                  />
                </div>
              </div>

              {/* Stack Status List */}
              {targetProduct && (
                <div className="mt-4 space-y-2">
                  {targetProduct.stacks.map((stack) => {
                    const status = stackStatuses[stack.name] || 'pending';
                    const isNew = isNewStack(stack.name);
                    return (
                      <div
                        key={stack.id}
                        className="flex items-center justify-between p-2 rounded-lg bg-gray-50 dark:bg-gray-800/50"
                      >
                        <div className="flex items-center gap-2">
                          {status === 'pending' && (
                            <span className="w-4 h-4 rounded-full border-2 border-gray-300 dark:border-gray-600" />
                          )}
                          {status === 'upgrading' && (
                            <div className="w-4 h-4 border-2 border-brand-600 border-t-transparent rounded-full animate-spin" />
                          )}
                          {status === 'running' && (
                            <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                            </svg>
                          )}
                          {status === 'failed' && (
                            <svg className="w-4 h-4 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                            </svg>
                          )}
                          <span className={`text-sm ${
                            status === 'upgrading' ? 'font-medium text-brand-600 dark:text-brand-400' :
                            status === 'running' ? 'text-green-700 dark:text-green-400' :
                            status === 'failed' ? 'text-red-700 dark:text-red-400' :
                            'text-gray-600 dark:text-gray-400'
                          }`}>
                            {stack.name}
                          </span>
                          {isNew && (
                            <span className="inline-flex items-center rounded-full bg-green-100 px-1.5 py-0.5 text-xs font-medium text-green-700 dark:bg-green-900/30 dark:text-green-300">
                              New
                            </span>
                          )}
                        </div>
                        <span className={`text-xs px-2 py-0.5 rounded-full ${
                          status === 'pending' ? 'bg-gray-100 text-gray-500 dark:bg-gray-700 dark:text-gray-400' :
                          status === 'upgrading' ? 'bg-brand-100 text-brand-700 dark:bg-brand-900/30 dark:text-brand-300' :
                          status === 'running' ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300' :
                          'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300'
                        }`}>
                          {status === 'pending' ? 'Pending' :
                           status === 'upgrading' ? 'Upgrading' :
                           status === 'running' ? 'Running' : 'Failed'}
                        </span>
                      </div>
                    );
                  })}
                </div>
              )}

              {/* Current Stack Detail Progress */}
              {progressUpdate && progressUpdate.phase !== 'ProductDeploy' && (
                <div className="mt-4 p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
                  <p className="text-xs text-gray-500 dark:text-gray-400 mb-1">
                    {formatPhase(progressUpdate.phase)}
                  </p>
                  <p className="text-sm text-gray-700 dark:text-gray-300">
                    {progressUpdate.message}
                  </p>
                  {(progressUpdate.totalServices > 0 || progressUpdate.totalInitContainers > 0) && (
                    <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                      {progressUpdate.phase === 'PullingImages'
                        ? `Images: ${progressUpdate.completedServices} / ${progressUpdate.totalServices}`
                        : progressUpdate.phase === 'InitializingContainers'
                          ? `Init Containers: ${progressUpdate.completedInitContainers} / ${progressUpdate.totalInitContainers}`
                          : `Services: ${progressUpdate.completedServices} / ${progressUpdate.totalServices}`
                      }
                    </p>
                  )}
                </div>
              )}

              {/* Connection Status */}
              <div className="mt-6 flex items-center justify-center gap-2 text-xs text-gray-500 dark:text-gray-400">
                <span className={`w-2 h-2 rounded-full ${
                  connectionState === 'connected' ? 'bg-green-500' :
                  connectionState === 'connecting' ? 'bg-yellow-500' :
                  connectionState === 'reconnecting' ? 'bg-yellow-500' :
                  'bg-red-500'
                }`} />
                {connectionState === 'connected' ? 'Live updates' :
                 connectionState === 'connecting' ? 'Connecting...' :
                 connectionState === 'reconnecting' ? 'Reconnecting...' :
                 'Updates unavailable'}
              </div>
            </div>

            {/* Init Container Logs */}
            {Object.keys(initContainerLogs).length > 0 && (
              <div className="mt-6 w-full">
                <div className="px-3 py-2 text-xs font-medium text-gray-600 dark:text-gray-400 bg-gray-100 dark:bg-gray-800 rounded-t-lg">
                  Init Container Logs
                </div>
                <div className="bg-gray-900 rounded-b-lg p-3 max-h-80 overflow-y-auto">
                  {Object.entries(initContainerLogs).map(([name, lines]) => (
                    <div key={name} className="mb-2 last:mb-0">
                      <div className="text-xs font-bold text-blue-400 mb-1">{name}</div>
                      {lines.map((line, i) => (
                        <div key={i} className="font-mono text-xs text-green-400 whitespace-pre-wrap break-all leading-relaxed">{line}</div>
                      ))}
                    </div>
                  ))}
                  <div ref={logEndRef} />
                </div>
              </div>
            )}
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
          Upgrade {productDeployment?.productName}
        </h1>
        <p className="text-gray-600 dark:text-gray-400">
          Upgrade from version <span className="font-medium">{productDeployment?.productVersion}</span> to{' '}
          <span className="font-medium">{selectedVersion || upgradeInfo?.latestVersion}</span> on{' '}
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

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Main Configuration */}
        <div className="lg:col-span-2 space-y-6">
          {/* Version Selection */}
          {(upgradeInfo?.availableVersions?.length ?? 0) > 1 && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Target Version
              </h2>
              <div>
                <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                  Select target version
                </label>
                <select
                  value={selectedVersion ?? upgradeInfo?.latestVersion ?? ''}
                  onChange={(e) => handleVersionChange(e.target.value)}
                  className="w-full px-4 py-3 border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                >
                  {upgradeInfo?.availableVersions?.map((v) => (
                    <option key={v.version} value={v.version}>
                      {v.version}{v.version === upgradeInfo.latestVersion ? ' (latest)' : ''} — {v.stackCount} stack{v.stackCount !== 1 ? 's' : ''}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          )}

          {/* Configuration Changes Info */}
          {((upgradeInfo?.newStacks?.length ?? 0) > 0 || (upgradeInfo?.removedStacks?.length ?? 0) > 0) && (
            <div className="rounded-2xl border border-blue-200 bg-blue-50 p-6 dark:border-blue-800 dark:bg-blue-900/20">
              <h2 className="text-lg font-semibold text-blue-900 dark:text-blue-100 mb-4">
                Stack Changes
              </h2>
              {(upgradeInfo?.newStacks?.length ?? 0) > 0 && (
                <div className="mb-3">
                  <p className="text-sm font-medium text-blue-800 dark:text-blue-200 mb-2">New Stacks:</p>
                  <div className="flex flex-wrap gap-2">
                    {upgradeInfo?.newStacks?.map((s) => (
                      <span key={s} className="inline-flex items-center rounded-full bg-green-100 px-3 py-1 text-xs font-medium text-green-800 dark:bg-green-800 dark:text-green-200">
                        {s}
                      </span>
                    ))}
                  </div>
                </div>
              )}
              {(upgradeInfo?.removedStacks?.length ?? 0) > 0 && (
                <div>
                  <p className="text-sm font-medium text-amber-700 dark:text-amber-400 mb-2">Stacks no longer in target version:</p>
                  <div className="flex flex-wrap gap-2">
                    {upgradeInfo?.removedStacks?.map((s) => (
                      <span key={s} className="inline-flex items-center rounded-full bg-amber-100 px-3 py-1 text-xs font-medium text-amber-800 dark:bg-amber-800 dark:text-amber-200">
                        {s}
                      </span>
                    ))}
                  </div>
                  <p className="text-xs text-amber-600 dark:text-amber-400 mt-2">
                    These stacks will not be automatically removed. You can remove them manually after the upgrade.
                  </p>
                </div>
              )}
            </div>
          )}

          {/* Upgrade Configuration */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Upgrade Configuration
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
                Continue upgrading remaining stacks if one fails
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
                These variables are used by multiple stacks. Current values are preserved where applicable.
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
              Review and adjust stack-specific variables for the upgrade.
            </p>

            <div className="space-y-3">
              {targetProduct?.stacks.map((stack) => {
                const isExpanded = expandedStacks.has(stack.id);
                const stackSpecific = getStackSpecificVariables(stack, sharedVarNames);
                const isNew = isNewStack(stack.name);
                const existingStack = productDeployment?.stacks.find(
                  s => s.stackName.toLowerCase() === stack.name.toLowerCase()
                );
                const hasRequiredMissing = stackSpecific.some(v =>
                  v.isRequired && !perStackVariableValues[stack.id]?.[v.name]
                );

                return (
                  <div
                    key={stack.id}
                    className={`border rounded-lg overflow-hidden ${
                      isNew
                        ? 'border-green-300 dark:border-green-700'
                        : 'border-gray-200 dark:border-gray-700'
                    }`}
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
                        {isNew ? (
                          <span className="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-300">
                            New Stack
                          </span>
                        ) : (
                          <span className="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600 dark:bg-gray-700 dark:text-gray-400">
                            Existing
                          </span>
                        )}
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
                        {/* Deployment Stack Name */}
                        <div>
                          <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                            Deployment Name
                          </label>
                          <input
                            type="text"
                            value={existingStack?.deploymentStackName || `${productDeployment?.productName}-${stack.name}`.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')}
                            readOnly
                            className="w-full px-4 py-3 border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white bg-gray-50 dark:bg-gray-800 cursor-not-allowed"
                          />
                          <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                            {isNew ? 'Auto-generated name for the new stack' : 'Name preserved from existing deployment'}
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
            {targetProduct && targetProduct.totalVariables > 0 && (
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
              </>
            )}

            {/* Upgrade Button */}
            <button
              onClick={handleUpgrade}
              disabled={!activeEnvironment}
              className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-blue-600 px-6 py-3 text-center font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16V4m0 0L3 8m4-4l4 4m6 0v12m0 0l4-4m-4 4l-4-4" />
              </svg>
              Upgrade All Stacks to {selectedVersion || upgradeInfo?.latestVersion}
            </button>
            <p className="mt-2 text-xs text-center text-gray-500 dark:text-gray-400">
              This will upgrade {targetProduct?.stacks.length} stack{(targetProduct?.stacks.length || 0) !== 1 ? 's' : ''} sequentially
            </p>
          </div>

          {/* Upgrade Info */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Upgrade Info
            </h2>

            <div className="space-y-3">
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Current Version</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {productDeployment?.productVersion}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Target Version</span>
                <span className="font-medium text-blue-600 dark:text-blue-400">
                  {selectedVersion || upgradeInfo?.latestVersion}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Current Stacks</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {productDeployment?.totalStacks || 0}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Target Stacks</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {targetProduct?.stacks.length || 0}
                </span>
              </div>
              {targetProduct && (
                <>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-500 dark:text-gray-400">Total Services</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {targetProduct.totalServices}
                    </span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-500 dark:text-gray-400">Total Variables</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {targetProduct.totalVariables}
                    </span>
                  </div>
                </>
              )}
              {sharedVars.length > 0 && (
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Shared Variables</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {sharedVars.length}
                  </span>
                </div>
              )}
              {productDeployment && productDeployment.upgradeCount > 0 && (
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Previous Upgrades</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {productDeployment.upgradeCount}
                  </span>
                </div>
              )}
            </div>

            {/* Stack List */}
            {targetProduct && targetProduct.stacks.length > 0 && (
              <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
                <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
                  Target Stacks
                </p>
                <div className="space-y-1">
                  {targetProduct.stacks.map((stack) => {
                    const isNew = isNewStack(stack.name);
                    return (
                      <div
                        key={stack.id}
                        className="flex items-center justify-between text-sm text-gray-700 dark:text-gray-300"
                      >
                        <div className="flex items-center gap-2">
                          <svg className="w-3 h-3 text-green-500" fill="currentColor" viewBox="0 0 8 8">
                            <circle cx="4" cy="4" r="3" />
                          </svg>
                          {stack.name}
                          {isNew && (
                            <span className="text-xs text-green-600 dark:text-green-400">(new)</span>
                          )}
                        </div>
                        <span className="text-xs text-gray-400">
                          {stack.services.length} svc
                        </span>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>

          {/* Cancel Button */}
          <Link
            to={getBackUrl()}
            className="w-full inline-flex items-center justify-center gap-2 rounded-md border border-gray-300 bg-white px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700"
          >
            Cancel
          </Link>
        </div>
      </div>
    </div>
  );
}
