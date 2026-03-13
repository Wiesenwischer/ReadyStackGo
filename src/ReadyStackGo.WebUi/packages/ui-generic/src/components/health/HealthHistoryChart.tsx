import {
  XAxis,
  Tooltip,
  ResponsiveContainer,
  ReferenceArea,
  ComposedChart,
  Line,
} from 'recharts';
import { useHealthTransitionsStore } from '@rsgo/core';
import type { HealthTransitionDto } from '@rsgo/core';
import UptimeDonutChart from './UptimeDonutChart';
import ServiceHealthTimeline from './ServiceHealthTimeline';

interface HealthHistoryChartProps {
  deploymentId: string;
  className?: string;
}

interface BandDataPoint {
  timestamp: number;
  value: number;
  status: string;
  operationMode: string;
  services: Array<{ name: string; status: string }>;
  healthyServices: number;
  totalServices: number;
}

const STATUS_COLORS: Record<string, string> = {
  healthy: '#22c55e',
  degraded: '#eab308',
  unhealthy: '#ef4444',
};

const MAINTENANCE_COLOR = '#3b82f6';

function getEffectiveColor(status: string, operationMode: string): string {
  if (operationMode?.toLowerCase() === 'maintenance') return MAINTENANCE_COLOR;
  return STATUS_COLORS[status.toLowerCase()] ?? '#6b7280';
}

function getStatusLabel(status: string, operationMode: string): string {
  if (operationMode?.toLowerCase() === 'maintenance') return 'Maintenance';
  return status.charAt(0).toUpperCase() + status.slice(1);
}

