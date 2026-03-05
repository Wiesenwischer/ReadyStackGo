import { useEffect, useRef } from 'react';
import { useParams, Link, useNavigate } from 'react-router';
import { useRollbackStore } from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';
import { useEnvironment } from '../../context/EnvironmentContext';

export default function RollbackStack() {
  const { stackName } = useParams<{ stackName: string }>();
  const navigate = useNavigate();
  const { token } = useAuth();
  const { activeEnvironment } = useEnvironment();
  const logEndRef = useRef<HTMLDivElement>(null);

  const store = useRollbackStore(token, activeEnvironment?.id, stackName);

  // Auto-scroll init container logs to bottom
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [store.initContainerLogs]);

  const getBackUrl = () => `/deployments/${encodeURIComponent(stackName || '')}`;

  if (store.state === 'loading') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-12">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-600 mx-auto mb-4"></div>
            <p className="text-gray-600 dark:text-gray-400">Loading rollback data...</p>
          </div>
        </div>
      </div>
    );
  }

  if (store.state === 'error' && !store.rollbackInfo) {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to={getBackUrl()}
            className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back to Deployment
          </Link>
        </div>
        <div className="rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">{store.error}</p>
        </div>
      </div>
    );
  }

  if (store.state === 'success') {
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
              Rollback Successful!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              {store.deployment?.stackName} has been rolled back to version {store.rollbackInfo?.rollbackTargetVersion}
            </p>

            <div className="flex gap-4">
              <Link
                to={getBackUrl()}
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

  if (store.state === 'rolling_back') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-amber-600 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Rolling Back...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Rolling back {store.deployment?.stackName} to version {store.rollbackInfo?.rollbackTargetVersion}
            </p>

            {/* Progress Section */}
            <div className="w-full max-w-md">
              {/* Progress Bar */}
              <div className="mb-4">
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-gray-600 dark:text-gray-400">
                    {store.formattedPhase || 'Initializing'}
                  </span>
                  <span className="text-gray-600 dark:text-gray-400">
                    {store.progressUpdate?.percentComplete ?? 0}%
                  </span>
                </div>
                <div className="h-3 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
                  <div
                    className="h-full bg-amber-600 rounded-full transition-all duration-500 ease-out"
                    style={{ width: `${store.progressUpdate?.percentComplete ?? 0}%` }}
                  />
                </div>
              </div>

              {/* Status Message */}
              <div className="text-center">
                <p className="text-sm text-gray-700 dark:text-gray-300 font-medium">
                  {store.progressUpdate?.message || 'Starting rollback...'}
                </p>

                {store.progressUpdate && (store.progressUpdate.totalServices > 0 || store.progressUpdate.totalInitContainers > 0) && (
                  <p className="text-xs text-gray-500 dark:text-gray-400 mt-2">
                    {store.progressUpdate.phase === 'PullingImages'
                      ? `Images: ${store.progressUpdate.completedServices} / ${store.progressUpdate.totalServices}`
                      : store.progressUpdate.phase === 'InitializingContainers'
                        ? `Init Containers: ${store.progressUpdate.completedInitContainers} / ${store.progressUpdate.totalInitContainers}`
                        : `Services: ${store.progressUpdate.completedServices} / ${store.progressUpdate.totalServices}`
                    }
                    {store.progressUpdate.currentService && (
                      <span className="ml-2">
                        (current: <span className="font-mono">{store.progressUpdate.currentService}</span>)
                      </span>
                    )}
                  </p>
                )}
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

            {/* Init Container Logs - full width */}
            {Object.keys(store.initContainerLogs).length > 0 && (
              <div className="mt-6 w-full">
                <div className="px-3 py-2 text-xs font-medium text-gray-600 dark:text-gray-400 bg-gray-100 dark:bg-gray-800 rounded-t-lg">
                  Init Container Logs
                </div>
                <div className="bg-gray-900 rounded-b-lg p-3 max-h-80 overflow-y-auto">
                  {Object.entries(store.initContainerLogs).map(([name, lines]) => (
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

  // Confirm state
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
          Back to Deployment
        </Link>
      </div>

      {/* Header */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
          Rollback {store.deployment?.stackName}
        </h1>
        <p className="text-gray-600 dark:text-gray-400">
          Rollback to version <span className="font-medium">{store.rollbackInfo?.rollbackTargetVersion}</span>
        </p>
      </div>

      {/* Error Display */}
      {store.error && (
        <div className="mb-6 p-4 text-sm text-red-800 bg-red-100 rounded-lg dark:bg-red-900/30 dark:text-red-400">
          <p className="font-medium mb-1">Error</p>
          <p>{store.error}</p>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Main Content */}
        <div className="lg:col-span-2 space-y-6">
          {/* Warning Banner */}
          <div className="rounded-2xl border border-amber-200 bg-amber-50 p-6 dark:border-amber-800 dark:bg-amber-900/20">
            <div className="flex items-start gap-4">
              <div className="flex-shrink-0">
                <svg className="w-6 h-6 text-amber-600 dark:text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
              </div>
              <div>
                <h3 className="text-lg font-semibold text-amber-800 dark:text-amber-200 mb-2">
                  Confirm Rollback
                </h3>
                <p className="text-sm text-amber-700 dark:text-amber-300 mb-3">
                  This will restore <strong>{store.deployment?.stackName}</strong> to version{' '}
                  <strong>{store.rollbackInfo?.rollbackTargetVersion}</strong>.
                </p>
                <ul className="text-sm text-amber-700 dark:text-amber-300 list-disc list-inside space-y-1">
                  <li>The current failed deployment will be replaced</li>
                  <li>All services will be restarted with the previous configuration</li>
                  <li>This action cannot be undone automatically</li>
                </ul>
              </div>
            </div>
          </div>

          {/* Snapshot Info */}
          {store.rollbackInfo?.snapshotDescription && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Snapshot Details
              </h2>
              <div className="space-y-3">
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Description</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {store.rollbackInfo.snapshotDescription}
                  </span>
                </div>
                {store.rollbackInfo.snapshotCreatedAt && (
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-500 dark:text-gray-400">Created At</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {new Date(store.rollbackInfo.snapshotCreatedAt).toLocaleString()}
                    </span>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Rollback Actions */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            {/* Rollback Button */}
            <button
              onClick={store.handleRollback}
              disabled={!activeEnvironment}
              className="w-full inline-flex items-center justify-center gap-2 rounded-md bg-amber-600 px-6 py-3 text-center font-medium text-white hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
              </svg>
              Start Rollback
            </button>
            <p className="mt-2 text-xs text-center text-gray-500 dark:text-gray-400">
              This will restore the previous version
            </p>
          </div>

          {/* Rollback Info */}
          <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Rollback Info
            </h2>

            <div className="space-y-3">
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Current Status</span>
                <span className="font-medium text-red-600 dark:text-red-400">
                  Failed
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Current Version</span>
                <span className="font-medium text-gray-900 dark:text-white">
                  {store.rollbackInfo?.currentVersion || 'Unknown'}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Rollback To</span>
                <span className="font-medium text-amber-600 dark:text-amber-400">
                  {store.rollbackInfo?.rollbackTargetVersion}
                </span>
              </div>
            </div>
          </div>

          {/* Cancel Button */}
          <button
            onClick={() => navigate(getBackUrl())}
            className="w-full inline-flex items-center justify-center gap-2 rounded-md border border-gray-300 bg-white px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
