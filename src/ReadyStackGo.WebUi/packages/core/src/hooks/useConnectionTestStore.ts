import { useState, useCallback } from 'react';
import { apiPost } from '../api/client';

export interface TestConnectionResponse {
  success: boolean;
  message: string;
  serverVersion?: string;
}

export interface UseConnectionTestStoreReturn {
  isTesting: boolean;
  testResult: TestConnectionResponse | null;
  testConnection: (connectionString: string) => Promise<void>;
  clearResult: () => void;
}

export function useConnectionTestStore(): UseConnectionTestStoreReturn {
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState<TestConnectionResponse | null>(null);

  const testConnection = useCallback(async (connectionString: string) => {
    setIsTesting(true);
    setTestResult(null);

    try {
      const result = await apiPost<TestConnectionResponse>('/api/connections/test/sqlserver', {
        connectionString,
      });
      setTestResult(result);
    } catch (err) {
      setTestResult({
        success: false,
        message: err instanceof Error ? err.message : 'Connection test failed',
      });
    } finally {
      setIsTesting(false);
    }
  }, []);

  const clearResult = useCallback(() => {
    setTestResult(null);
  }, []);

  return { isTesting, testResult, testConnection, clearResult };
}
