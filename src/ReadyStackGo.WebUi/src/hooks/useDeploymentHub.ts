import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from '../context/AuthContext';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

// Backend payload gets converted to camelCase by SignalR JSON serialization
export interface DeploymentProgressUpdate {
  sessionId: string;
  phase: string;
  message: string;
  percentComplete: number;
  currentService?: string;
  totalServices: number;
  completedServices: number;
  totalInitContainers: number;
  completedInitContainers: number;
  status: string;
  // Added by frontend handlers
  isComplete?: boolean;
  isError?: boolean;
  errorMessage?: string;
}

export interface UseDeploymentHubOptions {
  onDeploymentProgress?: (update: DeploymentProgressUpdate) => void;
  onConnectionStateChanged?: (state: ConnectionState) => void;
}

export interface UseDeploymentHubReturn {
  connectionState: ConnectionState;
  subscribeToDeployment: (sessionId: string) => Promise<void>;
  unsubscribeFromDeployment: (sessionId: string) => Promise<void>;
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

    // Set up event handlers for deployment progress
    connection.on('DeploymentProgress', (payload: DeploymentProgressUpdate) => {
      optionsRef.current.onDeploymentProgress?.(payload);
    });

    connection.on('DeploymentCompleted', (payload: DeploymentProgressUpdate) => {
      const update: DeploymentProgressUpdate = {
        ...payload,
        isComplete: true,
        isError: false
      };
      optionsRef.current.onDeploymentProgress?.(update);
    });

    connection.on('DeploymentFailed', (payload: DeploymentProgressUpdate) => {
      const update: DeploymentProgressUpdate = {
        ...payload,
        isComplete: true,
        isError: true
      };
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
  const subscribeToDeployment = useCallback(async (sessionId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('SubscribeToDeployment', sessionId);
    }
  }, []);

  const unsubscribeFromDeployment = useCallback(async (sessionId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('UnsubscribeFromDeployment', sessionId);
    }
  }, []);

  return {
    connectionState,
    subscribeToDeployment,
    unsubscribeFromDeployment
  };
}
