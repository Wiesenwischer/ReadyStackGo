import { useState, useRef, useCallback, useEffect } from 'react';
import type { HealthTransitionDto } from '@rsgo/core';

interface ServiceSegment {
  startTime: number;
  endTime: number;
  status: string;
  operationMode: string;
}

interface ServiceTimeline {
  name: string;
  segments: ServiceSegment[];
}

const STATUS_COLORS: Record<string, string> = {
  healthy: '#22c55e',
  degraded: '#eab308',
  unhealthy: '#ef4444',
  unknown: '#6b7280',
  notfound: '#6b7280',
};

const MAINTENANCE_COLOR = '#3b82f6';

function getSegmentColor(segment: ServiceSegment): string {
  if (segment.operationMode?.toLowerCase() === 'maintenance') return MAINTENANCE_COLOR;
  return STATUS_COLORS[segment.status.toLowerCase()] ?? '#6b7280';
}

function getSegmentLabel(segment: ServiceSegment): string {
  if (segment.operationMode?.toLowerCase() === 'maintenance') return 'Maintenance';
  const s = segment.status;
  return s.charAt(0).toUpperCase() + s.slice(1);
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

export function buildServiceTimelines(transitions: HealthTransitionDto[]): ServiceTimeline[] {
  if (transitions.length === 0) return [];

  const serviceNames = new Set<string>();
  for (const t of transitions) {
    for (const s of t.services ?? []) {
      serviceNames.add(s.name);
    }
  }

  const now = Date.now();
  const sortedNames = [...serviceNames].sort();

  return sortedNames.map((name) => {
    const segments: ServiceSegment[] = [];

    for (let i = 0; i < transitions.length; i++) {
      const t = transitions[i];
      const startMs = new Date(t.capturedAtUtc).getTime();
      const endMs = i < transitions.length - 1
        ? new Date(transitions[i + 1].capturedAtUtc).getTime()
        : now;

      const svc = t.services?.find((s) => s.name === name);
      const status = svc?.status ?? 'unknown';

      segments.push({
        startTime: startMs,
        endTime: endMs,
        status,
        operationMode: t.operationMode ?? 'Normal',
      });
    }

    return { name, segments };
  });
}

interface TooltipInfo {
  x: number;
  y: number;
  serviceName: string;
  segment: ServiceSegment;
}

interface ServiceHealthTimelineProps {
  transitions: HealthTransitionDto[];
  timeStart: number;
  timeEnd: number;
}

const ROW_HEIGHT = 24;
const LABEL_WIDTH = 130;

export default function ServiceHealthTimeline({
  transitions,
  timeStart,
  timeEnd,
}: ServiceHealthTimelineProps) {
  const timelines = buildServiceTimelines(transitions);
  const [tooltip, setTooltip] = useState<TooltipInfo | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [containerWidth, setContainerWidth] = useState(300);

  useEffect(() => {
    if (containerRef.current) {
      setContainerWidth(containerRef.current.clientWidth);
    }
  }, [timelines]);

  const handleMouseEnter = useCallback(
    (e: React.MouseEvent, serviceName: string, segment: ServiceSegment) => {
      const rect = containerRef.current?.getBoundingClientRect();
      if (!rect) return;
      setTooltip({
        x: e.clientX - rect.left,
        y: e.clientY - rect.top,
        serviceName,
        segment,
      });
    },
    [],
  );

  const handleMouseLeave = useCallback(() => setTooltip(null), []);

  if (timelines.length <= 1) return null;

  const timeRange = timeEnd - timeStart;
  if (timeRange <= 0) return null;

  return (
    <div className="relative mt-2" ref={containerRef}>
      <div
        className="overflow-y-auto"
        style={{ maxHeight: ROW_HEIGHT * 8 + 4 }}
      >
        {timelines.map((tl) => (
          <div key={tl.name} className="flex items-center" style={{ height: ROW_HEIGHT }}>
            <div
              className="text-[11px] text-gray-600 dark:text-gray-400 truncate flex-shrink-0 pr-2"
              style={{ width: LABEL_WIDTH }}
              title={tl.name}
            >
              {tl.name}
            </div>
            <div className="flex-1 relative h-4">
              <svg width="100%" height="100%" preserveAspectRatio="none">
                {tl.segments.map((seg, i) => {
                  const left = ((seg.startTime - timeStart) / timeRange) * 100;
                  const width = ((seg.endTime - seg.startTime) / timeRange) * 100;
                  return (
                    <rect
                      key={i}
                      x={`${left}%`}
                      y="2"
                      width={`${Math.max(width, 0.3)}%`}
                      height="12"
                      rx="2"
                      fill={getSegmentColor(seg)}
                      fillOpacity={0.7}
                      onMouseEnter={(e) => handleMouseEnter(e as unknown as React.MouseEvent, tl.name, seg)}
                      onMouseLeave={handleMouseLeave}
                      style={{ cursor: 'pointer' }}
                    />
                  );
                })}
              </svg>
            </div>
          </div>
        ))}
      </div>

      {tooltip && (
        <div
          className="absolute z-50 rounded-lg border border-gray-200 bg-white p-2 shadow-lg dark:border-gray-700 dark:bg-gray-800 pointer-events-none text-xs"
          style={{
            left: Math.min(tooltip.x, containerWidth - 200),
            top: tooltip.y - 60,
          }}
        >
          <p className="font-medium text-gray-900 dark:text-white">{tooltip.serviceName}</p>
          <p style={{ color: getSegmentColor(tooltip.segment) }} className="font-medium">
            {getSegmentLabel(tooltip.segment)}
          </p>
          <p className="text-gray-500 dark:text-gray-400">
            {formatDuration(tooltip.segment.endTime - tooltip.segment.startTime)}
          </p>
        </div>
      )}
    </div>
  );
}
