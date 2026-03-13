import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
  ReferenceArea,
} from 'recharts';
import { useHealthTransitionsStore } from '@rsgo/core';
import type { HealthTransitionDto } from '@rsgo/core';

interface HealthHistoryChartProps {
  deploymentId: string;
  className?: string;
}

interface ChartDataPoint {
  timestamp: number;
  healthyPercent: number;
  status: string;
  statusLabel: string;
  healthyServices: number;
  totalServices: number;
}

const STATUS_COLORS: Record<string, string> = {
  healthy: '#22c55e',
  degraded: '#eab308',
  unhealthy: '#ef4444',
};

function getStatusColor(status: string) {
  return STATUS_COLORS[status.toLowerCase()] ?? '#6b7280';
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

function CustomTooltip({ active, payload }: { active?: boolean; payload?: Array<{ payload: ChartDataPoint }> }) {
  if (active && payload && payload.length) {
    const data = payload[0].payload;
    return (
      <div className="rounded-lg border border-gray-200 bg-white p-3 shadow-lg dark:border-gray-700 dark:bg-gray-800">
        <p className="text-sm font-medium text-gray-900 dark:text-white">
          {new Date(data.timestamp).toLocaleString([], {
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit', second: '2-digit',
          })}
        </p>
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {data.healthyServices}/{data.totalServices} services healthy ({data.healthyPercent}%)
        </p>
        <p
          className="text-sm font-medium"
          style={{ color: getStatusColor(data.status) }}
        >
          {data.statusLabel}
        </p>
      </div>
    );
  }
  return null;
}

function buildChartData(transitions: HealthTransitionDto[]): ChartDataPoint[] {
  if (transitions.length === 0) return [];

  const points: ChartDataPoint[] = transitions.map((t) => ({
    timestamp: new Date(t.capturedAtUtc).getTime(),
    healthyPercent:
      t.totalServices > 0
        ? Math.round((t.healthyServices / t.totalServices) * 100)
        : 0,
    status: t.overallStatus,
    statusLabel:
      t.overallStatus.charAt(0).toUpperCase() + t.overallStatus.slice(1),
    healthyServices: t.healthyServices,
    totalServices: t.totalServices,
  }));

  // Add a "now" point extending the last status to current time
  const last = points[points.length - 1];
  const now = Date.now();
  if (now - last.timestamp > 30_000) {
    points.push({ ...last, timestamp: now });
  }

  return points;
}

/** Build colored background areas between transitions */
function buildReferenceAreas(chartData: ChartDataPoint[]) {
  if (chartData.length < 2) return [];

  const areas: Array<{ x1: number; x2: number; color: string }> = [];
  for (let i = 0; i < chartData.length - 1; i++) {
    areas.push({
      x1: chartData[i].timestamp,
      x2: chartData[i + 1].timestamp,
      color: getStatusColor(chartData[i].status),
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
          <div className="h-48 bg-gray-200 dark:bg-gray-700 rounded" />
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

  const chartData = buildChartData(transitions);
  const referenceAreas = buildReferenceAreas(chartData);
  const rangeMs = chartData.length >= 2
    ? chartData[chartData.length - 1].timestamp - chartData[0].timestamp
    : 0;

  // Count actual status changes (excluding first baseline and "now" extension)
  const transitionCount = Math.max(0, transitions.length - 1);

  const firstDate = new Date(chartData[0].timestamp);
  const sinceLabel = rangeMs > 24 * 60 * 60 * 1000
    ? firstDate.toLocaleDateString([], { day: '2-digit', month: '2-digit', year: 'numeric' })
    : firstDate.toLocaleString([], {
        day: '2-digit', month: '2-digit',
        hour: '2-digit', minute: '2-digit',
      });

  return (
    <div className={`rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03] ${className}`}>
      <div className="flex items-center justify-between mb-4">
        <h4 className="text-lg font-semibold text-gray-900 dark:text-white">
          Health History
        </h4>
        <div className="flex items-center gap-4 text-xs">
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
        </div>
      </div>

      <div className="h-48">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart
            data={chartData}
            margin={{ top: 5, right: 5, left: -20, bottom: 5 }}
          >
            <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
            <XAxis
              dataKey="timestamp"
              type="number"
              domain={['dataMin', 'dataMax']}
              tickFormatter={(value) => formatTimestamp(value, rangeMs)}
              tick={{ fontSize: 10, fill: '#9ca3af' }}
              tickLine={false}
              axisLine={false}
            />
            <YAxis
              domain={[0, 100]}
              tick={{ fontSize: 10, fill: '#9ca3af' }}
              tickLine={false}
              axisLine={false}
              tickFormatter={(value) => `${value}%`}
            />
            <Tooltip content={<CustomTooltip />} />
            {referenceAreas.map((area, i) => (
              <ReferenceArea
                key={i}
                x1={area.x1}
                x2={area.x2}
                fill={area.color}
                fillOpacity={0.1}
              />
            ))}
            <Line
              type="stepAfter"
              dataKey="healthyPercent"
              stroke="#6b7280"
              strokeWidth={2}
              dot={{ r: 4, fill: '#fff', stroke: '#6b7280', strokeWidth: 2 }}
              activeDot={{ r: 6 }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>

      <p className="mt-2 text-xs text-gray-500 dark:text-gray-400 text-center">
        {transitionCount} status change{transitionCount !== 1 ? 's' : ''} since {sinceLabel}
        {rangeMs > 0 && ` (${formatDuration(rangeMs)})`}
      </p>
    </div>
  );
}
