import { useEffect, useState, useRef, useCallback } from 'react';
import { useParams, Link } from 'react-router';
import {
  getProductDeployment,
  retryProduct,
  type GetProductDeploymentResponse,
  type DeployProductStackResult,
} from '../../api/deployments';
import { useEnvironment } from '../../context/EnvironmentContext';
import { useDeploymentHub, type DeploymentProgressUpdate } from '../../hooks/useDeploymentHub';

type RetryState = 'loading' | 'confirm' | 'retrying' | 'success' | 'error';
type StackRetryStatus = 'skipped' | 'pending' | 'deploying' | 'running' | 'failed';

export default function RetryProduct() {
  const { productDeploymentId } = useParams<{ productDeploymentId: string }>();
  const { activeEnvironment } = useEnvironment();

  const [state, setState] = useState<RetryState>('loading');
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [error, setError] = useState('');
  const [stackResults, setStackResults] = useState<DeployProductStackResult[]>([]);

  // Per-stack progress tracking
  const [stackStatuses, setStackStatuses] = useState<Record<string, StackRetryStatus>>({});
  // Progress state
  const retrySessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);

  // Prevent race condition: first completion (SignalR or API) wins
  const completedRef = useRef(false);

  // SignalR hub for real-time progress
  const handleRetryProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = retrySessionIdRef.current;
    if (!currentSessionId || update.sessionId !== currentSessionId) return;

    setProgressUpdate(update);

    // Track per-stack status using currentService (stack technical name)
    if (update.phase === 'ProductDeploy' && update.currentService) {
      const stackName = update.currentService;
      if (update.message?.startsWith('Retrying stack')) {
        setStackStatuses(prev => ({
          ...prev,
          [stackName]: 'deploying',
        }));
      } else if (update.message?.includes('retried successfully')) {
        setStackStatuses(prev => ({
          ...prev,
          [stackName]: 'running',
        }));
      } else if (update.message?.includes('retry failed')) {
        setStackStatuses(prev => ({
          ...prev,
          [stackName]: 'failed',
        }));
      }
    }

    // SignalR isComplete drives the final state transition
    if (update.isComplete && !completedRef.current) {
      completedRef.current = true;
      if (update.isError) {
        setError(update.errorMessage || 'Retry failed');
        setState('error');
      } else {
        setState('success');
      }
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub({
    onDeploymentProgress: handleRetryProgress,
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

        if (!response.canRetry) {
          setError(`Product "${response.productDisplayName}" cannot be retried in its current state (${response.status})`);
          setState('error');
          return;
        }

        // Initialize stack statuses: Running stacks are skipped, Failed/Pending will be retried
        const initialStatuses: Record<string, StackRetryStatus> = {};
        for (const stack of response.stacks) {
          initialStatuses[stack.stackName] = stack.status === 'Running' ? 'skipped' : 'pending';
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

  const handleRetry = async () => {
    if (!activeEnvironment || !deployment) {
      setError('No deployment to retry');
      return;
    }

    // Generate session ID BEFORE the API call
    const sessionId = `product-retry-${deployment.productName}-${Date.now()}`;
    retrySessionIdRef.current = sessionId;
    completedRef.current = false;

    setState('retrying');
    setError('');
    setProgressUpdate(null);

    // Subscribe to SignalR group BEFORE starting the API call
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    // Fire-and-forget: SignalR drives live progress.
    // API response is stored for the success screen and serves as a fallback
    // if SignalR doesn't deliver the completion message.
    retryProduct(
      activeEnvironment.id,
      deployment.productDeploymentId,
      { sessionId, continueOnError: true }
    )
      .then(response => {
        // Always store API results (success screen needs them)
        setStackResults(response.stackResults || []);

        // Fallback: if SignalR hasn't completed within 3s, use API response
        setTimeout(() => {
          if (!completedRef.current) {
            completedRef.current = true;

            const finalStatuses: Record<string, StackRetryStatus> = {};
            for (const stack of deployment.stacks) {
              if (stack.status === 'Running') {
                finalStatuses[stack.stackName] = 'skipped';
              } else {
                const result = response.stackResults.find(r => r.stackName === stack.stackName);
                finalStatuses[stack.stackName] = result?.success ? 'running' : 'failed';
              }
            }
            setStackStatuses(finalStatuses);

            if (!response.success) {
              setError(response.message || 'Retry completed with errors');
              setState('error');
            } else {
              setState('success');
            }
          }
        }, 3000);
      })
      .catch(err => {
        // Fallback: if SignalR hasn't completed within 3s, use API error
        setTimeout(() => {
          if (!completedRef.current) {
            completedRef.current = true;
            setError(err instanceof Error ? err.message : 'Retry failed');
            setState('error');
          }
        }, 3000);
      });
  };

  // Computed values
  const failedStacks = deployment?.stacks.filter(s => s.status === 'Failed' || s.status === 'Pending') ?? [];
  const runningStacks = deployment?.stacks.filter(s => s.status === 'Running') ?? [];

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
    const displayResults: DeployProductStackResult[] = stackResults.length > 0
      ? stackResults
      : (deployment?.stacks
          .filter(s => s.status !== 'Running')
          .map(s => ({
            stackName: s.stackName,
            stackDisplayName: s.stackDisplayName,
            serviceCount: s.serviceCount,
            success: stackStatuses[s.stackName] !== 'failed',
          })) ?? []);
    const successCount = displayResults.filter(r => r.success).length;

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
              Retry Successful!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              {deployment?.productDisplayName} — {successCount} stack{successCount !== 1 ? 's' : ''} retried successfully
            </p>

            {/* Stack Results Summary */}
            {displayResults.length > 0 && (
              <div className="w-full max-w-lg mb-6">
                <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                  {displayResults.map((result) => (
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
                to={`/product-deployments/${deployment?.productDeploymentId}`}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
              >
                View Deployment
              </Link>
              <Link
                to="/deployments"
                className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                All Deployments
              </Link>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // --- Retrying state ---
  if (state === 'retrying') {
    const totalRetryStacks = failedStacks.length;
    const completedRetryStacks = Object.values(stackStatuses).filter(s => s === 'running').length;
    const failedRetryStacks = Object.values(stackStatuses).filter(s => s === 'failed').length;
    const processedStacks = completedRetryStacks + failedRetryStacks;

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-yellow-500 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Retrying Failed Stacks...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Retrying {deployment?.productDisplayName} in {activeEnvironment?.name}
            </p>

            <div className="w-full max-w-lg">
              {/* Overall Progress */}
              <div className="mb-6">
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-gray-600 dark:text-gray-400">
                    {progressUpdate?.message || 'Initializing retry...'}
                  </span>
                  <span className="text-gray-600 dark:text-gray-400">
                    {processedStacks}/{totalRetryStacks} stacks
                  </span>
                </div>
                <div className="h-3 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
                  <div
                    className="h-full bg-yellow-500 rounded-full transition-all duration-500 ease-out"
                    style={{ width: `${totalRetryStacks > 0 ? (processedStacks / totalRetryStacks) * 100 : 0}%` }}
                  />
                </div>
              </div>

              {/* Per-Stack Status List */}
              <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                {deployment?.stacks
                  .slice()
                  .sort((a, b) => a.order - b.order)
                  .map((stack) => {
                    const status = stackStatuses[stack.stackName] || 'pending';
                    return (
                      <div
                        key={stack.stackName}
                        className={`flex items-center justify-between px-4 py-3 border-b last:border-b-0 border-gray-200 dark:border-gray-700 ${
                          status === 'deploying' ? 'bg-yellow-50 dark:bg-yellow-900/10' : ''
                        }`}
                      >
                        <div className="flex items-center gap-3">
                          {/* Status Icon */}
                          {status === 'skipped' && (
                            <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                            </svg>
                          )}
                          {status === 'pending' && (
                            <div className="w-4 h-4 rounded-full border-2 border-gray-300 dark:border-gray-600" />
                          )}
                          {status === 'deploying' && (
                            <div className="w-4 h-4 border-2 border-yellow-500 border-t-transparent rounded-full animate-spin" />
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
                          <span className={`text-sm font-medium ${
                            status === 'skipped' ? 'text-gray-400 dark:text-gray-500' :
                            status === 'deploying' ? 'text-yellow-700 dark:text-yellow-300' :
                            status === 'running' ? 'text-green-700 dark:text-green-400' :
                            status === 'failed' ? 'text-red-600 dark:text-red-400' :
                            'text-gray-900 dark:text-white'
                          }`}>
                            {stack.stackDisplayName}
                          </span>
                          {status === 'skipped' && (
                            <span className="text-xs text-gray-400 dark:text-gray-500">(already running)</span>
                          )}
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
    const errorDisplayResults: DeployProductStackResult[] = stackResults.length > 0
      ? stackResults
      : (deployment.stacks
          .filter(s => s.status !== 'Running')
          .map(s => ({
            stackName: s.stackName,
            stackDisplayName: s.stackDisplayName,
            serviceCount: s.serviceCount,
            success: stackStatuses[s.stackName] === 'running',
            errorMessage: stackStatuses[s.stackName] === 'failed' ? 'Retry failed' : undefined,
          })));
    const successCount = errorDisplayResults.filter(r => r.success).length;
    const failedCount = errorDisplayResults.filter(r => !r.success).length;

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to={`/product-deployments/${deployment.productDeploymentId}`}
            className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back to Deployment
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
              Retry Completed with Errors
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-2">
              {error}
            </p>
            {errorDisplayResults.length > 0 && (
              <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
                {successCount} succeeded, {failedCount} failed of {errorDisplayResults.length} retried stacks
              </p>
            )}

            {/* Per-Stack Results */}
            {errorDisplayResults.length > 0 && (
              <div className="w-full max-w-lg mb-6">
                <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                  {errorDisplayResults.map((result) => (
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
                to={`/product-deployments/${deployment.productDeploymentId}`}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-brand-600 px-6 py-3 text-center font-medium text-white hover:bg-brand-700"
              >
                View Deployment
              </Link>
              <Link
                to="/deployments"
                className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              >
                All Deployments
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
          to={`/product-deployments/${deployment?.productDeploymentId}`}
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back to Deployment
        </Link>
      </div>

      {/* Confirmation Card */}
      <div className="rounded-2xl border border-yellow-200 bg-white p-8 dark:border-yellow-800 dark:bg-white/[0.03]">
        <div className="flex flex-col items-center py-4">
          {/* Warning Icon */}
          <div className="flex items-center justify-center w-16 h-16 mb-6 bg-yellow-100 rounded-full dark:bg-yellow-900/30">
            <svg className="w-8 h-8 text-yellow-600 dark:text-yellow-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
          </div>

          <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
            Retry Failed Stacks
          </h1>
          <p className="text-gray-600 dark:text-gray-400 mb-2 text-center">
            Retry failed stacks of <strong className="text-gray-900 dark:text-white">{deployment?.productDisplayName}</strong>?
          </p>
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-6 text-center max-w-md">
            This will re-deploy {failedStacks.length} failed stack{failedStacks.length !== 1 ? 's' : ''} using the original configuration.
            {runningStacks.length > 0 && ` ${runningStacks.length} already running stack${runningStacks.length !== 1 ? 's' : ''} will be skipped.`}
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
                <span className="text-gray-600 dark:text-gray-400">Failed Stacks:</span>
                <span className="font-medium text-red-600 dark:text-red-400">{failedStacks.length} of {deployment?.totalStacks}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Running Stacks:</span>
                <span className="font-medium text-green-600 dark:text-green-400">{runningStacks.length} (skipped)</span>
              </div>
            </div>

            {/* Stacks to Retry */}
            {failedStacks.length > 0 && (
              <>
                <h4 className="text-xs font-medium text-gray-500 dark:text-gray-400 mb-2 uppercase tracking-wider">
                  Stacks to retry
                </h4>
                <div className="space-y-1 mb-3">
                  {failedStacks
                    .sort((a, b) => a.order - b.order)
                    .map((stack) => (
                      <div key={stack.stackName} className="flex items-center justify-between py-1">
                        <div className="flex items-center gap-2">
                          <svg className="w-3 h-3 text-yellow-500" fill="currentColor" viewBox="0 0 8 8">
                            <circle cx="4" cy="4" r="3" />
                          </svg>
                          <span className="text-sm text-gray-700 dark:text-gray-300">
                            {stack.stackDisplayName}
                          </span>
                        </div>
                        <span className="text-xs text-gray-500 dark:text-gray-400">
                          {stack.serviceCount} service{stack.serviceCount !== 1 ? 's' : ''}
                        </span>
                      </div>
                    ))}
                </div>
              </>
            )}

            {/* Running Stacks (skipped) */}
            {runningStacks.length > 0 && (
              <>
                <h4 className="text-xs font-medium text-gray-500 dark:text-gray-400 mb-2 uppercase tracking-wider">
                  Already running (will be skipped)
                </h4>
                <div className="space-y-1">
                  {runningStacks
                    .sort((a, b) => a.order - b.order)
                    .map((stack) => (
                      <div key={stack.stackName} className="flex items-center justify-between py-1">
                        <div className="flex items-center gap-2">
                          <svg className="w-3 h-3 text-green-500" fill="currentColor" viewBox="0 0 8 8">
                            <circle cx="4" cy="4" r="3" />
                          </svg>
                          <span className="text-sm text-gray-400 dark:text-gray-500">
                            {stack.stackDisplayName}
                          </span>
                        </div>
                        <span className="text-xs text-gray-400 dark:text-gray-500">
                          {stack.serviceCount} service{stack.serviceCount !== 1 ? 's' : ''}
                        </span>
                      </div>
                    ))}
                </div>
              </>
            )}
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
              to={`/product-deployments/${deployment?.productDeploymentId}`}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
            >
              Cancel
            </Link>
            <button
              onClick={handleRetry}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-yellow-500 px-6 py-3 text-center font-medium text-white hover:bg-yellow-600"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Retry Failed Stacks
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
