import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type { UpdateProgressInfo } from '../api/system';

export type UpdateConnectionState = 'disconnected' | 'connecting' | 'connected';

export interface UseUpdateHubReturn {
  connectionState: UpdateConnectionState;
  progress: UpdateProgressInfo | null;
}

/**
 * Hook for connecting to the UpdateHub via SignalR.
 * No auth required — the update hub is anonymous.
 * On connect, receives the current progress immediately from the server.
 */
export function useUpdateHub(): UseUpdateHubReturn {
  const [connectionState, setConnectionState] = useState<UpdateConnectionState>('disconnected');
  const [progress, setProgress] = useState<UpdateProgressInfo | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const getHubUrl = useCallback(() => {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || '';
    return `${baseUrl}/hubs/update`;
  }, []);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(getHubUrl())
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext: signalR.RetryContext) => {
          return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 10000);
        }
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('UpdateProgress', (payload: UpdateProgressInfo) => {
      setProgress(payload);
    });

    connection.onreconnected(() => setConnectionState('connected'));
    connection.onclose(() => setConnectionState('disconnected'));

    const startConnection = async () => {
      setConnectionState('connecting');
      try {
        await connection.start();
        setConnectionState('connected');
      } catch (err) {
        console.error('UpdateHub connection error:', err);
        setConnectionState('disconnected');
      }
    };

    startConnection();

    return () => {
      connection.stop();
    };
  }, [getHubUrl]);

  return { connectionState, progress };
}
