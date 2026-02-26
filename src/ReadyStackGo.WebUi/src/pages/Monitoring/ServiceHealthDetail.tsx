import { useState, useEffect, useCallback } from 'react';
import { useParams, Link } from 'react-router';
import { useEnvironment } from '../../context/EnvironmentContext';
import {
  type ServiceHealthDetailResult,
  type HealthCheckEntryDto,
  getServiceHealth,
  getHealthStatusPresentation,
} from '../../api/health';

export default function ServiceHealthDetail() {
  const { deploymentId, serviceName } = useParams<{
    deploymentId: string;
    serviceName: string;
  }>();
  const { activeEnvironment } = useEnvironment();
  const [result, setResult] = useState<ServiceHealthDetailResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(
    async (forceRefresh = false) => {
      if (!activeEnvironment || !deploymentId || !serviceName) return;

      try {
        setLoading(true);
        setError(null);
        const data = await getServiceHealth(
          activeEnvironment.id,
          deploymentId,
          decodeURIComponent(serviceName),
          forceRefresh
        );
        setResult(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load service health');
      } finally {
        setLoading(false);
      }
    },
    [activeEnvironment, deploymentId, serviceName]
  );

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Auto-refresh every 30 seconds
  useEffect(() => {
    const interval = setInterval(() => loadData(), 30000);
    return () => clearInterval(interval);
  }, [loadData]);

  if (loading && !result) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-t-2 border-b-2 border-brand-500" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-xl border border-red-200 bg-red-50 dark:border-red-800 dark:bg-red-900/20 p-6">
        <h2 className="text-lg font-semibold text-red-800 dark:text-red-200">Error</h2>
        <p className="mt-2 text-red-700 dark:text-red-300">{error}</p>
        <Link
          to="/health"
          className="mt-4 inline-block text-sm font-medium text-brand-500 hover:text-brand-600"
        >
          Back to Health Dashboard
        </Link>
      </div>
    );
  }

  if (!result) return null;

  const { service, stackName, capturedAtUtc } = result;
  const presentation = getHealthStatusPresentation(service.status);
  const hasEntries = service.healthCheckEntries && service.healthCheckEntries.length > 0;

  return (
    <div className="space-y-6">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
        <Link to="/health" className="hover:text-brand-500">
          Health
        </Link>
        <span>/</span>
        <span className="text-gray-700 dark:text-gray-300">{stackName}</span>
        <span>/</span>
        <span className="text-gray-900 dark:text-white font-medium">
          {decodeURIComponent(serviceName || '')}
        </span>
      </nav>

      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <span
            className={`h-4 w-4 rounded-full ${
              service.status.toLowerCase() === 'healthy'
                ? 'bg-green-500'
                : service.status.toLowerCase() === 'degraded'
                ? 'bg-yellow-500'
                : service.status.toLowerCase() === 'unhealthy'
                ? 'bg-red-500'
                : 'bg-gray-400'
            }`}
          />
          <div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
              {service.name}
            </h1>
            {service.containerName && (
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Container: {service.containerName}
              </p>
            )}
          </div>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => loadData(true)}
            className="px-3 py-1.5 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
          >
            Refresh
          </button>
          <span
            className={`inline-flex items-center rounded-full px-3 py-1 text-sm font-medium ${presentation.bgColor} ${presentation.textColor}`}
          >
            {presentation.label}
          </span>
        </div>
      </div>

      {/* Info Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <InfoCard label="Status" value={service.status} />
        <InfoCard
          label="Response Time"
          value={service.responseTimeMs != null ? `${service.responseTimeMs}ms` : 'N/A'}
        />
        <InfoCard
          label="Restart Count"
          value={service.restartCount > 0 ? String(service.restartCount) : '0'}
        />
        <InfoCard
          label="Last Check"
          value={formatRelativeTime(capturedAtUtc)}
        />
      </div>

      {/* Reason */}
      {service.reason && (
        <div className="rounded-xl border border-yellow-200 bg-yellow-50 dark:border-yellow-800 dark:bg-yellow-900/20 p-4">
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            <span className="font-medium">Reason:</span> {service.reason}
          </p>
        </div>
      )}

      {/* Health Check Entries */}
      {hasEntries && (
        <div className="rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden">
          <div className="px-4 py-3 border-b border-gray-200 dark:border-gray-800">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">
              Health Check Entries
            </h2>
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
              {service.healthCheckEntries!.length} check{service.healthCheckEntries!.length !== 1 ? 's' : ''} reported
            </p>
          </div>
          <div className="divide-y divide-gray-200 dark:divide-gray-800">
            {service.healthCheckEntries!.map((entry) => (
              <HealthCheckEntryCard key={entry.name} entry={entry} />
            ))}
          </div>
        </div>
      )}

      {!hasEntries && (
        <div className="rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] p-6 text-center">
          <p className="text-sm text-gray-500 dark:text-gray-400">
            No health check entries available for this service.
          </p>
          <p className="text-xs text-gray-400 dark:text-gray-500 mt-1">
            Health check entries are only available when the service exposes an ASP.NET Core HealthReport endpoint.
          </p>
        </div>
      )}
    </div>
  );
}

function InfoCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] p-4">
      <p className="text-xs text-gray-500 dark:text-gray-400">{label}</p>
      <p className="mt-1 text-lg font-semibold text-gray-900 dark:text-white">{value}</p>
    </div>
  );
}

function HealthCheckEntryCard({ entry }: { entry: HealthCheckEntryDto }) {
  const presentation = getHealthStatusPresentation(entry.status);
  const hasData = entry.data && Object.keys(entry.data).length > 0;

  return (
    <div className="p-4 space-y-3">
      {/* Entry header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <span
            className={`h-2.5 w-2.5 rounded-full ${
              entry.status.toLowerCase() === 'healthy'
                ? 'bg-green-500'
                : entry.status.toLowerCase() === 'degraded'
                ? 'bg-yellow-500'
                : entry.status.toLowerCase() === 'unhealthy'
                ? 'bg-red-500'
                : 'bg-gray-400'
            }`}
          />
          <span className="font-medium text-gray-900 dark:text-white">{entry.name}</span>
          {entry.tags && entry.tags.length > 0 && (
            <div className="flex gap-1">
              {entry.tags.map((tag) => (
                <span
                  key={tag}
                  className="inline-flex items-center rounded px-1.5 py-0.5 text-[10px] font-medium bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400"
                >
                  {tag}
                </span>
              ))}
            </div>
          )}
        </div>
        <div className="flex items-center gap-3">
          {entry.durationMs != null && (
            <span className="text-xs text-gray-500 dark:text-gray-400 tabular-nums">
              {entry.durationMs < 1
                ? '<1ms'
                : entry.durationMs < 1000
                ? `${Math.round(entry.durationMs)}ms`
                : `${(entry.durationMs / 1000).toFixed(1)}s`}
            </span>
          )}
          <span
            className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${presentation.bgColor} ${presentation.textColor}`}
          >
            {presentation.label}
          </span>
        </div>
      </div>

      {/* Description */}
      {entry.description && (
        <p className="text-sm text-gray-600 dark:text-gray-400 ml-5.5">
          {entry.description}
        </p>
      )}

      {/* Data table */}
      {hasData && (
        <div className="ml-5.5 rounded-lg bg-gray-50 dark:bg-gray-800/50 p-3">
          <table className="w-full text-sm">
            <tbody>
              {Object.entries(entry.data!).map(([key, value]) => (
                <tr key={key} className="border-b border-gray-200 dark:border-gray-700 last:border-0">
                  <td className="pr-4 py-1.5 font-medium text-gray-500 dark:text-gray-400 whitespace-nowrap align-top">
                    {key}
                  </td>
                  <td className="py-1.5 text-gray-900 dark:text-gray-200 break-all">
                    {value}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Exception */}
      {entry.exception && (
        <pre className="ml-5.5 rounded-lg bg-red-50 dark:bg-red-900/20 p-3 text-sm text-red-700 dark:text-red-400 overflow-x-auto whitespace-pre-wrap font-mono">
          {entry.exception}
        </pre>
      )}
    </div>
  );
}

function formatRelativeTime(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffSec = Math.floor(diffMs / 1000);
  const diffMin = Math.floor(diffSec / 60);
  const diffHour = Math.floor(diffMin / 60);

  if (diffSec < 60) return `${diffSec}s ago`;
  if (diffMin < 60) return `${diffMin}m ago`;
  if (diffHour < 24) return `${diffHour}h ago`;
  return date.toLocaleDateString();
}
