import { useEffect, useState, useCallback } from "react";
import { useParams, Link } from "react-router";
import {
  getDeployment,
  getRollbackInfo,
  checkUpgrade,
  markDeploymentFailed,
  type GetDeploymentResponse,
  type DeployedServiceInfo,
  type InitContainerResultDto,
  type RollbackInfoResponse,
  type CheckUpgradeResponse
} from "../../api/deployments";
import { useEnvironment } from "../../context/EnvironmentContext";
import { useHealthHub } from "../../hooks/useHealthHub";
import {
  getHealthStatusPresentation,
  getOperationModePresentation,
  getStackHealth,
  enterMaintenanceMode,
  exitMaintenanceMode,
  type StackHealthSummaryDto,
  type StackHealthDto,
  type ServiceHealthDto
} from "../../api/health";
import HealthHistoryChart from "../../components/health/HealthHistoryChart";

export default function DeploymentDetail() {
  const { stackName } = useParams<{ stackName: string }>();
  const { activeEnvironment } = useEnvironment();
  const [deployment, setDeployment] = useState<GetDeploymentResponse | null>(null);
  const [health, setHealth] = useState<StackHealthSummaryDto | null>(null);
  const [detailedHealth, setDetailedHealth] = useState<StackHealthDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modeActionLoading, setModeActionLoading] = useState(false);
  const [modeActionError, setModeActionError] = useState<string | null>(null);

  // Rollback state
  const [rollbackInfo, setRollbackInfo] = useState<RollbackInfoResponse | null>(null);

  // Upgrade state (simplified - just info, navigation handled via Link)
  const [upgradeInfo, setUpgradeInfo] = useState<CheckUpgradeResponse | null>(null);

  // Mark as Failed state (for stuck deployments)
  const [markFailedLoading, setMarkFailedLoading] = useState(false);
  const [markFailedError, setMarkFailedError] = useState<string | null>(null);

  // SignalR Health Hub connection - subscribe to deployment for detailed health updates
  const { connectionState, subscribeToDeployment, unsubscribeFromDeployment } = useHealthHub({
    onDeploymentDetailedHealthChanged: (healthData) => {
      // Update detailed health if it matches our deployment
      if (deployment?.deploymentId && healthData.deploymentId === deployment.deploymentId) {
        setDetailedHealth(healthData);
        // Also update summary from detailed data
        setHealth({
          deploymentId: healthData.deploymentId,
          stackName: healthData.stackName,
          currentVersion: healthData.currentVersion,
          overallStatus: healthData.overallStatus,
          operationMode: healthData.operationMode,
          healthyServices: healthData.self.healthyCount,
          totalServices: healthData.self.totalCount,
          statusMessage: healthData.statusMessage,
          requiresAttention: healthData.requiresAttention,
          capturedAtUtc: healthData.capturedAtUtc
        });
      }
    }
  });

  // Subscribe to deployment when connected and deployment is loaded
  useEffect(() => {
    const deploymentId = deployment?.deploymentId;
    if (deploymentId && connectionState === 'connected') {
      subscribeToDeployment(deploymentId);
      return () => {
        unsubscribeFromDeployment(deploymentId);
      };
    }
  }, [deployment?.deploymentId, connectionState, subscribeToDeployment, unsubscribeFromDeployment]);

  useEffect(() => {
    const loadDeployment = async () => {
      if (!activeEnvironment || !stackName) {
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        setError(null);
        const response = await getDeployment(activeEnvironment.id, decodeURIComponent(stackName));
        if (response.success) {
          setDeployment(response);

          // Load detailed health data with service info (force refresh for immediate data)
          if (response.deploymentId) {
            try {
              const healthData = await getStackHealth(activeEnvironment.id, response.deploymentId, true);
              setDetailedHealth(healthData);
              // Also set the health summary from detailed data
              setHealth({
                deploymentId: healthData.deploymentId,
                stackName: healthData.stackName,
                currentVersion: healthData.currentVersion,
                overallStatus: healthData.overallStatus,
                operationMode: healthData.operationMode,
                healthyServices: healthData.self.healthyCount,
                totalServices: healthData.self.totalCount,
                statusMessage: healthData.statusMessage,
                requiresAttention: healthData.requiresAttention,
                capturedAtUtc: healthData.capturedAtUtc
              });
            } catch (healthErr) {
              console.warn("Could not load health data:", healthErr);
            }
          }
        } else {
          setError(response.message || "Deployment not found");
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load deployment");
      } finally {
        setLoading(false);
      }
    };

    loadDeployment();
  }, [activeEnvironment, stackName]);

  // Load rollback info when deployment is loaded
  useEffect(() => {
    const loadRollbackInfo = async () => {
      if (!activeEnvironment || !deployment?.deploymentId) return;

      try {
        const info = await getRollbackInfo(activeEnvironment.id, deployment.deploymentId);
        setRollbackInfo(info);
      } catch (err) {
        console.warn("Could not load rollback info:", err);
      }
    };

    loadRollbackInfo();
  }, [activeEnvironment, deployment?.deploymentId]);

  // Load upgrade info when deployment is loaded
  useEffect(() => {
    const loadUpgradeInfo = async () => {
      if (!activeEnvironment || !deployment?.deploymentId) return;

      try {
        const info = await checkUpgrade(activeEnvironment.id, deployment.deploymentId);
        setUpgradeInfo(info);
      } catch (err) {
        console.warn("Could not load upgrade info:", err);
      }
    };

    loadUpgradeInfo();
  }, [activeEnvironment, deployment?.deploymentId]);

  const formatDate = (dateString?: string) => {
    if (!dateString) return "-";
    const date = new Date(dateString);
    return date.toLocaleDateString() + " " + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  const handleEnterMaintenance = async () => {
    if (!deployment?.deploymentId || !activeEnvironment) return;
    try {
      setModeActionLoading(true);
      setModeActionError(null);
      const response = await enterMaintenanceMode(activeEnvironment.id, deployment.deploymentId);
      if (!response.success) {
        setModeActionError(response.message || 'Failed to enter maintenance mode');
      } else {
        // Optimistically update local state
        if (health) {
          setHealth({ ...health, operationMode: 'Maintenance' });
        }
        // Also refresh health data from server to get full updated state
        try {
          const healthData = await getStackHealth(activeEnvironment.id, deployment.deploymentId, true);
          setDetailedHealth(healthData);
          setHealth({
            deploymentId: healthData.deploymentId,
            stackName: healthData.stackName,
            currentVersion: healthData.currentVersion,
            overallStatus: healthData.overallStatus,
            operationMode: healthData.operationMode,
            healthyServices: healthData.self.healthyCount,
            totalServices: healthData.self.totalCount,
            statusMessage: healthData.statusMessage,
            requiresAttention: healthData.requiresAttention,
            capturedAtUtc: healthData.capturedAtUtc
          });
        } catch {
          // Ignore refresh errors, optimistic update is enough
        }
      }
    } catch (err) {
      setModeActionError(err instanceof Error ? err.message : 'Failed to enter maintenance mode');
    } finally {
      setModeActionLoading(false);
    }
  };

  const handleExitMaintenance = async () => {
    if (!deployment?.deploymentId || !activeEnvironment) return;
    try {
      setModeActionLoading(true);
      setModeActionError(null);
      const response = await exitMaintenanceMode(activeEnvironment.id, deployment.deploymentId);
      if (!response.success) {
        setModeActionError(response.message || 'Failed to exit maintenance mode');
      } else {
        // Optimistically update local state
        if (health) {
          setHealth({ ...health, operationMode: 'Normal' });
        }
        // Also refresh health data from server to get full updated state
        try {
          const healthData = await getStackHealth(activeEnvironment.id, deployment.deploymentId, true);
          setDetailedHealth(healthData);
          setHealth({
            deploymentId: healthData.deploymentId,
            stackName: healthData.stackName,
            currentVersion: healthData.currentVersion,
            overallStatus: healthData.overallStatus,
            operationMode: healthData.operationMode,
            healthyServices: healthData.self.healthyCount,
            totalServices: healthData.self.totalCount,
            statusMessage: healthData.statusMessage,
            requiresAttention: healthData.requiresAttention,
            capturedAtUtc: healthData.capturedAtUtc
          });
        } catch {
          // Ignore refresh errors, optimistic update is enough
        }
      }
    } catch (err) {
      setModeActionError(err instanceof Error ? err.message : 'Failed to exit maintenance mode');
    } finally {
      setModeActionLoading(false);
    }
  };

  const handleMarkAsFailed = async () => {
    if (!deployment?.deploymentId || !activeEnvironment) return;
    try {
      setMarkFailedLoading(true);
      setMarkFailedError(null);
      const response = await markDeploymentFailed(
        activeEnvironment.id,
        deployment.deploymentId,
        "Manually marked as failed by user"
      );
      if (!response.success) {
        setMarkFailedError(response.message || 'Failed to mark deployment as failed');
      } else {
        // Refresh the deployment data to get updated status
        const refreshed = await getDeployment(activeEnvironment.id, decodeURIComponent(stackName || ''));
        if (refreshed.success) {
          setDeployment(refreshed);
        }
        // Also reload rollback info since status changed
        if (deployment.deploymentId) {
          try {
            const info = await getRollbackInfo(activeEnvironment.id, deployment.deploymentId);
            setRollbackInfo(info);
          } catch {
            // Ignore
          }
        }
      }
    } catch (err) {
      setMarkFailedError(err instanceof Error ? err.message : 'Failed to mark deployment as failed');
    } finally {
      setMarkFailedLoading(false);
    }
  };

  const statusPresentation = health
    ? getHealthStatusPresentation(health.overallStatus)
    : getHealthStatusPresentation('unknown');

  const modePresentation = health
    ? getOperationModePresentation(health.operationMode)
    : null;

  if (loading) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="animate-pulse">
          <div className="h-8 bg-gray-200 dark:bg-gray-700 rounded w-1/3 mb-4" />
          <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded w-1/2 mb-8" />
          <div className="space-y-4">
            <div className="h-32 bg-gray-200 dark:bg-gray-700 rounded" />
            <div className="h-48 bg-gray-200 dark:bg-gray-700 rounded" />
          </div>
        </div>
      </div>
    );
  }

  if (error || !deployment) {
    return (
      <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
        <div className="mb-6">
          <Link
            to="/deployments"
            className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back to Deployments
          </Link>
        </div>
        <div className="rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <p className="text-sm text-red-800 dark:text-red-200">
            {error || "Deployment not found"}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <div className="mb-6">
        <Link
          to="/deployments"
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back to Deployments
        </Link>
      </div>

      {/* Header */}
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-title-md2 font-semibold text-black dark:text-white">
              {deployment.stackName}
            </h1>
            {/* Health Status Badge */}
            <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ${statusPresentation.bgColor} ${statusPresentation.textColor}`}>
              {health?.requiresAttention && (
                <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
                </svg>
              )}
              {statusPresentation.label}
            </span>
            {/* Operation Mode Badge (if not Normal) */}
            {modePresentation && health?.operationMode !== 'Normal' && (
              <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${modePresentation.bgColor} ${modePresentation.textColor}`}>
                {modePresentation.label}
              </span>
            )}
          </div>
          <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">
            Deployed {formatDate(deployment.deployedAt)} in {activeEnvironment?.name}
          </p>
        </div>
        <div className="flex items-center gap-3">
          {connectionState === 'connected' && (
            <span className="inline-flex items-center gap-1.5 text-xs text-green-600 dark:text-green-400">
              <span className="h-2 w-2 rounded-full bg-green-500 animate-pulse"></span>
              Live
            </span>
          )}
          {/* Operation Mode Controls */}
          {deployment?.deploymentId && health && (
            <>
              {health.operationMode === 'Normal' && (
                <button
                  onClick={handleEnterMaintenance}
                  disabled={modeActionLoading}
                  className="inline-flex items-center justify-center gap-2 rounded-md bg-yellow-100 px-4 py-2 text-sm font-medium text-yellow-800 hover:bg-yellow-200 dark:bg-yellow-900/30 dark:text-yellow-400 dark:hover:bg-yellow-900/50 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                  </svg>
                  {modeActionLoading ? 'Entering...' : 'Enter Maintenance'}
                </button>
              )}
              {health.operationMode === 'Maintenance' && (
                <button
                  onClick={handleExitMaintenance}
                  disabled={modeActionLoading}
                  className="inline-flex items-center justify-center gap-2 rounded-md bg-green-100 px-4 py-2 text-sm font-medium text-green-800 hover:bg-green-200 dark:bg-green-900/30 dark:text-green-400 dark:hover:bg-green-900/50 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z" />
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  {modeActionLoading ? 'Exiting...' : 'Exit Maintenance'}
                </button>
              )}
            </>
          )}
        </div>
      </div>

      {/* Operation Mode Error */}
      {modeActionError && (
        <div className="mb-6 rounded-md bg-red-50 p-4 dark:bg-red-900/20">
          <div className="flex">
            <div className="flex-shrink-0">
              <svg className="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
              </svg>
            </div>
            <div className="ml-3">
              <p className="text-sm text-red-800 dark:text-red-200">{modeActionError}</p>
            </div>
            <div className="ml-auto pl-3">
              <button
                onClick={() => setModeActionError(null)}
                className="inline-flex rounded-md p-1.5 text-red-500 hover:bg-red-100 dark:hover:bg-red-900/50"
              >
                <span className="sr-only">Dismiss</span>
                <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                  <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
                </svg>
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Stuck Deployment Panel - shown when deployment is in Installing or Upgrading status */}
      {(deployment?.status === 'Installing' || deployment?.status === 'Upgrading') && (
        <div className="mb-6 rounded-2xl border border-orange-200 bg-orange-50 p-6 dark:border-orange-800 dark:bg-orange-900/20">
          <div className="flex items-start justify-between">
            <div>
              <h4 className="text-lg font-semibold text-orange-900 dark:text-orange-100 flex items-center gap-2">
                <svg className="w-5 h-5 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                {deployment.status === 'Installing' ? 'Installation in Progress' : 'Upgrade in Progress'}
              </h4>
              <p className="mt-1 text-sm text-orange-800 dark:text-orange-200">
                This deployment appears to be stuck in <strong>{deployment.status}</strong> status.
                If the operation has stalled or the application was restarted during the operation,
                you can manually mark it as failed to enable recovery options.
              </p>
              {markFailedError && (
                <p className="mt-2 text-sm text-red-600 dark:text-red-400">
                  {markFailedError}
                </p>
              )}
            </div>
            <button
              onClick={handleMarkAsFailed}
              disabled={markFailedLoading}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-orange-600 px-4 py-2 text-sm font-medium text-white hover:bg-orange-700 dark:bg-orange-700 dark:hover:bg-orange-600 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
              {markFailedLoading ? 'Marking...' : 'Mark as Failed'}
            </button>
          </div>
        </div>
      )}

      {/* Rollback Panel - only shown when canRollback is true */}
      {rollbackInfo?.canRollback && (
        <div className="mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-6 dark:border-amber-800 dark:bg-amber-900/20">
          <div className="flex items-start justify-between">
            <div>
              <h4 className="text-lg font-semibold text-amber-900 dark:text-amber-100 flex items-center gap-2">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
                Rollback Available
              </h4>
              <p className="mt-1 text-sm text-amber-800 dark:text-amber-200">
                The upgrade failed. You can rollback to version <strong>{rollbackInfo.rollbackTargetVersion}</strong>
                {rollbackInfo.snapshotDescription && (
                  <span className="block mt-1 text-amber-700 dark:text-amber-300">
                    {rollbackInfo.snapshotDescription}
                  </span>
                )}
              </p>
              {rollbackInfo.snapshotCreatedAt && (
                <p className="mt-2 text-xs text-amber-600 dark:text-amber-400">
                  Snapshot created: {formatDate(rollbackInfo.snapshotCreatedAt)}
                </p>
              )}
            </div>
            <Link
              to={`/deployments/${encodeURIComponent(stackName || '')}/rollback`}
              className="inline-flex items-center justify-center gap-2 rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700 dark:bg-amber-700 dark:hover:bg-amber-600"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
              </svg>
              Rollback
            </Link>
          </div>
        </div>
      )}

      {/* Upgrade Panel - navigates to upgrade page */}
      {upgradeInfo?.upgradeAvailable && upgradeInfo.canUpgrade && (
        <div className="mb-6 rounded-2xl border border-blue-200 bg-blue-50 p-6 dark:border-blue-800 dark:bg-blue-900/20">
          <div className="flex items-start justify-between">
            <div className="flex-1">
              <h4 className="text-lg font-semibold text-blue-900 dark:text-blue-100 flex items-center gap-2">
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                Upgrade Available
              </h4>
              <p className="mt-1 text-sm text-blue-800 dark:text-blue-200">
                Version <strong>{upgradeInfo.currentVersion}</strong> &rarr; <strong>{upgradeInfo.latestVersion}</strong>
                {(upgradeInfo.availableVersions?.length ?? 0) > 1 && (
                  <span className="ml-2 text-blue-600 dark:text-blue-400">
                    ({upgradeInfo.availableVersions?.length} versions available)
                  </span>
                )}
              </p>
            </div>
            <div className="flex flex-col items-end gap-2 ml-4">
              <Link
                to={`/deployments/${encodeURIComponent(stackName || '')}/upgrade`}
                className="inline-flex items-center justify-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 dark:bg-blue-700 dark:hover:bg-blue-600"
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16V4m0 0L3 8m4-4l4 4m6 0v12m0 0l4-4m-4 4l-4-4" />
                </svg>
                Upgrade
              </Link>
            </div>
          </div>
        </div>
      )}

      {/* Upgrade not available message - show reason if can't upgrade */}
      {upgradeInfo?.upgradeAvailable && !upgradeInfo.canUpgrade && upgradeInfo.cannotUpgradeReason && (
        <div className="mb-6 rounded-2xl border border-gray-200 bg-gray-50 p-6 dark:border-gray-700 dark:bg-gray-800/50">
          <div className="flex items-start gap-3">
            <svg className="w-5 h-5 text-gray-400 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <div>
              <h4 className="text-sm font-semibold text-gray-900 dark:text-gray-100">
                Upgrade Available ({upgradeInfo.currentVersion} &rarr; {upgradeInfo.latestVersion})
              </h4>
              <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">
                {upgradeInfo.cannotUpgradeReason}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Health Summary Card */}
      {health && (
        <div className="mb-6 rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
          <h4 className="text-lg font-semibold text-black dark:text-white mb-4">
            Health Status
          </h4>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="text-center p-4 rounded-lg bg-gray-50 dark:bg-gray-800/50">
              <div className="text-2xl font-bold text-gray-900 dark:text-white">
                {health.healthyServices}/{health.totalServices}
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-400">Services Healthy</div>
            </div>
            <div className="text-center p-4 rounded-lg bg-gray-50 dark:bg-gray-800/50">
              <div className="text-2xl font-bold text-gray-900 dark:text-white">
                {health.operationMode}
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-400">Operation Mode</div>
            </div>
            <div className="text-center p-4 rounded-lg bg-gray-50 dark:bg-gray-800/50">
              <div className="text-sm font-medium text-gray-900 dark:text-white">
                {health.statusMessage}
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-400">Status</div>
            </div>
          </div>
          {health.currentVersion && (
            <div className="mt-4 text-sm text-gray-600 dark:text-gray-400">
              Version: {health.currentVersion}
            </div>
          )}
        </div>
      )}

      {/* Health History Chart */}
      {deployment?.deploymentId && (
        <HealthHistoryChart
          deploymentId={deployment.deploymentId}
          className="mb-6"
        />
      )}

      {/* Init Container Results */}
      {deployment.initContainerResults && deployment.initContainerResults.length > 0 && (
        <InitContainerResultsPanel
          results={deployment.initContainerResults}
          formatDate={formatDate}
        />
      )}

      {/* Services */}
      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="px-4 py-6 md:px-6 xl:px-7.5">
          <h4 className="text-xl font-semibold text-black dark:text-white">
            Services ({detailedHealth?.self.services.length ?? deployment.services.length})
          </h4>
        </div>

        <div className="border-t border-stroke dark:border-strokedark">
          {(detailedHealth?.self.services.length ?? deployment.services.length) === 0 ? (
            <div className="px-4 py-8 text-center text-sm text-gray-600 dark:text-gray-400">
              No services found
            </div>
          ) : detailedHealth ? (
            <div className="divide-y divide-gray-100 dark:divide-gray-800">
              {detailedHealth.self.services.map((service) => (
                <ServiceHealthRow key={service.name} service={service} />
              ))}
            </div>
          ) : (
            <div className="divide-y divide-gray-100 dark:divide-gray-800">
              {deployment.services.map((service) => (
                <ServiceRow key={service.serviceName} service={service} />
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Configuration */}
      {deployment.configuration && Object.keys(deployment.configuration).length > 0 && (
        <div className="mt-6 rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
          <div className="px-4 py-6 md:px-6 xl:px-7.5">
            <h4 className="text-xl font-semibold text-black dark:text-white">
              Configuration
            </h4>
          </div>
          <div className="border-t border-stroke dark:border-strokedark p-4 md:p-6">
            <div className="grid gap-3 md:grid-cols-2">
              {Object.entries(deployment.configuration).map(([key, value]) => (
                <div key={key} className="rounded-lg bg-gray-50 p-3 dark:bg-gray-800/50">
                  <div className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                    {key}
                  </div>
                  <div className="mt-1 text-sm font-mono text-gray-900 dark:text-white break-all">
                    {value || <span className="text-gray-400 italic">not set</span>}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

interface ServiceHealthRowProps {
  service: ServiceHealthDto;
}

/**
 * Service row showing combined health status (like Portainer).
 * Shows "healthy" instead of "running" when health check passes.
 */
function ServiceHealthRow({ service }: ServiceHealthRowProps) {
  const getStatusDisplay = (status: string) => {
    // Map health status to display text (like Portainer)
    switch (status.toLowerCase()) {
      case 'healthy':
        return { label: 'healthy', color: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400' };
      case 'unhealthy':
        return { label: 'unhealthy', color: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400' };
      case 'degraded':
        return { label: 'starting', color: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400' };
      case 'unknown':
      default:
        return { label: 'unknown', color: 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300' };
    }
  };

  const statusDisplay = getStatusDisplay(service.status);

  return (
    <div className="px-4 py-4 md:px-6 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
      <div className="flex items-center justify-between gap-4">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-3">
            <h5 className="font-medium text-gray-900 dark:text-white">
              {service.name}
            </h5>
            <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${statusDisplay.color}`}>
              {statusDisplay.label}
            </span>
          </div>
          <div className="mt-1 flex items-center gap-3">
            {service.containerId && (
              <span className="text-xs text-gray-500 dark:text-gray-400 font-mono">
                {service.containerId.substring(0, 12)}
              </span>
            )}
            {service.containerName && (
              <span className="text-xs text-gray-500 dark:text-gray-400">
                {service.containerName}
              </span>
            )}
          </div>
          {service.reason && (
            <div className="mt-1 text-xs text-amber-600 dark:text-amber-400">
              {service.reason}
            </div>
          )}
          {service.restartCount > 0 && (
            <div className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              Restarts: {service.restartCount}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

interface InitContainerResultsPanelProps {
  results: InitContainerResultDto[];
  formatDate: (dateString?: string) => string;
}

function InitContainerResultsPanel({ results, formatDate }: InitContainerResultsPanelProps) {
  const [expandedLogs, setExpandedLogs] = useState<Record<string, boolean>>({});

  const toggleLog = useCallback((serviceName: string) => {
    setExpandedLogs(prev => ({
      ...prev,
      [serviceName]: !prev[serviceName]
    }));
  }, []);

  return (
    <div className="mb-6 rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
      <div className="px-4 py-6 md:px-6 xl:px-7.5">
        <h4 className="text-xl font-semibold text-black dark:text-white">
          Init Containers ({results.length})
        </h4>
      </div>
      <div className="border-t border-stroke dark:border-strokedark divide-y divide-gray-100 dark:divide-gray-800">
        {results.map((result) => (
          <div key={result.serviceName}>
            <div
              className={`px-4 py-4 md:px-6 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors ${result.logOutput ? 'cursor-pointer' : ''}`}
              onClick={() => result.logOutput && toggleLog(result.serviceName)}
            >
              <div className="flex items-center justify-between gap-4">
                <div className="flex items-center gap-3">
                  {result.success ? (
                    <svg className="w-5 h-5 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                    </svg>
                  ) : (
                    <svg className="w-5 h-5 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                    </svg>
                  )}
                  <h5 className="font-medium text-gray-900 dark:text-white">
                    {result.serviceName}
                  </h5>
                  <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                    result.success
                      ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
                      : 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
                  }`}>
                    {result.success ? 'completed' : `failed (exit ${result.exitCode})`}
                  </span>
                  {result.logOutput && (
                    <span className="inline-flex items-center gap-1 text-xs text-gray-500 dark:text-gray-400">
                      <svg className={`w-3.5 h-3.5 transition-transform ${expandedLogs[result.serviceName] ? 'rotate-90' : ''}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                      </svg>
                      Logs
                    </span>
                  )}
                </div>
                <span className="text-xs text-gray-500 dark:text-gray-400">
                  {formatDate(result.executedAtUtc)}
                </span>
              </div>
            </div>
            {/* Log output panel */}
            {result.logOutput && expandedLogs[result.serviceName] && (
              <div className="px-4 pb-4 md:px-6">
                <div className="bg-gray-900 rounded-lg p-3 max-h-80 overflow-y-auto">
                  {result.logOutput.split('\n').map((line, i) => (
                    <div key={i} className="font-mono text-xs text-green-400 whitespace-pre-wrap break-all leading-relaxed">{line}</div>
                  ))}
                </div>
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

interface ServiceRowProps {
  service: DeployedServiceInfo;
}

/**
 * Fallback service row using deployment data (before health data is loaded).
 */
function ServiceRow({ service }: ServiceRowProps) {
  const getStatusColor = (status?: string) => {
    switch (status?.toLowerCase()) {
      case 'running':
        return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400';
      case 'stopped':
      case 'exited':
        return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400';
      case 'starting':
      case 'restarting':
        return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400';
      default:
        return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300';
    }
  };

  return (
    <div className="px-4 py-4 md:px-6 hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors">
      <div className="flex items-center justify-between gap-4">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-3">
            <h5 className="font-medium text-gray-900 dark:text-white">
              {service.serviceName}
            </h5>
            <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${getStatusColor(service.status)}`}>
              {service.status || 'Unknown'}
            </span>
          </div>
          {service.containerId && (
            <div className="mt-1 text-xs text-gray-500 dark:text-gray-400 font-mono">
              {service.containerId.substring(0, 12)}
            </div>
          )}
        </div>
        {service.ports.length > 0 && (
          <div className="flex flex-wrap gap-1">
            {service.ports.map((port, index) => (
              <span
                key={index}
                className="inline-flex items-center rounded bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-800 dark:bg-blue-900/30 dark:text-blue-400"
              >
                {port}
              </span>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
