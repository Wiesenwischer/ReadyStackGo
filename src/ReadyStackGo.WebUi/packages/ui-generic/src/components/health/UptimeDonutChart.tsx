import { PieChart, Pie, Cell, ResponsiveContainer } from 'recharts';
import type { HealthTransitionDto } from '@rsgo/core';

interface UptimeDonutChartProps {
  transitions: HealthTransitionDto[];
}

interface UptimeSlice {
  status: string;
  label: string;
  durationMs: number;
  percent: number;
  color: string;
}

const STATUS_CONFIG: Record<string, { label: string; color: string }> = {
  healthy: { label: 'Healthy', color: '#22c55e' },
  degraded: { label: 'Degraded', color: '#eab308' },
  unhealthy: { label: 'Unhealthy', color: '#ef4444' },
  maintenance: { label: 'Maintenance', color: '#3b82f6' },
};

function getConfig(status: string) {
  return STATUS_CONFIG[status.toLowerCase()] ?? { label: status, color: '#6b7280' };
}

function buildUptimeSlices(transitions: HealthTransitionDto[]): UptimeSlice[] {
  if (transitions.length === 0) return [];

  const durations: Record<string, number> = {};
  const now = Date.now();

  for (let i = 0; i < transitions.length; i++) {
    const startMs = new Date(transitions[i].capturedAtUtc).getTime();
    const endMs = i < transitions.length - 1
      ? new Date(transitions[i + 1].capturedAtUtc).getTime()
      : now;

    const isMaintenance = transitions[i].operationMode?.toLowerCase() === 'maintenance';
    const key = isMaintenance ? 'maintenance' : transitions[i].overallStatus.toLowerCase();
    durations[key] = (durations[key] || 0) + (endMs - startMs);
  }

  const totalMs = Object.values(durations).reduce((sum, d) => sum + d, 0);
  if (totalMs <= 0) return [];

  return Object.entries(durations)
    .filter(([, ms]) => ms > 0)
    .map(([status, ms]) => {
      const config = getConfig(status);
      return {
        status,
        label: config.label,
        durationMs: ms,
        percent: Math.round((ms / totalMs) * 1000) / 10,
        color: config.color,
      };
    })
    .sort((a, b) => b.durationMs - a.durationMs);
}

function getUptimePercent(slices: UptimeSlice[]): number {
  const normalTime = slices
    .filter((s) => s.status !== 'maintenance')
    .reduce((sum, s) => sum + s.durationMs, 0);
  const healthyTime = slices
    .find((s) => s.status === 'healthy')?.durationMs ?? 0;
  return normalTime > 0 ? Math.round((healthyTime / normalTime) * 1000) / 10 : 0;
}

export default function UptimeDonutChart({ transitions }: UptimeDonutChartProps) {
  const slices = buildUptimeSlices(transitions);
  if (slices.length === 0) return null;

  const uptimePercent = getUptimePercent(slices);

  return (
    <div className="flex flex-col items-center">
      <div className="relative" style={{ width: 120, height: 120 }}>
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie
              data={slices}
              dataKey="durationMs"
              cx="50%"
              cy="50%"
              innerRadius={36}
              outerRadius={52}
              strokeWidth={0}
            >
              {slices.map((slice, i) => (
                <Cell key={i} fill={slice.color} />
              ))}
            </Pie>
          </PieChart>
        </ResponsiveContainer>
        <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
          <span className="text-lg font-bold text-gray-900 dark:text-white leading-none">
            {uptimePercent}%
          </span>
          <span className="text-[10px] text-gray-500 dark:text-gray-400">uptime</span>
        </div>
      </div>
      <div className="mt-2 flex flex-wrap justify-center gap-x-3 gap-y-1">
        {slices.map((slice) => (
          <span key={slice.status} className="flex items-center gap-1 text-[10px] text-gray-600 dark:text-gray-400">
            <span className="h-2 w-2 rounded-full flex-shrink-0" style={{ backgroundColor: slice.color }} />
            {slice.percent}% {slice.label}
          </span>
        ))}
      </div>
    </div>
  );
}
