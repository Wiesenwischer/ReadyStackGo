import { useState, useEffect, useRef, useCallback } from 'react';
import { containerApi } from '../api/containers';
import { useContainerLogsHub, type ConnectionState } from '../realtime/useContainerLogsHub';

const MAX_LINES = 10000;
const TAIL_OPTIONS = [100, 500, 1000] as const;

export type TailOption = typeof TAIL_OPTIONS[number];

export interface UseContainerLogsStoreReturn {
  lines: string[];
  loading: boolean;
  error: string | null;
  tail: number;
  setTail: (value: number) => void;
  streamEnded: boolean;
  connectionState: ConnectionState;
  tailOptions: readonly number[];
}

export function useContainerLogsStore(
  token: string | null,
  environmentId: string | undefined,
  containerId: string | undefined,
): UseContainerLogsStoreReturn {
  const [lines, setLines] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tail, setTail] = useState<number>(500);
  const [streamEnded, setStreamEnded] = useState(false);
  const isSubscribedRef = useRef(false);

  const handleLogLine = useCallback((_containerId: string, logLine: string) => {
    setLines(prev => {
      const next = [...prev, logLine];
      return next.length > MAX_LINES ? next.slice(next.length - MAX_LINES) : next;
    });
  }, []);

  const handleStreamEnded = useCallback(() => {
    setStreamEnded(true);
  }, []);

  const { connectionState, subscribeToContainerLogs, unsubscribeFromContainerLogs } = useContainerLogsHub(token, {
    onLogLine: handleLogLine,
    onStreamEnded: handleStreamEnded,
  });

  // Fetch initial logs
  useEffect(() => {
    if (!environmentId || !containerId) return;

    let cancelled = false;

    const fetchLogs = async () => {
      setLoading(true);
      setError(null);
      setStreamEnded(false);

      try {
        const logText = await containerApi.getLogs(environmentId, containerId, tail);
        if (cancelled) return;
        const logLines = logText ? logText.split('\n') : [];
        setLines(logLines);
      } catch (err) {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : 'Failed to load logs');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    fetchLogs();

    return () => {
      cancelled = true;
    };
  }, [environmentId, containerId, tail]);

  // Subscribe to SignalR stream once connected
  useEffect(() => {
    if (connectionState !== 'connected' || !environmentId || !containerId) return;
    if (isSubscribedRef.current) return;

    isSubscribedRef.current = true;
    subscribeToContainerLogs(environmentId, containerId).catch(err => {
      console.error('Failed to subscribe to container logs:', err);
    });

    return () => {
      if (isSubscribedRef.current) {
        isSubscribedRef.current = false;
        unsubscribeFromContainerLogs(containerId).catch(() => {});
      }
    };
  }, [connectionState, environmentId, containerId, subscribeToContainerLogs, unsubscribeFromContainerLogs]);

  return {
    lines,
    loading,
    error,
    tail,
    setTail,
    streamEnded,
    connectionState,
    tailOptions: TAIL_OPTIONS,
  };
}
