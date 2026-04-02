import { useState } from 'react';
import type { ProductPrecheckResponse, ProductPrecheckStackResult, PrecheckCheckDto } from '@rsgo/core';

interface ProductPrecheckPanelProps {
  result: ProductPrecheckResponse;
  isLoading?: boolean;
  onRecheck?: () => void;
}

function SeverityIcon({ severity }: { severity: string }) {
  switch (severity) {
    case 'ok':
      return (
        <svg className="w-4 h-4 text-green-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
        </svg>
      );
    case 'warning':
      return (
        <svg className="w-4 h-4 text-yellow-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
        </svg>
      );
    case 'error':
      return (
        <svg className="w-4 h-4 text-red-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
        </svg>
      );
    default:
      return null;
  }
}

function StackStatusIcon({ stack }: { stack: ProductPrecheckStackResult }) {
  if (stack.hasErrors) {
    return (
      <svg className="w-5 h-5 text-red-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
      </svg>
    );
  }
  if (stack.hasWarnings) {
    return (
      <svg className="w-5 h-5 text-yellow-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
      </svg>
    );
  }
  return (
    <svg className="w-5 h-5 text-green-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
    </svg>
  );
}

function CheckRow({ check }: { check: PrecheckCheckDto }) {
  return (
    <div className="flex items-start gap-2 py-1.5">
      <SeverityIcon severity={check.severity} />
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-700 dark:text-gray-300">{check.title}</span>
          {check.serviceName && (
            <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-gray-200 text-gray-500 dark:bg-gray-700 dark:text-gray-400">
              {check.serviceName}
            </span>
          )}
        </div>
        {check.detail && check.severity !== 'ok' && (
          <p className="text-[11px] text-gray-500 dark:text-gray-500 mt-0.5">{check.detail}</p>
        )}
      </div>
    </div>
  );
}

function StackSection({ stack, defaultExpanded }: { stack: ProductPrecheckStackResult; defaultExpanded: boolean }) {
  const [expanded, setExpanded] = useState(defaultExpanded);

  const sortedChecks = [...stack.checks].sort((a, b) => {
    const order: Record<string, number> = { error: 0, warning: 1, ok: 2 };
    return (order[a.severity] ?? 3) - (order[b.severity] ?? 3);
  });

  const borderColor = stack.hasErrors
    ? 'border-red-200 dark:border-red-800'
    : stack.hasWarnings
      ? 'border-yellow-200 dark:border-yellow-800'
      : 'border-green-200 dark:border-green-800';

  return (
    <div className={`border rounded-lg overflow-hidden ${borderColor}`}>
      <button
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-center justify-between p-3 text-left hover:bg-gray-50 dark:hover:bg-gray-800/50 transition-colors"
      >
        <div className="flex items-center gap-2">
          <StackStatusIcon stack={stack} />
          <span className="text-sm font-medium text-gray-900 dark:text-white">{stack.stackName}</span>
          <span className="text-xs text-gray-500 dark:text-gray-400">{stack.summary}</span>
        </div>
        <svg
          className={`w-4 h-4 text-gray-400 transition-transform ${expanded ? 'rotate-90' : ''}`}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
        </svg>
      </button>

      {expanded && (
        <div className="border-t border-gray-200 dark:border-gray-700 px-3 pb-3">
          {sortedChecks.map((check, i) => (
            <CheckRow key={`${check.rule}-${i}`} check={check} />
          ))}
        </div>
      )}
    </div>
  );
}

export function ProductPrecheckPanel({ result, isLoading, onRecheck }: ProductPrecheckPanelProps) {
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

      {/* Per-Stack Results */}
      <div className="space-y-2">
        {result.stacks.map((stack) => (
          <StackSection
            key={stack.stackId}
            stack={stack}
            defaultExpanded={stack.hasErrors || stack.hasWarnings}
          />
        ))}
      </div>
    </div>
  );
}
