import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface UseContainerLogsHubOptions {
  onLogLine?: (containerId: string, logLine: string) => void;
  onStreamEnded?: (containerId: string, reason: string) => void;
  onConnectionStateChanged?: (state: ConnectionState) => void;
}

export interface UseContainerLogsHubReturn {
  connectionState: ConnectionState;
  subscribeToContainerLogs: (environmentId: string, containerId: string) => Promise<void>;
  unsubscribeFromContainerLogs: (containerId: string) => Promise<void>;
}

export function useContainerLogsHub(token: string | null, options: UseContainerLogsHubOptions = {}): UseContainerLogsHubReturn {
  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected');
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const optionsRef = useRef(options);
  const subscribedContainerRef = useRef<{ environmentId: string; containerId: string } | null>(null);

  // Keep options ref up to date
  useEffect(() => {
    optionsRef.current = options;
  }, [options]);

  // Build SignalR hub URL
  const getHubUrl = useCallback(() => {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || '';
    return `${baseUrl}/hubs/container`;
  }, []);

  // Create and connect to hub
  useEffect(() => {
    if (!token) {
      setConnectionState('disconnected');
      return;
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(getHubUrl(), {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext: signalR.RetryContext) => {
          return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connectionRef.current = connection;

    // Set up event handlers
    connection.on('ContainerLogLine', (containerId: string, logLine: string) => {
      optionsRef.current.onLogLine?.(containerId, logLine);
    });

    connection.on('ContainerLogStreamEnded', (containerId: string, reason: string) => {
      optionsRef.current.onStreamEnded?.(containerId, reason);
    });

    // Connection state handlers
    connection.onreconnecting(() => {
      setConnectionState('reconnecting');
      optionsRef.current.onConnectionStateChanged?.('reconnecting');
    });

    connection.onreconnected(async () => {
      setConnectionState('connected');
      optionsRef.current.onConnectionStateChanged?.('connected');
      // Re-subscribe to container logs after reconnection
      const sub = subscribedContainerRef.current;
      if (sub) {
        try {
          await connection.invoke('SubscribeToContainerLogs', sub.environmentId, sub.containerId);
        } catch (err) {
          console.error('Failed to re-subscribe to container logs after reconnection:', err);
        }
      }
    });

    connection.onclose(() => {
      setConnectionState('disconnected');
      optionsRef.current.onConnectionStateChanged?.('disconnected');
    });

    // Start connection
    const startConnection = async () => {
      setConnectionState('connecting');
      optionsRef.current.onConnectionStateChanged?.('connecting');

      try {
        await connection.start();
        setConnectionState('connected');
        optionsRef.current.onConnectionStateChanged?.('connected');
      } catch (err) {
        console.error('SignalR container hub connection error:', err);
        setConnectionState('disconnected');
        optionsRef.current.onConnectionStateChanged?.('disconnected');
      }
    };

    startConnection();

    // Cleanup on unmount
    return () => {
      subscribedContainerRef.current = null;
      connection.stop();
    };
  }, [token, getHubUrl]);

  // Subscription methods
  const subscribeToContainerLogs = useCallback(async (environmentId: string, containerId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      subscribedContainerRef.current = { environmentId, containerId };
      await connectionRef.current.invoke('SubscribeToContainerLogs', environmentId, containerId);
    }
  }, []);

  const unsubscribeFromContainerLogs = useCallback(async (containerId: string) => {
    subscribedContainerRef.current = null;
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('UnsubscribeFromContainerLogs', containerId);
    }
  }, []);

  return {
    connectionState,
    subscribeToContainerLogs,
    unsubscribeFromContainerLogs
  };
}
