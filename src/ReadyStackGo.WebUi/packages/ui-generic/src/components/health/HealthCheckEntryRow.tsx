import { useState } from 'react';
import { type HealthCheckEntryDto, getHealthStatusPresentation } from '@rsgo/core';

interface HealthCheckEntryRowProps {
  entry: HealthCheckEntryDto;
}

export default function HealthCheckEntryRow({ entry }: HealthCheckEntryRowProps) {
  const [showData, setShowData] = useState(false);
  const presentation = getHealthStatusPresentation(entry.status);
  const hasData = entry.data && Object.keys(entry.data).length > 0;
  const hasExtra = hasData || entry.exception;

  return (
    <div className="py-2 px-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span
            className={`h-1.5 w-1.5 rounded-full ${
              entry.status.toLowerCase() === 'healthy'
                ? 'bg-green-500'
                : entry.status.toLowerCase() === 'degraded'
                ? 'bg-yellow-500'
                : entry.status.toLowerCase() === 'unhealthy'
                ? 'bg-red-500'
                : 'bg-gray-400'
            }`}
          />
          <span className="text-xs font-medium text-gray-700 dark:text-gray-300">
            {entry.name}
          </span>
          {entry.tags && entry.tags.length > 0 && (
            <div className="flex gap-1">
              {entry.tags.map((tag) => (
                <span
                  key={tag}
                  className="inline-flex items-center rounded px-1 py-0.5 text-[10px] font-medium bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400"
                >
                  {tag}
                </span>
              ))}
            </div>
          )}
        </div>
        <div className="flex items-center gap-3">
          {entry.durationMs != null && (
            <span className="text-[10px] text-gray-400 dark:text-gray-500 tabular-nums">
              {entry.durationMs < 1
                ? '<1ms'
                : entry.durationMs < 1000
                ? `${Math.round(entry.durationMs)}ms`
                : `${(entry.durationMs / 1000).toFixed(1)}s`}
            </span>
          )}
          <span
            className={`inline-flex items-center rounded px-1.5 py-0.5 text-[10px] font-medium ${presentation.bgColor} ${presentation.textColor}`}
          >
            {presentation.label}
          </span>
          {hasExtra && (
            <button
              onClick={() => setShowData(!showData)}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
            >
              <svg
                className={`w-3.5 h-3.5 transition-transform ${showData ? 'rotate-180' : ''}`}
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
              </svg>
            </button>
          )}
        </div>
      </div>

      {entry.description && (
        <p className="mt-1 ml-3.5 text-[11px] text-gray-500 dark:text-gray-400">
          {entry.description}
        </p>
      )}

      {showData && (
        <div className="mt-2 ml-3.5 space-y-2">
          {hasData && (
            <div className="rounded bg-gray-50 dark:bg-gray-800/50 p-2">
              <table className="w-full text-[11px]">
                <tbody>
                  {Object.entries(entry.data!).map(([key, value]) => (
                    <tr key={key}>
                      <td className="pr-3 py-0.5 font-medium text-gray-500 dark:text-gray-400 whitespace-nowrap align-top">
                        {key}
                      </td>
                      <td className="py-0.5 text-gray-700 dark:text-gray-300 break-all">
                        {value}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {entry.exception && (
            <pre className="rounded bg-red-50 dark:bg-red-900/20 p-2 text-[11px] text-red-700 dark:text-red-400 overflow-x-auto whitespace-pre-wrap">
              {entry.exception}
            </pre>
          )}
        </div>
      )}
    </div>
  );
}
