import { useState, useEffect } from 'react';
import { getOnboardingStatus } from '../api/onboarding';

export interface UseSetupHintStoreReturn {
  hints: string[];
  loading: boolean;
}

export function useSetupHintStore(): UseSetupHintStoreReturn {
  const [hints, setHints] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const checkStatus = async () => {
      try {
        const status = await getOnboardingStatus();
        const missing: string[] = [];
        if (!status.stackSources.done) missing.push('stack sources');
        if (!status.registries.done) missing.push('container registries');
        setHints(missing);
      } catch {
        // Non-critical — don't show anything on error
      } finally {
        setLoading(false);
      }
    };
    checkStatus();
  }, []);

  return { hints, loading };
}
