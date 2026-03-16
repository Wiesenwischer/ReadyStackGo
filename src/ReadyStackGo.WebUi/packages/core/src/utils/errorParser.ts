/**
 * Parse a raw deployment error message into a structured format with
 * a short summary line and collapsible details.
 */
export interface ParsedError {
  /** Short one-line summary of the error (first meaningful line) */
  summary: string;
  /** Full error details (stack trace, logs) — may be empty if error is short */
  details: string;
  /** Detected container/service name if available */
  containerName?: string;
  /** Detected exit code if available */
  exitCode?: number;
}

/**
 * Parse a raw error message into summary + details.
 *
 * Patterns detected:
 * - Init container failures: "Init container 'X' failed with exit code N"
 * - Image pull failures: "Failed to pull image 'X'"
 * - Generic: first line = summary, rest = details
 */
export function parseErrorMessage(raw: string | null | undefined): ParsedError | null {
  if (!raw || raw.trim().length === 0) return null;

  const trimmed = raw.trim();

  // Pattern: Init container failure
  const initMatch = trimmed.match(/Init container '([^']+)' failed with exit code (\d+)/i);
  if (initMatch) {
    return {
      summary: `Init container '${initMatch[1]}' failed with exit code ${initMatch[2]}`,
      details: trimmed,
      containerName: initMatch[1],
      exitCode: parseInt(initMatch[2]),
    };
  }

  // Pattern: "Deployment failed: Init container 'X' failed: ..."
  const deployInitMatch = trimmed.match(/Deployment failed: Init container '([^']+)' failed/i);
  if (deployInitMatch) {
    const firstSentenceEnd = trimmed.indexOf('. Last ');
    const summary = firstSentenceEnd > 0
      ? trimmed.substring(0, firstSentenceEnd + 1)
      : trimmed.substring(0, Math.min(trimmed.length, 200));
    return {
      summary,
      details: trimmed,
      containerName: deployInitMatch[1],
    };
  }

  // Pattern: Image pull failure
  const pullMatch = trimmed.match(/Failed to pull image '([^']+)'/i);
  if (pullMatch) {
    return {
      summary: `Failed to pull image '${pullMatch[1]}'`,
      details: trimmed,
    };
  }

  // Pattern: Service failed
  const serviceMatch = trimmed.match(/Service '([^']+)' failed/i);
  if (serviceMatch) {
    return {
      summary: `Service '${serviceMatch[1]}' failed to start`,
      details: trimmed,
      containerName: serviceMatch[1],
    };
  }

  // Generic: split at first stack trace marker or take first line
  const stackTraceIdx = trimmed.indexOf(' --- End of stack trace');
  const atIdx = trimmed.indexOf(' at ');
  const splitIdx = stackTraceIdx > 0 ? stackTraceIdx : (atIdx > 0 ? atIdx : -1);

  if (splitIdx > 0 && splitIdx < 300) {
    return {
      summary: trimmed.substring(0, splitIdx).trim(),
      details: trimmed,
    };
  }

  // Fallback: first line or first 200 chars
  const firstNewline = trimmed.indexOf('\n');
  if (firstNewline > 0 && firstNewline < 300) {
    return {
      summary: trimmed.substring(0, firstNewline).trim(),
      details: trimmed.length > firstNewline + 10 ? trimmed : '',
    };
  }

  // Short error — no details needed
  if (trimmed.length <= 200) {
    return {
      summary: trimmed,
      details: '',
    };
  }

  return {
    summary: trimmed.substring(0, 200) + '...',
    details: trimmed,
  };
}