function formatDuration(ms: number): string {
  const seconds = Math.floor(ms / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  const remainMinutes = minutes % 60;
  if (hours < 24) return remainMinutes > 0 ? `${hours}h ${remainMinutes}m` : `${hours}h`;
  const days = Math.floor(hours / 24);
  const remainHours = hours % 24;
  return remainHours > 0 ? `${days}d ${remainHours}h` : `${days}d`;
}

function formatTimestamp(timestamp: number, rangeMs: number): string {
  const date = new Date(timestamp);
  if (rangeMs > 24 * 60 * 60 * 1000) {
    return date.toLocaleDateString([], { day: '2-digit', month: '2-digit' }) +
      ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

function CustomTooltip({ active, payload }: { active?: boolean; payload?: Array<{ payload: BandDataPoint }> }) {
  if (!active || !payload?.length) return null;
  const data = payload[0].payload;

  const color = getEffectiveColor(data.status, data.operationMode);
  const label = getStatusLabel(data.status, data.operationMode);

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-3 shadow-lg dark:border-gray-700 dark:bg-gray-800 max-w-xs">
      <p className="text-sm font-medium text-gray-900 dark:text-white">
        {new Date(data.timestamp).toLocaleString([], {
          day: '2-digit', month: '2-digit', year: 'numeric',
          hour: '2-digit', minute: '2-digit', second: '2-digit',
        })}
      </p>
      <p className="text-sm font-medium mt-1" style={{ color }}>
        {label} ({data.healthyServices}/{data.totalServices} healthy)
      </p>
      {data.services?.length > 0 && (
        <div className="mt-1.5 space-y-0.5">
          {data.services.map((svc) => {
            const svcColor = data.operationMode?.toLowerCase() === 'maintenance'
              ? MAINTENANCE_COLOR
              : STATUS_COLORS[svc.status.toLowerCase()] ?? '#6b7280';
            return (
              <div key={svc.name} className="flex items-center gap-1.5 text-xs text-gray-700 dark:text-gray-300">
                <span className="h-1.5 w-1.5 rounded-full flex-shrink-0" style={{ backgroundColor: svcColor }} />
                <span className="truncate">{svc.name}</span>
                <span className="ml-auto text-gray-500 dark:text-gray-400">
                  {data.operationMode?.toLowerCase() === 'maintenance' ? 'Maintenance' : svc.status}
                </span>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

function buildBandData(transitions: HealthTransitionDto[]): BandDataPoint[] {
  if (transitions.length === 0) return [];

  const points: BandDataPoint[] = transitions.map((t) => ({
    timestamp: new Date(t.capturedAtUtc).getTime(),
    value: 100,
    status: t.overallStatus,
    operationMode: t.operationMode ?? 'Normal',
    services: t.services?.map((s) => ({ name: s.name, status: s.status })) ?? [],
    healthyServices: t.healthyServices,
    totalServices: t.totalServices,
  }));

  // Extend to "now"
  const last = points[points.length - 1];
  const now = Date.now();
  if (now - last.timestamp > 30_000) {
    points.push({ ...last, timestamp: now });
  }

  return points;
}

function buildReferenceAreas(data: BandDataPoint[]) {
  if (data.length < 2) return [];

  const areas: Array<{ x1: number; x2: number; color: string }> = [];
  for (let i = 0; i < data.length - 1; i++) {
    areas.push({
      x1: data[i].timestamp,
      x2: data[i + 1].timestamp,
      color: getEffectiveColor(data[i].status, data[i].operationMode),
    });
  }
  return areas;
}

export default function HealthHistoryChart({
  deploymentId,
  className = '',
}: HealthHistoryChartProps) {
  const { transitions, loading, error } = useHealthTransitionsStore(deploymentId);

  if (loading) {
    return (
      <div className={`rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03] ${className}`}>
        <div className="animate-pulse">
          <div className="h-5 bg-gray-200 dark:bg-gray-700 rounded w-1/4 mb-4" />
          <div className="h-32 bg-gray-200 dark:bg-gray-700 rounded" />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={`rounded-xl border border-red-200 bg-red-50 p-6 dark:border-red-800 dark:bg-red-900/20 ${className}`}>
        <h4 className="text-sm font-medium text-red-800 dark:text-red-300 mb-1">
          Health History
        </h4>
        <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
      </div>
    );
  }

  if (transitions.length === 0) {
    return (
      <div className={`rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03] ${className}`}>
        <h4 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
          Health History
        </h4>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          No health history available yet.
        </p>
      </div>
    );
  }

  const bandData = buildBandData(transitions);
  const referenceAreas = buildReferenceAreas(bandData);
  const timeStart = bandData[0].timestamp;
  const timeEnd = bandData[bandData.length - 1].timestamp;
  const rangeMs = timeEnd - timeStart;

  const transitionCount = Math.max(0, transitions.length - 1);

  const firstDate = new Date(timeStart);
  const sinceLabel = rangeMs > 24 * 60 * 60 * 1000
    ? firstDate.toLocaleDateString([], { day: '2-digit', month: '2-digit', year: 'numeric' })
    : firstDate.toLocaleString([], {
        day: '2-digit', month: '2-digit',
        hour: '2-digit', minute: '2-digit',
      });

  const hasMultipleServices = (transitions[0]?.services?.length ?? 0) > 1;
  const hasMaintenance = transitions.some(
    (t) => t.operationMode?.toLowerCase() === 'maintenance',
  );

  return (
    <div className={`rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03] ${className}`}>
      {/* Header with legend */}
      <div className="flex items-center justify-between mb-4">
        <h4 className="text-lg font-semibold text-gray-900 dark:text-white">
          Health History
        </h4>
        <div className="flex items-center gap-3 text-xs text-gray-600 dark:text-gray-400">
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-full bg-green-500" />
            Healthy
          </span>
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-full bg-yellow-500" />
            Degraded
          </span>
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-full bg-red-500" />
            Unhealthy
          </span>
          {hasMaintenance && (
            <span className="flex items-center gap-1">
              <span className="h-2 w-2 rounded-full bg-blue-500" />
              Maintenance
            </span>
          )}
        </div>
      </div>

      {/* Main content: Donut + Timeline */}
      <div className="flex gap-6">
        {/* Uptime Donut */}
        <div className="flex-shrink-0">
          <UptimeDonutChart transitions={transitions} />
        </div>

        {/* Timeline section */}
        <div className="flex-1 min-w-0">
          {/* Aggregate status band */}
          <div className="h-12">
            <ResponsiveContainer width="100%" height="100%">
              <ComposedChart
                data={bandData}
                margin={{ top: 0, right: 0, left: 0, bottom: 0 }}
              >
                <XAxis
                  dataKey="timestamp"
                  type="number"
                  domain={['dataMin', 'dataMax']}
                  tickFormatter={(value) => formatTimestamp(value, rangeMs)}
                  tick={{ fontSize: 10, fill: '#9ca3af' }}
                  tickLine={false}
                  axisLine={false}
                />
                <Tooltip content={<CustomTooltip />} />
                {referenceAreas.map((area, i) => (
                  <ReferenceArea
                    key={i}
                    x1={area.x1}
                    x2={area.x2}
                    y1={0}
                    y2={100}
                    fill={area.color}
                    fillOpacity={0.3}
                  />
                ))}
                {/* Invisible line to make the chart area interactive for tooltips */}
                <Line
                  type="stepAfter"
                  dataKey="value"
                  stroke="transparent"
                  dot={false}
                  activeDot={false}
                  isAnimationActive={false}
                />
              </ComposedChart>
            </ResponsiveContainer>
          </div>

          {/* Per-service swim lanes */}
          {hasMultipleServices && (
            <ServiceHealthTimeline
              transitions={transitions}
              timeStart={timeStart}
              timeEnd={timeEnd}
            />
          )}
        </div>
      </div>

      {/* Footer */}
      <p className="mt-3 text-xs text-gray-500 dark:text-gray-400 text-center">
        {transitionCount} status change{transitionCount !== 1 ? 's' : ''} since {sinceLabel}
        {rangeMs > 0 && ` (${formatDuration(rangeMs)})`}
      </p>
    </div>
  );
}
