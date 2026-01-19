import { useEffect, useState } from 'react';
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts';
import { getHealthHistory, type StackHealthSummaryDto } from '../../api/health';

interface HealthHistoryChartProps {
  deploymentId: string;
  className?: string;
}

interface ChartDataPoint {
  time: string;
  timestamp: number;
  healthyPercent: number;
  status: string;
  statusLabel: string;
}

export default function HealthHistoryChart({
  deploymentId,
  className = '',
}: HealthHistoryChartProps) {
  const [history, setHistory] = useState<StackHealthSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchHistory = async () => {
      try {
        setLoading(true);
        const data = await getHealthHistory(deploymentId, 100);
        setHistory(data);
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load history');
      } finally {
        setLoading(false);
      }
    };

    fetchHistory();
  }, [deploymentId]);

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

  if (history.length === 0) {
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

  // Transform data for the chart
  const chartData: ChartDataPoint[] = history
    .map((snapshot) => {
      const healthyPercent =
        snapshot.totalServices > 0
          ? Math.round((snapshot.healthyServices / snapshot.totalServices) * 100)
          : 0;

      return {
        time: new Date(snapshot.capturedAtUtc).toLocaleTimeString([], {
          hour: '2-digit',
          minute: '2-digit',
        }),
        timestamp: new Date(snapshot.capturedAtUtc).getTime(),
        healthyPercent,
        status: snapshot.overallStatus,
        statusLabel:
          snapshot.overallStatus.charAt(0).toUpperCase() +
          snapshot.overallStatus.slice(1),
      };
    })
    .reverse(); // Oldest first for chart

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'healthy':
        return '#22c55e'; // green-500
      case 'degraded':
        return '#eab308'; // yellow-500
      case 'unhealthy':
        return '#ef4444'; // red-500
      default:
        return '#6b7280'; // gray-500
    }
  };

  const CustomTooltip = ({ active, payload }: { active?: boolean; payload?: Array<{ payload: ChartDataPoint }> }) => {
    if (active && payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div className="rounded-lg border border-gray-200 bg-white p-3 shadow-lg dark:border-gray-700 dark:bg-gray-800">
          <p className="text-sm font-medium text-gray-900 dark:text-white">
            {data.time}
          </p>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            Health: {data.healthyPercent}%
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
  };

  // Determine chart color based on current (latest) status
  const currentStatus = history[0]?.overallStatus || 'unknown';
  const chartColor = getStatusColor(currentStatus);

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
          <AreaChart
            data={chartData}
            margin={{ top: 5, right: 5, left: -20, bottom: 5 }}
          >
            <defs>
              <linearGradient id="healthGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor={chartColor} stopOpacity={0.3} />
                <stop offset="95%" stopColor={chartColor} stopOpacity={0} />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
            <XAxis
              dataKey="time"
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
            <Area
              type="monotone"
              dataKey="healthyPercent"
              stroke={chartColor}
              strokeWidth={2}
              fill="url(#healthGradient)"
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>

      <p className="mt-2 text-xs text-gray-500 dark:text-gray-400 text-center">
        Last {history.length} health checks
      </p>
    </div>
  );
}
