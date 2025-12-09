import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import { getDeployment, type GetDeploymentResponse, type DeployedServiceInfo } from "../api/deployments";
import { useEnvironment } from "../context/EnvironmentContext";
import { useHealthHub } from "../hooks/useHealthHub";
import {
  getHealthStatusPresentation,
  getOperationModePresentation,
  getStackHealth,
  enterMaintenanceMode,
  exitMaintenanceMode,
  type StackHealthSummaryDto,
  type StackHealthDto,
  type ServiceHealthDto
} from "../api/health";

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

  // SignalR Health Hub connection
  const { connectionState, subscribeToEnvironment, unsubscribeFromEnvironment } = useHealthHub({
    onDeploymentHealthChanged: (healthData) => {
      // Update health if it matches our deployment
      if (deployment?.deploymentId && healthData.deploymentId === deployment.deploymentId) {
        setHealth(healthData);
      }
    },
    onEnvironmentHealthChanged: (summary) => {
      // Find our deployment in the summary
      if (deployment?.deploymentId) {
        const stackHealth = summary.stacks.find(s => s.deploymentId === deployment.deploymentId);
        if (stackHealth) {
          setHealth(stackHealth);
        }
      }
    }
  });

  // Subscribe to environment when connected
  useEffect(() => {
    if (activeEnvironment && connectionState === 'connected') {
      subscribeToEnvironment(activeEnvironment.id);
      return () => {
        unsubscribeFromEnvironment(activeEnvironment.id);
      };
    }
  }, [activeEnvironment, connectionState, subscribeToEnvironment, unsubscribeFromEnvironment]);

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

          // Load detailed health data with service info
          if (response.deploymentId) {
            try {
              const healthData = await getStackHealth(activeEnvironment.id, response.deploymentId);
              setDetailedHealth(healthData);
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
      const response = await enterMaintenanceMode(deployment.deploymentId);
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
      const response = await exitMaintenanceMode(deployment.deploymentId);
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
