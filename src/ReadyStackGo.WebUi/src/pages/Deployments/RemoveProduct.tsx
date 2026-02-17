import { useEffect, useState, useRef, useCallback } from 'react';
import { useParams, Link } from 'react-router';
import {
  getProductDeployment,
  removeProductDeployment,
  type GetProductDeploymentResponse,
  type RemoveProductStackResult,
} from '../../api/deployments';
import { useEnvironment } from '../../context/EnvironmentContext';
import { useDeploymentHub, type DeploymentProgressUpdate } from '../../hooks/useDeploymentHub';

// Format phase names for display (RemovingContainers -> Removing Containers)
const formatPhase = (phase: string | undefined): string => {
  if (!phase) return '';
  return phase.replace(/([A-Z])/g, ' $1').trim();
};

type RemoveState = 'loading' | 'confirm' | 'removing' | 'success' | 'error';
type StackRemoveStatus = 'pending' | 'removing' | 'removed' | 'failed';

export default function RemoveProduct() {
  const { productDeploymentId } = useParams<{ productDeploymentId: string }>();
  const { activeEnvironment } = useEnvironment();

  const [state, setState] = useState<RemoveState>('loading');
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [error, setError] = useState('');
  const [stackResults, setStackResults] = useState<RemoveProductStackResult[]>([]);

  // Per-stack progress tracking
  const [stackStatuses, setStackStatuses] = useState<Record<string, StackRemoveStatus>>({});
  const [completedCount, setCompletedCount] = useState(0);

  // Progress state
  const removeSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);

  // Total service count
  const totalServices = deployment?.stacks.reduce((sum, s) => sum + s.serviceCount, 0) ?? 0;

  // SignalR hub for real-time progress
  const handleRemoveProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = removeSessionIdRef.current;
    if (!currentSessionId || update.sessionId !== currentSessionId) return;

    setProgressUpdate(update);

    // Track per-stack status from phase/message info
    if (update.phase === 'ProductRemoval' && update.message) {
      // Parse "Removing stack X/N: stackName" messages
      const match = update.message.match(/Removing stack (\d+)\/(\d+): (.+)/);
      if (match) {
        const stackName = match[3];
        setStackStatuses(prev => ({
          ...prev,
          [stackName]: 'removing',
        }));
      }

      // Parse "Stack removed: stackName" messages
      const removedMatch = update.message.match(/Stack removed: (.+)/);
      if (removedMatch) {
        const stackName = removedMatch[1];
        setStackStatuses(prev => ({
          ...prev,
          [stackName]: 'removed',
        }));
        setCompletedCount(prev => prev + 1);
      }

      // Parse "Stack removal failed: stackName" messages
      const failedMatch = update.message.match(/Stack removal failed: (.+)/);
      if (failedMatch) {
        const stackName = failedMatch[1];
        setStackStatuses(prev => ({
          ...prev,
          [stackName]: 'failed',
        }));
        setCompletedCount(prev => prev + 1);
      }
    }

    // Check if removal completed (success or error)
    if (update.isComplete) {
      if (update.isError) {
        setError(update.errorMessage || 'Removal failed');
        setState('error');
      } else {
        setState('success');
      }
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub({
    onDeploymentProgress: handleRemoveProgress,
  });

  // Load product deployment details
  useEffect(() => {
    if (!activeEnvironment || !productDeploymentId) {
      setState('error');
      setError('No environment or product deployment ID provided');
      return;
    }

    const loadDeployment = async () => {
      try {
        setState('loading');
        setError('');

        const response = await getProductDeployment(activeEnvironment.id, productDeploymentId);
        setDeployment(response);

        if (!response.canRemove) {
          setError(`Product "${response.productDisplayName}" cannot be removed in its current state (${response.status})`);
          setState('error');
          return;
        }

        // Initialize stack statuses
        const initialStatuses: Record<string, StackRemoveStatus> = {};
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
  }, [activeEnvironment, productDeploymentId]);

  const handleRemove = async () => {
    if (!activeEnvironment || !deployment) {
      setError('No deployment to remove');
      return;
    }

    // Generate session ID BEFORE the API call
    const sessionId = `product-remove-${deployment.productName}-${Date.now()}`;
    removeSessionIdRef.current = sessionId;

    setState('removing');
    setError('');
    setProgressUpdate(null);
    setCompletedCount(0);

    // Subscribe to SignalR group BEFORE starting the API call
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    try {
      const response = await removeProductDeployment(
        activeEnvironment.id,
        deployment.productDeploymentId,
        { sessionId }
      );

      setStackResults(response.stackResults || []);

      // Update stack statuses from results
      const finalStatuses: Record<string, StackRemoveStatus> = {};
      for (const result of response.stackResults) {
        finalStatuses[result.stackName] = result.success ? 'removed' : 'failed';
      }
      setStackStatuses(finalStatuses);

      if (!response.success) {
        setError(response.message || 'Removal completed with errors');
        setState('error');
        return;
      }

      // If no SignalR connection, set success immediately
      if (connectionState !== 'connected') {
        setState('success');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Removal failed');
      setState('error');
    }
  };

  // --- Loading state ---
  if (state === 'loading') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-12">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-600 mx-auto mb-4"></div>
            <p className="text-gray-600 dark:text-gray-400">Loading product deployment...</p>
          </div>
        </div>
      </div>
    );
  }

  // --- Error state (no deployment loaded) ---
  if (state === 'error' && !deployment) {
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

  // --- Success state ---
  if (state === 'success') {
    const successCount = stackResults.filter(r => r.success).length;
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
              Product Removed Successfully!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              {deployment?.productDisplayName} ({successCount} stack{successCount !== 1 ? 's' : ''}) has been removed from {activeEnvironment?.name}
            </p>

            {/* Stack Results Summary */}
            {stackResults.length > 0 && (
              <div className="w-full max-w-lg mb-6">
                <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                  {stackResults.map((result) => (
                    <div key={result.stackName} className="flex items-center justify-between px-4 py-3 border-b last:border-b-0 border-gray-200 dark:border-gray-700">
                      <div className="flex items-center gap-3">
                        <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                        </svg>
                        <span className="text-sm font-medium text-gray-900 dark:text-white">{result.stackDisplayName}</span>
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
                to="/catalog"
                className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Browse Catalog
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // --- Removing state ---
  if (state === 'removing') {
    const totalStacks = deployment?.stacks.length ?? 0;
    const removedStacks = Object.values(stackStatuses).filter(s => s === 'removed').length;
    const failedStacks = Object.values(stackStatuses).filter(s => s === 'failed').length;
    const processedStacks = removedStacks + failedStacks;

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-red-600 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Removing Product...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Removing {deployment?.productDisplayName} from {activeEnvironment?.name}
            </p>

            <div className="w-full max-w-lg">
              {/* Overall Progress */}
              <div className="mb-6">
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-gray-600 dark:text-gray-400">
                    {formatPhase(progressUpdate?.phase) || 'Initializing'}
                  </span>
                  <span className="text-gray-600 dark:text-gray-400">
                    {processedStacks}/{totalStacks} stacks
                  </span>
                </div>
                <div className="h-3 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
                  <div
                    className="h-full bg-red-600 rounded-full transition-all duration-500 ease-out"
                    style={{ width: `${totalStacks > 0 ? (processedStacks / totalStacks) * 100 : 0}%` }}
                  />
                </div>
              </div>

              {/* Status Message */}
              <div className="text-center mb-6">
                <p className="text-sm text-gray-700 dark:text-gray-300 font-medium">
                  {progressUpdate?.message || 'Starting removal...'}
                </p>
              </div>

              {/* Per-Stack Status List */}
              <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                {deployment?.stacks
                  .slice()
                  .sort((a, b) => b.order - a.order) // Reverse order (removal order)
                  .map((stack) => {
                    const status = stackStatuses[stack.stackName] || 'pending';
                    return (
                      <div
                        key={stack.stackName}
                        className={`flex items-center justify-between px-4 py-3 border-b last:border-b-0 border-gray-200 dark:border-gray-700 ${
                          status === 'removing' ? 'bg-red-50 dark:bg-red-900/10' : ''
                        }`}
                      >
                        <div className="flex items-center gap-3">
                          {/* Status Icon */}
                          {status === 'pending' && (
                            <div className="w-4 h-4 rounded-full border-2 border-gray-300 dark:border-gray-600" />
                          )}
                          {status === 'removing' && (
                            <div className="w-4 h-4 border-2 border-red-600 border-t-transparent rounded-full animate-spin" />
                          )}
                          {status === 'removed' && (
                            <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                            </svg>
                          )}
                          {status === 'failed' && (
                            <svg className="w-4 h-4 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                            </svg>
                          )}
                          <span className={`text-sm font-medium ${
                            status === 'removing' ? 'text-red-700 dark:text-red-300' :
                            status === 'removed' ? 'text-gray-500 dark:text-gray-400 line-through' :
                            status === 'failed' ? 'text-red-600 dark:text-red-400' :
                            'text-gray-900 dark:text-white'
                          }`}>
                            {stack.stackDisplayName}
                          </span>
                        </div>
                        <span className="text-xs text-gray-500 dark:text-gray-400">
                          {stack.serviceCount} service{stack.serviceCount !== 1 ? 's' : ''}
                        </span>
                      </div>
                    );
                  })}
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

  // --- Error state (with deployment loaded, e.g. partial failure) ---
  if (state === 'error' && deployment) {
    const successCount = stackResults.filter(r => r.success).length;
    const failedCount = stackResults.filter(r => !r.success).length;

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

        <div className="rounded-2xl border border-red-200 bg-white p-8 dark:border-red-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-4">
            <div className="flex items-center justify-center w-16 h-16 mb-6 bg-red-100 rounded-full dark:bg-red-900/30">
              <svg className="w-8 h-8 text-red-600 dark:text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
            </div>

            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Removal Completed with Errors
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-2">
              {error}
            </p>
            {stackResults.length > 0 && (
              <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
                {successCount} removed, {failedCount} failed of {stackResults.length} stacks
              </p>
            )}

            {/* Per-Stack Results */}
            {stackResults.length > 0 && (
              <div className="w-full max-w-lg mb-6">
                <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                  {stackResults.map((result) => (
                    <div key={result.stackName} className="px-4 py-3 border-b last:border-b-0 border-gray-200 dark:border-gray-700">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-3">
                          {result.success ? (
                            <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                            </svg>
                          ) : (
                            <svg className="w-4 h-4 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                            </svg>
                          )}
                          <span className={`text-sm font-medium ${
                            result.success
                              ? 'text-gray-900 dark:text-white'
                              : 'text-red-600 dark:text-red-400'
                          }`}>
                            {result.stackDisplayName}
                          </span>
                        </div>
                        <span className="text-xs text-gray-500 dark:text-gray-400">
                          {result.serviceCount} service{result.serviceCount !== 1 ? 's' : ''}
                        </span>
                      </div>
                      {result.errorMessage && (
                        <p className="mt-1 ml-7 text-xs text-red-600 dark:text-red-400">
                          {result.errorMessage}
                        </p>
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
                to="/catalog"
                className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                Browse Catalog
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // --- Confirm state ---
  return (
    <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
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

      {/* Confirmation Card */}
      <div className="rounded-2xl border border-red-200 bg-white p-8 dark:border-red-800 dark:bg-white/[0.03]">
        <div className="flex flex-col items-center py-4">
          {/* Warning Icon */}
          <div className="flex items-center justify-center w-16 h-16 mb-6 bg-red-100 rounded-full dark:bg-red-900/30">
            <svg className="w-8 h-8 text-red-600 dark:text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          </div>

          <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
            Remove Product Deployment
          </h1>
          <p className="text-gray-600 dark:text-gray-400 mb-2 text-center">
            Are you sure you want to remove <strong className="text-gray-900 dark:text-white">{deployment?.productDisplayName}</strong>?
          </p>
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-6 text-center max-w-md">
            This will stop and remove all {deployment?.totalStacks} stack{(deployment?.totalStacks ?? 0) !== 1 ? 's' : ''} with {totalServices} container{totalServices !== 1 ? 's' : ''}.
            This action cannot be undone.
          </p>

          {/* Deployment Info */}
          <div className="w-full max-w-lg mb-6 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg">
            <h3 className="text-sm font-medium text-gray-900 dark:text-white mb-3">Product Details</h3>
            <div className="space-y-2 text-sm mb-4">
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Product:</span>
                <span className="font-medium text-gray-900 dark:text-white">{deployment?.productDisplayName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Version:</span>
                <span className="font-medium text-gray-900 dark:text-white">v{deployment?.productVersion}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Environment:</span>
                <span className="font-medium text-gray-900 dark:text-white">{activeEnvironment?.name}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Stacks:</span>
                <span className="font-medium text-gray-900 dark:text-white">{deployment?.totalStacks}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Total Services:</span>
                <span className="font-medium text-gray-900 dark:text-white">{totalServices}</span>
              </div>
            </div>

            {/* Stack List */}
            <h4 className="text-xs font-medium text-gray-500 dark:text-gray-400 mb-2 uppercase tracking-wider">
              Stacks to remove (in reverse order)
            </h4>
            <div className="space-y-1">
              {deployment?.stacks
                .slice()
                .sort((a, b) => b.order - a.order)
                .map((stack) => (
                  <div key={stack.stackName} className="flex items-center justify-between py-1">
                    <div className="flex items-center gap-2">
                      <svg className="w-3 h-3 text-red-400" fill="currentColor" viewBox="0 0 8 8">
                        <circle cx="4" cy="4" r="3" />
                      </svg>
                      <span className="text-sm text-gray-700 dark:text-gray-300">
                        {stack.stackDisplayName}
                      </span>
                      {stack.deploymentStackName && (
                        <span className="text-xs text-gray-400 dark:text-gray-500 font-mono">
                          ({stack.deploymentStackName})
                        </span>
                      )}
                    </div>
                    <span className="text-xs text-gray-500 dark:text-gray-400">
                      {stack.serviceCount} service{stack.serviceCount !== 1 ? 's' : ''}
                    </span>
                  </div>
                ))}
            </div>
          </div>

          {/* Error Display */}
          {error && (
            <div className="w-full max-w-lg mb-6 p-4 text-sm text-red-800 bg-red-100 rounded-lg dark:bg-red-900/30 dark:text-red-400">
              <p className="font-medium mb-1">Error</p>
              <p>{error}</p>
            </div>
          )}

          {/* Action Buttons */}
          <div className="flex gap-4">
            <Link
              to="/catalog"
              className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
            >
              Cancel
            </Link>
            <button
              onClick={handleRemove}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-red-600 px-6 py-3 text-center font-medium text-white hover:bg-red-700"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
              Remove All Stacks
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
