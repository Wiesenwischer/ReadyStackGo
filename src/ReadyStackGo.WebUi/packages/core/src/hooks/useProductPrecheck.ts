import { useState, useCallback } from 'react';
import { runProductPrecheck, type ProductPrecheckResponse, type ProductPrecheckStackConfig } from '../api/precheck';

export type ProductPrecheckState = 'idle' | 'checking' | 'done' | 'error';

export interface UseProductPrecheckReturn {
  precheckState: ProductPrecheckState;
  precheckResult: ProductPrecheckResponse | null;
  precheckError: string;
  runProductPrecheckCheck: (
    environmentId: string,
    productId: string,
    deploymentName: string,
    stackConfigs: ProductPrecheckStackConfig[],
    sharedVariables: Record<string, string>
  ) => Promise<ProductPrecheckResponse | null>;
  resetPrecheck: () => void;
}

export function useProductPrecheck(): UseProductPrecheckReturn {
  const [precheckState, setPrecheckState] = useState<ProductPrecheckState>('idle');
  const [precheckResult, setPrecheckResult] = useState<ProductPrecheckResponse | null>(null);
  const [precheckError, setPrecheckError] = useState('');

  const runProductPrecheckCheck = useCallback(async (
    environmentId: string,
    productId: string,
    deploymentName: string,
    stackConfigs: ProductPrecheckStackConfig[],
    sharedVariables: Record<string, string>
  ): Promise<ProductPrecheckResponse | null> => {
    setPrecheckState('checking');
    setPrecheckError('');
    setPrecheckResult(null);

    try {
      const result = await runProductPrecheck(environmentId, {
        productId,
        deploymentName,
        stackConfigs,
        sharedVariables,
      });
      setPrecheckResult(result);
      setPrecheckState('done');
      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Product precheck failed';
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
    runProductPrecheckCheck,
    resetPrecheck,
  };
}
