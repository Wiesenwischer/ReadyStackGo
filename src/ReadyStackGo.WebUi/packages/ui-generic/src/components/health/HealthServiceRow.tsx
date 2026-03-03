import { useState } from 'react';
import { Link } from 'react-router';
import { type ServiceHealthDto, getHealthStatusPresentation } from '@rsgo/core';
import HealthCheckEntryRow from './HealthCheckEntryRow';

interface HealthServiceRowProps {
  service: ServiceHealthDto;
  deploymentId?: string;
}

export default function HealthServiceRow({ service, deploymentId }: HealthServiceRowProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const presentation = getHealthStatusPresentation(service.status);
  const hasEntries = service.healthCheckEntries && service.healthCheckEntries.length > 0;

  return (
    <div className="border-b border-gray-100 dark:border-gray-800 last:border-0">
      <div
        className={`flex items-center justify-between py-2 px-3 hover:bg-gray-50 dark:hover:bg-gray-800/30 transition-colors ${hasEntries ? 'cursor-pointer' : ''}`}
        onClick={hasEntries ? () => setIsExpanded(!isExpanded) : undefined}
      >
        <div className="flex items-center gap-3">
          <span
            className={`h-2 w-2 rounded-full ${
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
            <span className="text-sm font-medium text-gray-900 dark:text-white">
              {service.name}
            </span>
            {service.containerName && (
              <span className="ml-2 text-xs text-gray-500 dark:text-gray-400">
                ({service.containerName})
              </span>
            )}
          </div>
        </div>
        <div className="flex items-center gap-4">
          {service.responseTimeMs != null && (
            <span className="text-xs text-gray-400 dark:text-gray-500 tabular-nums">
              {service.responseTimeMs}ms
            </span>
          )}
          {service.restartCount > 0 && (
            <span className="text-xs text-gray-500 dark:text-gray-400">
              {service.restartCount} restart{service.restartCount !== 1 ? 's' : ''}
            </span>
          )}
          <span
            className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${presentation.bgColor} ${presentation.textColor}`}
          >
            {presentation.label}
          </span>
          {deploymentId && (
            <Link
              to={`/health/${deploymentId}/${encodeURIComponent(service.name)}`}
              className="text-gray-400 hover:text-brand-500 transition-colors"
              onClick={(e) => e.stopPropagation()}
              title="View details"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
              </svg>
            </Link>
          )}
          {hasEntries && (
            <svg
              className={`w-4 h-4 text-gray-400 transition-transform ${isExpanded ? 'rotate-180' : ''}`}
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          )}
        </div>
      </div>

      {isExpanded && hasEntries && (
        <div className="ml-5 border-l-2 border-gray-200 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-800/20">
          <div className="divide-y divide-gray-100 dark:divide-gray-800">
            {service.healthCheckEntries!.map((entry) => (
              <HealthCheckEntryRow key={entry.name} entry={entry} />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
