import { parseErrorMessage, type ParsedError } from '@rsgo/core';

interface DeploymentErrorProps {
  /** Raw error message string */
  error: string | null | undefined;
  /** Optional title override (default: "Deployment Error") */
  title?: string;
  /** Compact mode — no title, smaller text */
  compact?: boolean;
}

/**
 * Structured deployment error display.
 * Shows a short summary with optional collapsible details for stack traces.
 */
export function DeploymentError({ error, title = 'Deployment Error', compact = false }: DeploymentErrorProps) {
  const parsed = parseErrorMessage(error);
  if (!parsed) return null;

  if (compact) {
    return <CompactError parsed={parsed} />;
  }

  return (
    <div className="rounded-lg border border-red-200 bg-red-50 dark:border-red-800 dark:bg-red-900/20 overflow-hidden">
      <div className="px-4 py-3">
        <div className="flex items-start gap-3">
          <svg className="w-5 h-5 text-red-500 mt-0.5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
          </svg>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold text-red-800 dark:text-red-200">{title}</p>
            {parsed.containerName && (
              <p className="mt-0.5 text-xs text-red-600 dark:text-red-400">
                Container: <code className="font-mono bg-red-100 dark:bg-red-900/40 px-1 rounded">{parsed.containerName}</code>
                {parsed.exitCode !== undefined && (
                  <span className="ml-2">Exit code: <code className="font-mono bg-red-100 dark:bg-red-900/40 px-1 rounded">{parsed.exitCode}</code></span>
                )}
              </p>
            )}
            <p className="mt-1 text-sm text-red-700 dark:text-red-300 break-words">{parsed.summary}</p>
          </div>
        </div>
      </div>

      {parsed.details && parsed.details !== parsed.summary && (
        <details className="border-t border-red-200 dark:border-red-800">
          <summary className="px-4 py-2 text-xs font-medium text-red-600 dark:text-red-400 cursor-pointer hover:bg-red-100 dark:hover:bg-red-900/30 select-none">
            Show full error details
          </summary>
          <pre className="px-4 py-3 text-xs text-red-700 dark:text-red-300 bg-red-100/50 dark:bg-red-950/30 overflow-x-auto max-h-64 whitespace-pre-wrap break-words font-mono">
            {parsed.details}
          </pre>
        </details>
      )}
    </div>
  );
}

function CompactError({ parsed }: { parsed: ParsedError }) {
  return (
    <div className="text-xs text-red-600 dark:text-red-400">
      <span className="font-medium">{parsed.summary}</span>
      {parsed.details && parsed.details !== parsed.summary && (
        <details className="mt-1">
          <summary className="cursor-pointer hover:underline select-none">Details</summary>
          <pre className="mt-1 p-2 text-xs bg-red-50 dark:bg-red-950/30 rounded overflow-x-auto max-h-48 whitespace-pre-wrap break-words font-mono">
            {parsed.details}
          </pre>
        </details>
      )}
    </div>
  );
}
