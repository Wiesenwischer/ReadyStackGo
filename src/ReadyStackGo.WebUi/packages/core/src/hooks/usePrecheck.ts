import { useState, useCallback } from 'react';
import { runPrecheck, type PrecheckResponse } from '../api/precheck';

export type PrecheckState = 'idle' | 'checking' | 'done' | 'error';

export interface UsePrecheckReturn {
  precheckState: PrecheckState;
  precheckResult: PrecheckResponse | null;
  precheckError: string;
  runPrecheckCheck: (environmentId: string, stackId: string, stackName: string, variables: Record<string, string>) => Promise<PrecheckResponse | null>;
  resetPrecheck: () => void;
}

export function usePrecheck(): UsePrecheckReturn {
  const [precheckState, setPrecheckState] = useState<PrecheckState>('idle');
  const [precheckResult, setPrecheckResult] = useState<PrecheckResponse | null>(null);
  const [precheckError, setPrecheckError] = useState('');

  const runPrecheckCheck = useCallback(async (
    environmentId: string,
    stackId: string,
    stackName: string,
    variables: Record<string, string>
  ): Promise<PrecheckResponse | null> => {
    setPrecheckState('checking');
    setPrecheckError('');
    setPrecheckResult(null);

    try {
      const result = await runPrecheck(environmentId, stackId, { stackName, variables });
      setPrecheckResult(result);
      setPrecheckState('done');
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Precheck failed';
      setPrecheckError(message);
      setPrecheckState('error');
      return null;
    }
  }, []);

  const resetPrecheck = useCallback(() => {
    setPrecheckState('idle');
    setPrecheckResult(null);
    setPrecheckError('');
  }, []);

  return {
    precheckState,
    precheckResult,
    precheckError,
    runPrecheckCheck,
    resetPrecheck,
  };
}
