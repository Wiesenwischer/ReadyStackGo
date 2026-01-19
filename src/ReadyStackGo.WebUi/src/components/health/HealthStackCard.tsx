import { useState } from 'react';
import { Link } from 'react-router';
import {
  type StackHealthDto,
  type StackHealthSummaryDto,
  getHealthStatusPresentation,
  getOperationModePresentation,
} from '../../api/health';
import HealthServiceRow from './HealthServiceRow';

interface HealthStackCardProps {
  stack: StackHealthSummaryDto;
  detailedHealth?: StackHealthDto | null;
  onExpand?: (deploymentId: string) => void;
  isLoading?: boolean;
}

export default function HealthStackCard({
  stack,
  detailedHealth,
  onExpand,
  isLoading,
}: HealthStackCardProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const statusPresentation = getHealthStatusPresentation(stack.overallStatus);
  const modePresentation = getOperationModePresentation(stack.operationMode);

  const handleToggleExpand = () => {
    const newExpanded = !isExpanded;
    setIsExpanded(newExpanded);
    if (newExpanded && onExpand) {
      onExpand(stack.deploymentId);
    }
  };

  const formatTime = (dateString: string) => {
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
  };

  return (
    <div className="rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03] overflow-hidden transition-all">
      {/* Header - clickable to expand */}
      <button
        onClick={handleToggleExpand}
        className="w-full px-4 py-4 flex items-center justify-between hover:bg-gray-50 dark:hover:bg-gray-800/30 transition-colors cursor-pointer"
      >
        <div className="flex items-center gap-3">
          {/* Status indicator */}
          <span
            className={`h-3 w-3 rounded-full ${
              stack.overallStatus.toLowerCase() === 'healthy'
                ? 'bg-green-500'
                : stack.overallStatus.toLowerCase() === 'degraded'
                ? 'bg-yellow-500'
                : stack.overallStatus.toLowerCase() === 'unhealthy'
                ? 'bg-red-500'
                : 'bg-gray-400'
            }`}
          />
          {/* Stack name */}
          <div className="text-left">
            <div className="flex items-center gap-2">
              <span className="font-semibold text-gray-900 dark:text-white">
                {stack.stackName}
              </span>
              {stack.currentVersion && (
                <span className="text-xs text-gray-500 dark:text-gray-400">
                  v{stack.currentVersion}
                </span>
              )}
            </div>
            <div className="text-xs text-gray-500 dark:text-gray-400">
              Last check: {formatTime(stack.capturedAtUtc)}
            </div>
          </div>
        </div>

        <div className="flex items-center gap-3">
          {/* Service count */}
          <span className="text-sm text-gray-600 dark:text-gray-400">
            {stack.healthyServices}/{stack.totalServices} services
          </span>

          {/* Operation mode badge (if not Normal) */}
          {stack.operationMode !== 'Normal' && (
            <span
              className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${modePresentation.bgColor} ${modePresentation.textColor}`}
            >
              {modePresentation.label}
            </span>
          )}

          {/* Status badge */}
          <span
            className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${statusPresentation.bgColor} ${statusPresentation.textColor}`}
          >
            {stack.requiresAttention && (
              <svg
                className="w-3 h-3 mr-1"
                fill="currentColor"
                viewBox="0 0 20 20"
              >
                <path
                  fillRule="evenodd"
                  d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z"
                  clipRule="evenodd"
                />
              </svg>
            )}
            {statusPresentation.label}
          </span>

          {/* Expand/collapse icon */}
          <svg
            className={`w-5 h-5 text-gray-400 transition-transform ${
              isExpanded ? 'rotate-180' : ''
            }`}
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M19 9l-7 7-7-7"
            />
          </svg>
        </div>
      </button>

      {/* Expanded content */}
      {isExpanded && (
        <div className="border-t border-gray-200 dark:border-gray-800">
          {isLoading ? (
            <div className="p-4">
              <div className="animate-pulse space-y-2">
                <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded w-3/4" />
                <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded w-1/2" />
                <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded w-2/3" />
              </div>
            </div>
          ) : detailedHealth ? (
            <div>
              {/* Services list */}
              <div className="divide-y divide-gray-100 dark:divide-gray-800">
                {detailedHealth.self.services.map((service) => (
                  <HealthServiceRow key={service.name} service={service} />
                ))}
              </div>

              {/* Footer with link to detail page */}
              <div className="px-4 py-3 bg-gray-50 dark:bg-gray-800/30 flex justify-between items-center">
                <span className="text-xs text-gray-500 dark:text-gray-400">
                  {stack.statusMessage}
                </span>
                <Link
                  to={`/deployments/${encodeURIComponent(stack.stackName)}`}
                  className="text-sm font-medium text-brand-500 hover:text-brand-600 dark:text-brand-400"
                >
                  View Details
                </Link>
              </div>
            </div>
          ) : (
            <div className="p-4 text-sm text-gray-500 dark:text-gray-400">
              No detailed health data available
            </div>
          )}
        </div>
      )}
    </div>
  );
}
