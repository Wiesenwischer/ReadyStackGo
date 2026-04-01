import type { PrecheckResponse, PrecheckCheckDto } from '@rsgo/core';

interface PrecheckPanelProps {
  result: PrecheckResponse;
  isLoading?: boolean;
  onRecheck?: () => void;
}

function SeverityIcon({ severity }: { severity: string }) {
  switch (severity) {
    case 'ok':
      return (
        <svg className="w-5 h-5 text-green-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
        </svg>
      );
    case 'warning':
      return (
        <svg className="w-5 h-5 text-yellow-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
        </svg>
      );
    case 'error':
      return (
        <svg className="w-5 h-5 text-red-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
        </svg>
      );
    default:
      return null;
  }
}

function severityBgClass(severity: string): string {
  switch (severity) {
    case 'ok':
      return 'bg-green-50 dark:bg-green-900/20';
    case 'warning':
      return 'bg-yellow-50 dark:bg-yellow-900/20';
    case 'error':
      return 'bg-red-50 dark:bg-red-900/20';
    default:
      return 'bg-gray-50 dark:bg-gray-900/20';
  }
}

function PrecheckItemRow({ check }: { check: PrecheckCheckDto }) {
  const isExpandable = check.severity !== 'ok';
  const content = (
    <div className={`flex items-start gap-3 p-3 rounded-lg ${severityBgClass(check.severity)}`}>
      <SeverityIcon severity={check.severity} />
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-gray-900 dark:text-white">
            {check.title}
          </span>
          {check.serviceName && (
            <span className="text-xs px-2 py-0.5 rounded-full bg-gray-200 text-gray-600 dark:bg-gray-700 dark:text-gray-300">
              {check.serviceName}
            </span>
          )}
        </div>
        {check.detail && isExpandable && (
          <p className="text-xs text-gray-600 dark:text-gray-400 mt-1">{check.detail}</p>
        )}
      </div>
    </div>
  );

  return content;
}

export function PrecheckPanel({ result, isLoading, onRecheck }: PrecheckPanelProps) {
  // Group checks: errors first, then warnings, then OK
  const sortedChecks = [...result.checks].sort((a, b) => {
    const order = { error: 0, warning: 1, ok: 2 };
    return (order[a.severity] ?? 3) - (order[b.severity] ?? 3);
  });

  const summaryColor = result.hasErrors
    ? 'text-red-700 dark:text-red-400'
    : result.hasWarnings
      ? 'text-yellow-700 dark:text-yellow-400'
      : 'text-green-700 dark:text-green-400';

  const summaryBg = result.hasErrors
    ? 'bg-red-50 border-red-200 dark:bg-red-900/20 dark:border-red-800'
    : result.hasWarnings
      ? 'bg-yellow-50 border-yellow-200 dark:bg-yellow-900/20 dark:border-yellow-800'
      : 'bg-green-50 border-green-200 dark:bg-green-900/20 dark:border-green-800';

  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-white/[0.03]">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white">
          Deployment Precheck
        </h2>
        {onRecheck && (
          <button
            onClick={onRecheck}
            disabled={isLoading}
            className="inline-flex items-center gap-1.5 text-sm text-brand-600 hover:text-brand-700 disabled:opacity-50 dark:text-brand-400"
          >
            <svg className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Re-Check
          </button>
        )}
      </div>

      {/* Summary Banner */}
      <div className={`rounded-lg border p-3 mb-4 ${summaryBg}`}>
        <p className={`text-sm font-medium ${summaryColor}`}>
          {result.summary}
        </p>
      </div>

      {/* Check Items */}
      <div className="space-y-2">
        {sortedChecks.map((check, index) => (
          <PrecheckItemRow key={`${check.rule}-${index}`} check={check} />
        ))}
      </div>
    </div>
  );
}
