import { useEffect, useState, useRef, useCallback } from 'react';
import { useParams, Link, useNavigate } from 'react-router';
import {
  getDeployment,
  getRollbackInfo,
  rollbackDeployment,
  type GetDeploymentResponse,
  type RollbackInfoResponse
} from '../../api/deployments';
import { useEnvironment } from '../../context/EnvironmentContext';
import { useDeploymentHub, type DeploymentProgressUpdate } from '../../hooks/useDeploymentHub';

// Format phase names for display (PullingImages -> Pulling Images)
const formatPhase = (phase: string | undefined): string => {
  if (!phase) return '';
  return phase.replace(/([A-Z])/g, ' $1').trim();
};

type RollbackState = 'loading' | 'confirm' | 'rolling_back' | 'success' | 'error';

export default function RollbackStack() {
  const { stackName } = useParams<{ stackName: string }>();
  const navigate = useNavigate();
  const { activeEnvironment } = useEnvironment();

  const [state, setState] = useState<RollbackState>('loading');
  const [deployment, setDeployment] = useState<GetDeploymentResponse | null>(null);
  const [rollbackInfo, setRollbackInfo] = useState<RollbackInfoResponse | null>(null);
  const [error, setError] = useState('');

  // Rollback progress state
  const rollbackSessionIdRef = useRef<string | null>(null);
  const [progressUpdate, setProgressUpdate] = useState<DeploymentProgressUpdate | null>(null);

  // SignalR hub for real-time rollback progress
  const handleRollbackProgress = useCallback((update: DeploymentProgressUpdate) => {
    const currentSessionId = rollbackSessionIdRef.current;
    if (currentSessionId && update.sessionId === currentSessionId) {
      setProgressUpdate(update);

      if (update.isComplete) {
        if (update.isError) {
          setError(update.errorMessage || 'Rollback failed');
          setState('error');
        } else {
          setState('success');
        }
      }
    }
  }, []);

  const { subscribeToDeployment, connectionState } = useDeploymentHub({
    onDeploymentProgress: handleRollbackProgress,
  });

  // Load deployment and rollback info
  useEffect(() => {
    if (!stackName || !activeEnvironment) {
      return;
    }

    const loadData = async () => {
      try {
        setState('loading');
        setError('');

        // Load deployment
        const deploymentResponse = await getDeployment(activeEnvironment.id, decodeURIComponent(stackName));
        if (!deploymentResponse.success || !deploymentResponse.deploymentId) {
          setError(deploymentResponse.message || 'Deployment not found');
          setState('error');
          return;
        }
        setDeployment(deploymentResponse);

        // Load rollback info
        const rollbackInfoResponse = await getRollbackInfo(activeEnvironment.id, deploymentResponse.deploymentId);
        setRollbackInfo(rollbackInfoResponse);

        if (!rollbackInfoResponse.canRollback) {
          setError('Rollback is not available for this deployment. The deployment must be in a Failed state to rollback.');
          setState('error');
          return;
        }

        setState('confirm');
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load rollback data');
        setState('error');
      }
    };

    loadData();
  }, [stackName, activeEnvironment]);

  const handleRollback = async () => {
    if (!activeEnvironment || !deployment?.deploymentId) {
      setError('Missing required data');
      return;
    }

    // Generate session ID before API call
    const sessionId = `rollback-${deployment.stackName}-${Date.now()}`;
    rollbackSessionIdRef.current = sessionId;

    setState('rolling_back');
    setError('');
    setProgressUpdate(null);

    // Subscribe to SignalR before starting
    if (connectionState === 'connected') {
      await subscribeToDeployment(sessionId);
    }

    try {
      const response = await rollbackDeployment(activeEnvironment.id, deployment.deploymentId, { sessionId });

      if (!response.success) {
        setError(response.message || 'Rollback failed');
        setState('error');
        return;
      }

      // State will be set to 'success' by SignalR callback
      // But if no SignalR connection, set it immediately
      if (connectionState !== 'connected') {
        setState('success');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Rollback failed');
      setState('error');
    }
  };

  const getBackUrl = () => `/deployments/${encodeURIComponent(stackName || '')}`;

  if (state === 'loading') {
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

  if (state === 'error' && !rollbackInfo) {
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
              Rollback Successful!
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              {deployment?.stackName} has been rolled back to version {rollbackInfo?.rollbackTargetVersion}
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

  if (state === 'rolling_back') {
    return (
      <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
        <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="flex flex-col items-center py-8">
            <div className="w-16 h-16 mb-6 border-4 border-amber-600 border-t-transparent rounded-full animate-spin"></div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
              Rolling Back...
            </h1>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
              Rolling back {deployment?.stackName} to version {rollbackInfo?.rollbackTargetVersion}
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
                    className="h-full bg-amber-600 rounded-full transition-all duration-500 ease-out"
                    style={{ width: `${progressUpdate?.percentComplete ?? 0}%` }}
                  />
                </div>
              </div>

              {/* Status Message */}
              <div className="text-center">
                <p className="text-sm text-gray-700 dark:text-gray-300 font-medium">
                  {progressUpdate?.message || 'Starting rollback...'}
                </p>

                {progressUpdate && progressUpdate.totalServices > 0 && (
                  <p className="text-xs text-gray-500 dark:text-gray-400 mt-2">
                    {progressUpdate.phase === 'PullingImages' ? 'Images' : 'Services'}: {progressUpdate.completedServices} / {progressUpdate.totalServices}
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
          Rollback {deployment?.stackName}
        </h1>
        <p className="text-gray-600 dark:text-gray-400">
          Rollback to version <span className="font-medium">{rollbackInfo?.rollbackTargetVersion}</span>
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
                  This will restore <strong>{deployment?.stackName}</strong> to version{' '}
                  <strong>{rollbackInfo?.rollbackTargetVersion}</strong>.
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
          {rollbackInfo?.snapshotDescription && (
            <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Snapshot Details
              </h2>
              <div className="space-y-3">
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500 dark:text-gray-400">Description</span>
                  <span className="font-medium text-gray-900 dark:text-white">
                    {rollbackInfo.snapshotDescription}
                  </span>
                </div>
                {rollbackInfo.snapshotCreatedAt && (
                  <div className="flex justify-between text-sm">
                    <span className="text-gray-500 dark:text-gray-400">Created At</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {new Date(rollbackInfo.snapshotCreatedAt).toLocaleString()}
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
              onClick={handleRollback}
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
                  {rollbackInfo?.currentVersion || 'Unknown'}
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500 dark:text-gray-400">Rollback To</span>
                <span className="font-medium text-amber-600 dark:text-amber-400">
                  {rollbackInfo?.rollbackTargetVersion}
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
