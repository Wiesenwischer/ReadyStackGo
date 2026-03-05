import { useParams, Link } from 'react-router';
import {
  useRedeployProductStore,
  type DeployProductStackResult,
} from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';
import { useEnvironment } from '../../context/EnvironmentContext';

export default function RedeployProduct() {
  const { productDeploymentId } = useParams<{ productDeploymentId: string }>();
  const { token } = useAuth();
  const { activeEnvironment } = useEnvironment();

  const store = useRedeployProductStore(token, activeEnvironment?.id, productDeploymentId);

  // --- Loading state ---
  if (store.state === 'loading') {
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
  if (store.state === 'error' && !store.deployment) {
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
          <p className="text-sm text-red-800 dark:text-red-200">{store.error}</p>
        </div>
      </div>
    );
  }

  // --- Success state ---
  if (store.state === 'success') {
    const displayResults: DeployProductStackResult[] = store.stackResults.length > 0
      ? store.stackResults
      : (store.deployment?.stacks.map(s => ({
            stackName: s.stackName,
            stackDisplayName: s.stackDisplayName,
            serviceCount: s.serviceCount,
            success: store.stackStatuses[s.stackName] !== 'failed',
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
              Redeploy Successful!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              {store.deployment?.productDisplayName} — {successCount} stack{successCount !== 1 ? 's' : ''} redeployed successfully
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
                to={`/product-deployments/${store.deployment?.productDeploymentId}`}
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

  // --- Redeploying state ---
  if (store.state === 'redeploying') {
    const totalStacks = store.deployment?.totalStacks ?? 0;
    const completedRedeployStacks = Object.values(store.stackStatuses).filter(s => s === 'running').length;
    const failedRedeployStacks = Object.values(store.stackStatuses).filter(s => s === 'failed').length;
    const processedStacks = completedRedeployStacks + failedRedeployStacks;

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-blue-500 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Redeploying Stacks...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Redeploying {store.deployment?.productDisplayName} in {activeEnvironment?.name}
            </p>

            <div className="w-full max-w-lg">
              {/* Overall Progress */}
              <div className="mb-6">
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-gray-600 dark:text-gray-400">
                    {store.progressUpdate?.message || 'Initializing redeploy...'}
                  </span>
                  <span className="text-gray-600 dark:text-gray-400">
                    {processedStacks}/{totalStacks} stacks
                  </span>
                </div>
                <div className="h-3 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
                  <div
                    className="h-full bg-blue-500 rounded-full transition-all duration-500 ease-out"
                    style={{ width: `${totalStacks > 0 ? (processedStacks / totalStacks) * 100 : 0}%` }}
                  />
                </div>
              </div>

              {/* Per-Stack Status List */}
              <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                {store.deployment?.stacks
                  .slice()
                  .sort((a, b) => a.order - b.order)
                  .map((stack) => {
                    const status = store.stackStatuses[stack.stackName] || 'pending';
                    return (
                      <div
                        key={stack.stackName}
                        className={`flex items-center justify-between px-4 py-3 border-b last:border-b-0 border-gray-200 dark:border-gray-700 ${
                          status === 'deploying' ? 'bg-blue-50 dark:bg-blue-900/10' : ''
                        }`}
                      >
                        <div className="flex items-center gap-3">
                          {/* Status Icon */}
                          {status === 'pending' && (
                            <div className="w-4 h-4 rounded-full border-2 border-gray-300 dark:border-gray-600" />
                          )}
                          {status === 'deploying' && (
                            <div className="w-4 h-4 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
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
                            status === 'deploying' ? 'text-blue-700 dark:text-blue-300' :
                            status === 'running' ? 'text-green-700 dark:text-green-400' :
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
                  store.connectionState === 'connected' ? 'bg-green-500' :
                  store.connectionState === 'connecting' ? 'bg-yellow-500' :
                  store.connectionState === 'reconnecting' ? 'bg-yellow-500' :
                  'bg-red-500'
                }`} />
                {store.connectionState === 'connected' ? 'Live updates' :
                 store.connectionState === 'connecting' ? 'Connecting...' :
                 store.connectionState === 'reconnecting' ? 'Reconnecting...' :
                 'Updates unavailable'}
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // --- Error state (with deployment loaded, e.g. partial failure) ---
  if (store.state === 'error' && store.deployment) {
    const errorDisplayResults: DeployProductStackResult[] = store.stackResults.length > 0
      ? store.stackResults
      : (store.deployment.stacks.map(s => ({
            stackName: s.stackName,
            stackDisplayName: s.stackDisplayName,
            serviceCount: s.serviceCount,
            success: store.stackStatuses[s.stackName] === 'running',
            errorMessage: store.stackStatuses[s.stackName] === 'failed' ? 'Redeploy failed' : undefined,
          })));
    const successCount = errorDisplayResults.filter(r => r.success).length;
    const failedCount = errorDisplayResults.filter(r => !r.success).length;

    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to={`/product-deployments/${store.deployment.productDeploymentId}`}
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
              Redeploy Completed with Errors
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-2">
              {store.error}
            </p>
            {errorDisplayResults.length > 0 && (
              <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
                {successCount} succeeded, {failedCount} failed of {errorDisplayResults.length} stacks
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
                to={`/product-deployments/${store.deployment.productDeploymentId}`}
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
  const allStacks = store.deployment?.stacks.slice().sort((a, b) => a.order - b.order) ?? [];

  return (
    <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6">
        <Link
          to={`/product-deployments/${store.deployment?.productDeploymentId}`}
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back to Deployment
        </Link>
      </div>

      {/* Confirmation Card */}
      <div className="rounded-2xl border border-blue-200 bg-white p-8 dark:border-blue-800 dark:bg-white/[0.03]">
        <div className="flex flex-col items-center py-4">
          {/* Redeploy Icon */}
          <div className="flex items-center justify-center w-16 h-16 mb-6 bg-blue-100 rounded-full dark:bg-blue-900/30">
            <svg className="w-8 h-8 text-blue-600 dark:text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
          </div>

          <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
            Redeploy Product
          </h1>
          <p className="text-gray-600 dark:text-gray-400 mb-2 text-center">
            Redeploy all stacks of <strong className="text-gray-900 dark:text-white">{store.deployment?.productDisplayName}</strong>?
          </p>
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-6 text-center max-w-md">
            This will re-deploy all {allStacks.length} stack{allStacks.length !== 1 ? 's' : ''} with a fresh image pull
            using the same version and configuration.
          </p>

          {/* Deployment Info */}
          <div className="w-full max-w-lg mb-6 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg">
            <h3 className="text-sm font-medium text-gray-900 dark:text-white mb-3">Product Details</h3>
            <div className="space-y-2 text-sm mb-4">
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Product:</span>
                <span className="font-medium text-gray-900 dark:text-white">{store.deployment?.productDisplayName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Version:</span>
                <span className="font-medium text-gray-900 dark:text-white">v{store.deployment?.productVersion}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Environment:</span>
                <span className="font-medium text-gray-900 dark:text-white">{activeEnvironment?.name}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Stacks:</span>
                <span className="font-medium text-gray-900 dark:text-white">{allStacks.length}</span>
              </div>
            </div>

            {/* Stacks to Redeploy */}
            <h4 className="text-xs font-medium text-gray-500 dark:text-gray-400 mb-2 uppercase tracking-wider">
              Stacks to redeploy
            </h4>
            <div className="space-y-1">
              {allStacks.map((stack) => (
                <div key={stack.stackName} className="flex items-center justify-between py-1">
                  <div className="flex items-center gap-2">
                    <svg className="w-3 h-3 text-blue-500" fill="currentColor" viewBox="0 0 8 8">
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
          </div>

          {/* Error Display */}
          {store.error && (
            <div className="w-full max-w-lg mb-6 p-4 text-sm text-red-800 bg-red-100 rounded-lg dark:bg-red-900/30 dark:text-red-400">
              <p className="font-medium mb-1">Error</p>
              <p>{store.error}</p>
            </div>
          )}

          {/* Action Buttons */}
          <div className="flex gap-4">
            <Link
              to={`/product-deployments/${store.deployment?.productDeploymentId}`}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
            >
              Cancel
            </Link>
            <button
              onClick={store.handleRedeploy}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-blue-500 px-6 py-3 text-center font-medium text-white hover:bg-blue-600"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Redeploy All Stacks
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
