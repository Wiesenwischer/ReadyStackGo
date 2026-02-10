import { useEffect, useState, useRef, useCallback } from 'react';
import { useParams, Link } from 'react-router';
import { deployCompose, deployStack } from '../../api/deployments';
import { useEnvironment } from '../../context/EnvironmentContext';
import { type StackDetail, getStack, getProduct, type Product, type ProductVersion } from '../../api/stacks';
import VariableInput, { groupVariables } from '../../components/variables/VariableInput';
import { useDeploymentHub, type DeploymentProgressUpdate } from '../../hooks/useDeploymentHub';
import { getEnvironmentVariables, saveEnvironmentVariables } from '../../api/environments';

// Format phase names for display (PullingImages -> Pulling Images)
const formatPhase = (phase: string | undefined): string => {
  if (!phase) return '';
  // Insert space before each capital letter (except the first one)
  return phase.replace(/([A-Z])/g, ' $1').trim();
};

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

type DeployState = 'loading' | 'configure' | 'deploying' | 'success' | 'error';

export default function DeployStack() {
  const { stackId } = useParams<{ stackId: string }>();
  const { activeEnvironment } = useEnvironment();

  // Check if this is a custom deployment (no pre-defined stack)
  const isCustomDeploy = stackId === 'custom';

  const [state, setState] = useState<DeployState>(isCustomDeploy ? 'configure' : 'loading');
  const [stack, setStack] = useState<StackDetail | null>(null);
  const [product, setProduct] = useState<Product | null>(null);
  const [selectedStackId, setSelectedStackId] = useState<string | null>(stackId || null);
  const [stackName, setStackName] = useState('');
  const [yamlContent, setYamlContent] = useState('');
  const [variableValues, setVariableValues] = useState<Record<string, string>>({});
  const [error, setError] = useState('');
  const [deployWarnings, setDeployWarnings] = useState<string[]>([]);
  const envFileInputRef = useRef<HTMLInputElement>(null);

  // Deployment progress state
  // Use ref for session ID to avoid stale closures in SignalR callback
  const deploymentSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);

  // SignalR hub for real-time deployment progress
  // Use ref to avoid stale closure - the callback may fire before state is updated
  const handleDeploymentProgress = useCallback((update: DeploymentProgressUpdate) => {
    // Check against ref (updated synchronously) for immediate filtering
    const currentSessionId = deploymentSessionIdRef.current;
    if (currentSessionId && update.sessionId === currentSessionId) {
      setProgressUpdate(update);

      // Check if deployment completed (success or error)
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

  const { subscribeToDeployment, connectionState } = useDeploymentHub({
    onDeploymentProgress: handleDeploymentProgress,
  });

  // Load stack details (only if not custom)
  useEffect(() => {
    if (isCustomDeploy) {
      return; // No loading needed for custom deploy
    }

    if (!stackId) {
      setError('No stack ID provided');
      setState('error');
      return;
    }

    const loadStack = async () => {
      try {
        setState('loading');
        setError('');

        const detail = await getStack(stackId);
        setStack(detail);
        setStackName(detail.name);
        setSelectedStackId(stackId);

        // Load product to get available versions
        if (detail.productId) {
          try {
            const productData = await getProduct(detail.productId);
            setProduct(productData);
          } catch {
            // Product load failure is not critical, version selection just won't be available
          }
        }

        // Initialize variable values with defaults
        const initialValues: Record<string, string> = {};
        detail.variables.forEach(v => {
          initialValues[v.name] = v.defaultValue || '';
        });

        // Load saved environment variables and merge with defaults
        if (activeEnvironment) {
          try {
            const savedVars = await getEnvironmentVariables(activeEnvironment.id);
            // Merge saved values with defaults (saved values take precedence)
            Object.keys(savedVars.variables).forEach(key => {
              if (Object.prototype.hasOwnProperty.call(initialValues, key)) {
                initialValues[key] = savedVars.variables[key];
              }
            });
          } catch {
            // If loading fails, just use defaults
          }
        }

        setVariableValues(initialValues);

        setState('configure');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load stack');
        setState('error');
      }
    };

    loadStack();
  }, [stackId, isCustomDeploy]);

  // Handle version change
  const handleVersionChange = async (version: ProductVersion) => {
    if (!product) return;

    try {
      // Find the stack for the selected version (use default stack of that version)
      const newStackId = version.defaultStackId;
      setSelectedStackId(newStackId);

      // Load the stack details for the new version
      const detail = await getStack(newStackId);
      setStack(detail);
      setStackName(detail.name);

      // Re-initialize variables, keeping values where applicable
      const initialValues: Record<string, string> = {};
      detail.variables.forEach(v => {
        // Keep existing value if variable exists, otherwise use default
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
  };

  // Handle .env file import
  const handleEnvFileImport = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file && stack) {
      const reader = new FileReader();
      reader.onload = (event) => {
        const content = event.target?.result as string;
        const envValues = parseEnvContent(content);
        // Update variable values with matching keys from .env
        setVariableValues(prev => {
          const updated = { ...prev };
          for (const v of stack.variables) {
            if (envValues[v.name] !== undefined) {
              updated[v.name] = envValues[v.name];
            }
          }
          return updated;
        });
      };
      reader.readAsText(file);
    }
    // Reset input so same file can be selected again
    if (envFileInputRef.current) {
      envFileInputRef.current.value = '';
    }
  };

  // Handle reset to defaults
  const handleResetToDefaults = () => {
    if (!stack) return;

    // Reset all variables to their default values
    const defaultValues: Record<string, string> = {};
    stack.variables.forEach(v => {
      defaultValues[v.name] = v.defaultValue || '';
    });
    setVariableValues(defaultValues);
  };

  const handleDeploy = async () => {
    if (!stackName.trim()) {
      setError('Please provide a stack name');
      return;
    }

    if (!activeEnvironment) {
      setError('No environment selected');
      return;
    }

    // For custom deploy, require YAML content
    if (isCustomDeploy && !yamlContent.trim()) {
      setError('Please provide docker-compose YAML content');
      return;
    }

    // For stack deploy, require stack to be loaded
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

    // Generate session ID BEFORE the API call so we can receive progress updates immediately
    const sessionId = `${stackName}-${Date.now()}`;
    deploymentSessionIdRef.current = sessionId;

    setState('deploying');
    setError('');
    setProgressUpdate(null);

    // Subscribe to SignalR group BEFORE starting the API call
    // This ensures we don't miss any progress updates
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    try {
      let response;

      if (isCustomDeploy) {
        // Custom deploy: use old API with YAML content
        response = await deployCompose(activeEnvironment.id, {
          stackName,
          yamlContent,
          variables: variableValues,
          sessionId,
        });
      } else {
        // Catalog deploy: use new API with stackId (no YAML needed)
        response = await deployStack(activeEnvironment.id, selectedStackId!, {
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
      if (activeEnvironment) {
        try {
          await saveEnvironmentVariables(activeEnvironment.id, {
            variables: variableValues
          });
        } catch {
          // Ignore save errors - deployment was successful
        }
      }

      // State will be set to 'success' by SignalR callback when deployment completes
      // But if no SignalR connection, set it immediately
      if (connectionState !== 'connected') {
        setState('success');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Deployment failed');
      setState('error');
    }
  };

  // Get back URL - go back to product detail page in catalog
  const getBackUrl = () => {
    if (stack?.productId) {
      return `/catalog/${encodeURIComponent(stack.productId)}`;
    }
    return '/catalog';
  };

  if (state === 'loading') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-12">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-600 mx-auto mb-4"></div>
            <p className="text-gray-600 dark:text-gray-400">Loading stack...</p>
          </div>
        </div>
      </div>
    );
  }

  if (state === 'error' && !stack) {
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
              Stack Deployed Successfully!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              {stackName} has been deployed to {activeEnvironment?.name}
            </p>

            {deployWarnings.length > 0 && (
              <div className="w-full max-w-md mb-6 p-4 text-sm text-yellow-800 bg-yellow-100 rounded-lg dark:bg-yellow-900/30 dark:text-yellow-400">
                <p className="font-medium mb-2">Warnings:</p>
                <ul className="ml-4 list-disc">
                  {deployWarnings.map((w, i) => (
                    <li key={i}>{w}</li>
                  ))}
                </ul>
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

  if (state === 'deploying') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-brand-600 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Deploying Stack...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Deploying {stackName} to {activeEnvironment?.name}
            </p>

            {/* Progress Section */}
            <div className="w-full max-w-md">
              {/* Progress Bar */}
              <div className="mb-4">
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-gray-600 dark:text-gray-400">
                    {formatPhase(progressUpdate?.phase) || 'Initializing'}
                  </span>
                  <span className="text-gray-600 dark:text-gray-400">
                    {progressUpdate?.percentComplete ?? 0}%
                  </span>
                </div>
                <div className="h-3 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
                  <div
                    className="h-full bg-brand-600 rounded-full transition-all duration-500 ease-out"
                    style={{ width: `${progressUpdate?.percentComplete ?? 0}%` }}
                  />
                </div>
              </div>

              {/* Status Message */}
              <div className="text-center">
                <p className="text-sm text-gray-700 dark:text-gray-300 font-medium">
                  {progressUpdate?.message || 'Starting deployment...'}
                </p>

                {/* Service Progress */}
                {progressUpdate && (progressUpdate.totalServices > 0 || progressUpdate.totalInitContainers > 0) && (
                  <p className="text-xs text-gray-500 dark:text-gray-400 mt-2">
                    {progressUpdate.phase === 'PullingImages'
                      ? `Images: ${progressUpdate.completedServices} / ${progressUpdate.totalServices}`
                      : progressUpdate.phase === 'InitializingContainers'
                        ? `Init Containers: ${progressUpdate.completedInitContainers} / ${progressUpdate.totalInitContainers}`
                        : `Services: ${progressUpdate.completedServices} / ${progressUpdate.totalServices}`
                    }
                    {progressUpdate.currentService && (
                      <span className="ml-2">
                        (current: <span className="font-mono">{progressUpdate.currentService}</span>)
                      </span>
                    )}
                  </p>
                )}
              </div>

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
          </div>
        </div>
      </div>
    );
  }

  // Configure state
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
          {isCustomDeploy ? 'Deploy Custom Stack' : `Deploy ${stack?.name}`}
        </h1>
        <p className="text-gray-600 dark:text-gray-400">
          {isCustomDeploy
            ? 'Deploy a custom docker-compose stack'
            : `Configure and deploy this stack to `}
          {!isCustomDeploy && <span className="font-medium">{activeEnvironment?.name}</span>}
        </p>
      </div>

      {/* Error Display - Directly under header for visibility */}
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
          {/* Stack Name & Version */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Stack Configuration
            </h2>

            {/* Version Selection (only if multiple versions available) */}
            {!isCustomDeploy && product?.availableVersions && product.availableVersions.length > 1 && (
              <div className="mb-4">
                <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                  Version
                </label>
                <select
                  value={stack?.version || ''}
                  onChange={(e) => {
                    const version = product.availableVersions?.find(v => v.version === e.target.value);
                    if (version) handleVersionChange(version);
                  }}
                  className="w-full px-4 py-3 border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                >
                  {product.availableVersions.map((v) => (
                    <option key={v.productId} value={v.version}>
                      {v.version}{v.isCurrent ? ' (current)' : ''}
                    </option>
                  ))}
                </select>
                <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                  {product.availableVersions.length} versions available
                </p>
              </div>
            )}

            <div>
              <label className="block mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                Stack Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={stackName}
                onChange={(e) => setStackName(e.target.value)}
                className="w-full px-4 py-3 border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                placeholder="my-stack"
              />
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                This name will be used to identify the deployment
              </p>
            </div>
          </div>

          {/* Custom YAML Input (for custom deploy) */}
          {isCustomDeploy && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Docker Compose YAML
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
                Paste your docker-compose.yml content below.
              </p>
              <textarea
                value={yamlContent}
                onChange={(e) => setYamlContent(e.target.value)}
                rows={15}
                className="w-full px-4 py-3 font-mono text-sm border border-gray-300 rounded-lg dark:border-gray-600 dark:bg-gray-700 dark:text-white focus:ring-2 focus:ring-brand-500"
                placeholder={`version: "3.8"
services:
  web:
    image: nginx:alpine
    ports:
      - "80:80"`}
              />
            </div>
          )}

          {/* Variables (for stack deploy) */}
          {!isCustomDeploy && stack && stack.variables.length > 0 && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Environment Variables
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">
                Configure the environment variables for this deployment. Required fields are marked with *.
              </p>

              {/* Render variables grouped */}
              {(() => {
                const groups = groupVariables(stack.variables);
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
                          value={variableValues[v.name] || ''}
                          onChange={(newValue) =>
                            setVariableValues({ ...variableValues, [v.name]: newValue })
                          }
                        />
                      ))}
                    </div>
                  </div>
                ));
              })()}
            </div>
          )}

          </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Import & Deploy Actions - First for better UX */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            {/* Import .env Button (only for stack deploy with variables) */}
            {!isCustomDeploy && stack && stack.variables.length > 0 && (
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

                {/* Reset to Defaults Button */}
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
              disabled={!activeEnvironment || !stackName.trim() || (!isCustomDeploy && !selectedStackId) || (isCustomDeploy && !yamlContent.trim())}
              className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
              </svg>
              Deploy to {activeEnvironment?.name || 'Environment'}
            </button>
            <p className="mt-2 text-xs text-center text-gray-500 dark:text-gray-400">
              This will start the deployment process
            </p>
          </div>

          {/* Stack Info (only for stack deploy) */}
          {!isCustomDeploy && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Stack Info
              </h2>

              {stack?.description && (
                <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
                  {stack.description}
                </p>
              )}

              <div className="space-y-3">
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Services</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {stack?.services.length || 0}
                  </span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Variables</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {stack?.variables.length || 0}
                  </span>
                </div>
                {stack?.version && (
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-500 dark:text-gray-400">Version</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {stack.version}
                    </span>
                  </div>
                )}
              </div>

              {stack && stack.services.length > 0 && (
                <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
                  <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
                    Services
                  </p>
                  <div className="space-y-1">
                    {stack.services.map((service) => (
                      <div
                        key={service.name}
                        className="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300"
                      >
                        <svg className="w-3 h-3 text-green-500" fill="currentColor" viewBox="0 0 8 8">
                          <circle cx="4" cy="4" r="3" />
                        </svg>
                        {service.name}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Custom Deploy Info */}
          {isCustomDeploy && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Custom Deployment
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
                Deploy any docker-compose configuration by pasting the YAML content.
              </p>
              <div className="text-sm text-gray-500 dark:text-gray-400 space-y-2">
                <p>Requirements:</p>
                <ul className="list-disc list-inside space-y-1 ml-2">
                  <li>Valid docker-compose YAML</li>
                  <li>Unique stack name</li>
                  <li>Images must be accessible</li>
                </ul>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
