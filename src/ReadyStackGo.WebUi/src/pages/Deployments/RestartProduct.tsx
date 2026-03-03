import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router';
import {
  getProductDeployment,
  restartProductContainers,
  type GetProductDeploymentResponse,
  type StackRestartResult,
} from '../../api/deployments';
import { useEnvironment } from '../../context/EnvironmentContext';

type RestartState = 'loading' | 'confirm' | 'restarting' | 'success' | 'error';

export default function RestartProduct() {
  const { productDeploymentId } = useParams<{ productDeploymentId: string }>();
  const { activeEnvironment } = useEnvironment();

  const [state, setState] = useState<RestartState>('loading');
  const [deployment, setDeployment] = useState<GetProductDeploymentResponse | null>(null);
  const [error, setError] = useState('');
  const [restartResults, setRestartResults] = useState<StackRestartResult[]>([]);

  // Total service count
  const totalServices = deployment?.stacks.reduce((sum, s) => sum + s.serviceCount, 0) ?? 0;

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

        if (!response.canRestart) {
          setError(`Product "${response.productDisplayName}" cannot be restarted in its current state (${response.status})`);
          setState('error');
          return;
        }

        setState('confirm');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load product deployment');
        setState('error');
      }
    };

    loadDeployment();
  }, [activeEnvironment, productDeploymentId]);

  const handleRestart = async () => {
    if (!activeEnvironment || !deployment) {
      setError('No deployment to restart');
      return;
    }

    setState('restarting');
    setError('');

    try {
      const response = await restartProductContainers(
        activeEnvironment.id,
        deployment.productDeploymentId
      );
      setRestartResults(response.results);

      if (response.success) {
        setState('success');
      } else {
        setError(response.message || 'Restart completed with errors');
        setState('error');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to restart containers');
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
    const successCount = restartResults.filter(r => r.success).length;
    const totalStopped = restartResults.reduce((sum, r) => sum + r.containersStopped, 0);
    const totalStarted = restartResults.reduce((sum, r) => sum + r.containersStarted, 0);

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
              Containers Restarted Successfully!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-2">
              {deployment?.productDisplayName} ({successCount} stack{successCount !== 1 ? 's' : ''}) containers have been restarted
            </p>
            <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
              {totalStopped} stopped, {totalStarted} started
            </p>

            {/* Stack Results Summary */}
            {restartResults.length > 0 && (
              <div className="w-full max-w-lg mb-6">
                <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                  {restartResults.map((result) => (
                    <div key={result.stackName} className="flex items-center justify-between px-4 py-3 border-b last:border-b-0 border-gray-200 dark:border-gray-700">
                      <div className="flex items-center gap-3">
                        <svg className="w-4 h-4 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                        </svg>
                        <span className="text-sm font-medium text-gray-900 dark:text-white">{result.stackName}</span>
                      </div>
                      <span className="text-xs text-gray-500 dark:text-gray-400">
                        {result.containersStopped} stopped, {result.containersStarted} started
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div className="flex gap-4">
              <Link
                to={`/product-deployments/${productDeploymentId}`}
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

  // --- Restarting state ---
  if (state === 'restarting') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-amber-600 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Restarting Containers...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Restarting {deployment?.productDisplayName} in {activeEnvironment?.name}
            </p>

            <div className="w-full max-w-lg">
              {/* Stack List */}
              <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                {deployment?.stacks
                  .slice()
                  .sort((a, b) => a.order - b.order)
                  .map((stack) => (
                    <div
                      key={stack.stackName}
                      className="flex items-center justify-between px-4 py-3 border-b last:border-b-0 border-gray-200 dark:border-gray-700"
                    >
                      <div className="flex items-center gap-3">
                        <div className="w-4 h-4 border-2 border-amber-600 border-t-transparent rounded-full animate-spin" />
                        <span className="text-sm font-medium text-amber-700 dark:text-amber-300">
                          {stack.stackDisplayName}
                        </span>
                      </div>
                      <span className="text-xs text-gray-500 dark:text-gray-400">
                        {stack.serviceCount} service{stack.serviceCount !== 1 ? 's' : ''}
                      </span>
                    </div>
                  ))}
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // --- Error state (with deployment loaded, e.g. partial failure) ---
  if (state === 'error' && deployment) {
    const successCount = restartResults.filter(r => r.success).length;
    const failedCount = restartResults.filter(r => !r.success).length;

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to={`/product-deployments/${productDeploymentId}`}
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
              Restart Completed with Errors
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-2">
              {error}
            </p>
            {restartResults.length > 0 && (
              <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
                {successCount} restarted, {failedCount} failed of {restartResults.length} stacks
              </p>
            )}

            {/* Per-Stack Results */}
            {restartResults.length > 0 && (
              <div className="w-full max-w-lg mb-6">
                <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                  {restartResults.map((result) => (
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
                            {result.stackName}
                          </span>
                        </div>
                        <span className="text-xs text-gray-500 dark:text-gray-400">
                          {result.containersStopped} stopped, {result.containersStarted} started
                        </span>
                      </div>
                      {result.error && (
                        <p className="mt-1 ml-7 text-xs text-red-600 dark:text-red-400">
                          {result.error}
                        </p>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div className="flex gap-4">
              <Link
                to={`/product-deployments/${productDeploymentId}`}
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
          to={`/product-deployments/${productDeploymentId}`}
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back to Deployment
        </Link>
      </div>

      {/* Confirmation Card */}
      <div className="rounded-2xl border border-amber-200 bg-white p-8 dark:border-amber-800 dark:bg-white/[0.03]">
        <div className="flex flex-col items-center py-4">
          {/* Restart Icon */}
          <div className="flex items-center justify-center w-16 h-16 mb-6 bg-amber-100 rounded-full dark:bg-amber-900/30">
            <svg className="w-8 h-8 text-amber-600 dark:text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
          </div>

          <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
            Restart Product Containers
          </h1>
          <p className="text-gray-600 dark:text-gray-400 mb-2 text-center">
            Are you sure you want to restart all containers of <strong className="text-gray-900 dark:text-white">{deployment?.productDisplayName}</strong>?
          </p>
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-6 text-center max-w-md">
            This will stop and restart all {deployment?.totalStacks} stack{(deployment?.totalStacks ?? 0) !== 1 ? 's' : ''} with {totalServices} container{totalServices !== 1 ? 's' : ''} sequentially.
            Services will be briefly unavailable.
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
              Stacks to restart
            </h4>
            <div className="space-y-1">
              {deployment?.stacks
                .slice()
                .sort((a, b) => a.order - b.order)
                .map((stack) => (
                  <div key={stack.stackName} className="flex items-center justify-between py-1">
                    <div className="flex items-center gap-2">
                      <svg className="w-3 h-3 text-amber-400" fill="currentColor" viewBox="0 0 8 8">
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
              to={`/product-deployments/${productDeploymentId}`}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
            >
              Cancel
            </Link>
            <button
              onClick={handleRestart}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-amber-600 px-6 py-3 text-center font-medium text-white hover:bg-amber-700"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Restart All Containers
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
