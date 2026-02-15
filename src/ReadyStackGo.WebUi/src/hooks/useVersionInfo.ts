import { useState, useEffect, useCallback, useRef } from 'react';
import { systemApi, type VersionInfo } from '../api/system';

// Module-level shared state (singleton pattern)
let sharedData: VersionInfo | null = null;
let sharedPromise: Promise<VersionInfo> | null = null;
const listeners = new Set<() => void>();

function notifyListeners() {
  listeners.forEach(listener => listener());
}

async function fetchVersion(forceCheck: boolean = false): Promise<VersionInfo> {
  if (forceCheck) {
    sharedData = null;
    sharedPromise = null;
  }

  if (sharedData && !forceCheck) {
    return sharedData;
  }

  if (sharedPromise && !forceCheck) {
    return sharedPromise;
  }

  sharedPromise = systemApi.getVersion(forceCheck).then(data => {
    sharedData = data;
    sharedPromise = null;
    notifyListeners();
    return data;
  }).catch(error => {
    sharedPromise = null;
    throw error;
  });

  return sharedPromise;
}

export interface UseVersionInfoReturn {
  versionInfo: VersionInfo | null;
  isLoading: boolean;
  error: string | null;
  refetch: (forceCheck?: boolean) => Promise<void>;
}

export function useVersionInfo(): UseVersionInfoReturn {
  const [versionInfo, setVersionInfo] = useState<VersionInfo | null>(sharedData);
  const [isLoading, setIsLoading] = useState(!sharedData);
  const [error, setError] = useState<string | null>(null);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;

    const listener = () => {
      if (mountedRef.current && sharedData) {
        setVersionInfo(sharedData);
        setIsLoading(false);
      }
    };
    listeners.add(listener);

    if (!sharedData) {
      fetchVersion().then(data => {
        if (mountedRef.current) {
          setVersionInfo(data);
          setIsLoading(false);
        }
      }).catch(err => {
        if (mountedRef.current) {
          setError(err instanceof Error ? err.message : 'Failed to load version info');
          setIsLoading(false);
        }
      });
    }

    return () => {
      mountedRef.current = false;
      listeners.delete(listener);
    };
  }, []);

  const refetch = useCallback(async (forceCheck: boolean = false) => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await fetchVersion(forceCheck);
      if (mountedRef.current) {
        setVersionInfo(data);
        setIsLoading(false);
      }
    } catch (err) {
      if (mountedRef.current) {
        setError(err instanceof Error ? err.message : 'Failed to load version info');
        setIsLoading(false);
      }
    }
  }, []);

  return { versionInfo, isLoading, error, refetch };
}
