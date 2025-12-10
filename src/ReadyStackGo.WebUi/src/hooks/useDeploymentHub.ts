import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from '../context/AuthContext';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface DeploymentProgressUpdate {
  deploymentId: string;
  phase: string;
  message: string;
  progressPercent: number;
  currentService?: string;
  totalServices: number;
  completedServices: number;
  isComplete: boolean;
  isError: boolean;
  errorMessage?: string;
}

export interface UseDeploymentHubOptions {
  onDeploymentProgress?: (update: DeploymentProgressUpdate) => void;
  onConnectionStateChanged?: (state: ConnectionState) => void;
}

export interface UseDeploymentHubReturn {
  connectionState: ConnectionState;
  subscribeToDeployment: (deploymentId: string) => Promise<void>;
  unsubscribeFromDeployment: (deploymentId: string) => Promise<void>;
  subscribeToAllDeployments: () => Promise<void>;
  unsubscribeFromAllDeployments: () => Promise<void>;
}

export function useDeploymentHub(options: UseDeploymentHubOptions = {}): UseDeploymentHubReturn {
  const { token } = useAuth();
  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected');
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const optionsRef = useRef(options);

  // Keep options ref up to date
  useEffect(() => {
    optionsRef.current = options;
  }, [options]);

  // Build SignalR hub URL
  const getHubUrl = useCallback(() => {
    const baseUrl = import.meta.env.VITE_API_BASE_URL || '';
    return `${baseUrl}/hubs/deployment`;
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
          // Exponential backoff: 0s, 2s, 4s, 8s, max 30s
          return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connectionRef.current = connection;

    // Set up event handlers
    connection.on('DeploymentProgress', (update: DeploymentProgressUpdate) => {
      optionsRef.current.onDeploymentProgress?.(update);
    });

    // Connection state handlers
    connection.onreconnecting(() => {
      setConnectionState('reconnecting');
      optionsRef.current.onConnectionStateChanged?.('reconnecting');
    });

    connection.onreconnected(() => {
      setConnectionState('connected');
      optionsRef.current.onConnectionStateChanged?.('connected');
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
        console.error('SignalR deployment hub connection error:', err);
        setConnectionState('disconnected');
        optionsRef.current.onConnectionStateChanged?.('disconnected');
      }
    };

    startConnection();

    // Cleanup on unmount
    return () => {
      connection.stop();
    };
  }, [token, getHubUrl]);

  // Subscription methods
  const subscribeToDeployment = useCallback(async (deploymentId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('SubscribeToDeployment', deploymentId);
    }
  }, []);

  const unsubscribeFromDeployment = useCallback(async (deploymentId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('UnsubscribeFromDeployment', deploymentId);
    }
  }, []);

  const subscribeToAllDeployments = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('SubscribeToAllDeployments');
    }
  }, []);

  const unsubscribeFromAllDeployments = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('UnsubscribeFromAllDeployments');
    }
  }, []);

  return {
    connectionState,
    subscribeToDeployment,
    unsubscribeFromDeployment,
    subscribeToAllDeployments,
    unsubscribeFromAllDeployments
  };
}
