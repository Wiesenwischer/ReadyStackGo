import { useParams, Link, useNavigate } from 'react-router';
import { useRemoveStackStore } from '@rsgo/core';
import { useAuth } from '../../context/AuthContext';
import { useEnvironment } from '../../context/EnvironmentContext';

export default function RemoveStack() {
  const { stackName } = useParams<{ stackName: string }>();
  const { token } = useAuth();
  const { activeEnvironment } = useEnvironment();
  const navigate = useNavigate();

  const store = useRemoveStackStore(token, activeEnvironment?.id, stackName);

  const handleCancel = () => {
    navigate('/deployments');
  };

  if (store.state === 'loading') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="flex items-center justify-center py-12">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-600 mx-auto mb-4"></div>
            <p className="text-gray-600 dark:text-gray-400">Loading deployment...</p>
          </div>
        </div>
      </div>
    );
  }

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
              Deployment Removed Successfully!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              {store.deployment?.stackName} has been removed from {activeEnvironment?.name}
            </p>

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

  if (store.state === 'removing') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-red-600 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Removing Deployment...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Removing {store.deployment?.stackName} from {activeEnvironment?.name}
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
                    className="h-full bg-red-600 rounded-full transition-all duration-500 ease-out"
                    style={{ width: `${store.progressUpdate?.percentComplete ?? 0}%` }}
                  />
                </div>
              </div>

              {/* Status Message */}
              <div className="text-center">
                <p className="text-sm text-gray-700 dark:text-gray-300 font-medium">
                  {store.progressUpdate?.message || 'Starting removal...'}
                </p>

                {/* Container Progress */}
                {store.progressUpdate && store.progressUpdate.totalServices > 0 && (
                  <p className="text-xs text-gray-500 dark:text-gray-400 mt-2">
                    Containers: {store.progressUpdate.completedServices} / {store.progressUpdate.totalServices}
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
          to="/deployments"
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back to Deployments
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
            Remove Deployment
          </h1>
          <p className="text-gray-600 dark:text-gray-400 mb-2 text-center">
            Are you sure you want to remove <strong className="text-gray-900 dark:text-white">{store.deployment?.stackName}</strong>?
          </p>
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-6 text-center max-w-md">
            This will stop and remove all containers associated with this deployment.
            This action cannot be undone.
          </p>

          {/* Deployment Info */}
          <div className="w-full max-w-md mb-6 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg">
            <h3 className="text-sm font-medium text-gray-900 dark:text-white mb-3">Deployment Details</h3>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Stack Name:</span>
                <span className="font-medium text-gray-900 dark:text-white">{store.deployment?.stackName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Environment:</span>
                <span className="font-medium text-gray-900 dark:text-white">{activeEnvironment?.name}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600 dark:text-gray-400">Services:</span>
                <span className="font-medium text-gray-900 dark:text-white">{store.deployment?.services.length || 0}</span>
              </div>
            </div>
          </div>

          {/* Error Display */}
          {store.error && (
            <div className="w-full max-w-md mb-6 p-4 text-sm text-red-800 bg-red-100 rounded-lg dark:bg-red-900/30 dark:text-red-400">
              <p className="font-medium mb-1">Error</p>
              <p>{store.error}</p>
            </div>
          )}

          {/* Action Buttons */}
          <div className="flex gap-4">
            <button
              onClick={handleCancel}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-gray-100 px-6 py-3 text-center font-medium text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
            >
              Cancel
            </button>
            <button
              onClick={store.handleRemove}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-red-600 px-6 py-3 text-center font-medium text-white hover:bg-red-700"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
              Remove Deployment
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
