import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from '../context/AuthContext';
import type { StackHealthSummaryDto, StackHealthDto, EnvironmentHealthSummaryDto } from '../api/health';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface UseHealthHubOptions {
  onDeploymentHealthChanged?: (health: StackHealthSummaryDto) => void;
  onDeploymentDetailedHealthChanged?: (health: StackHealthDto) => void;
  onEnvironmentHealthChanged?: (summary: EnvironmentHealthSummaryDto) => void;
  onGlobalHealthChanged?: (health: StackHealthSummaryDto) => void;
  onConnectionStateChanged?: (state: ConnectionState) => void;
}

export interface UseHealthHubReturn {
  connectionState: ConnectionState;
  subscribeToEnvironment: (environmentId: string) => Promise<void>;
  unsubscribeFromEnvironment: (environmentId: string) => Promise<void>;
  subscribeToDeployment: (deploymentId: string) => Promise<void>;
  unsubscribeFromDeployment: (deploymentId: string) => Promise<void>;
  subscribeToAllHealth: () => Promise<void>;
  unsubscribeFromAllHealth: () => Promise<void>;
}

export function useHealthHub(options: UseHealthHubOptions = {}): UseHealthHubReturn {
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
    return `${baseUrl}/hubs/health`;
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
    connection.on('DeploymentHealthChanged', (health: StackHealthSummaryDto) => {
      optionsRef.current.onDeploymentHealthChanged?.(health);
    });

    connection.on('DeploymentDetailedHealthChanged', (health: StackHealthDto) => {
      optionsRef.current.onDeploymentDetailedHealthChanged?.(health);
    });

    connection.on('EnvironmentHealthChanged', (summary: EnvironmentHealthSummaryDto) => {
      optionsRef.current.onEnvironmentHealthChanged?.(summary);
    });

    connection.on('GlobalHealthChanged', (health: StackHealthSummaryDto) => {
      optionsRef.current.onGlobalHealthChanged?.(health);
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
        console.error('SignalR connection error:', err);
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
  const subscribeToEnvironment = useCallback(async (environmentId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('SubscribeToEnvironment', environmentId);
    }
  }, []);

  const unsubscribeFromEnvironment = useCallback(async (environmentId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('UnsubscribeFromEnvironment', environmentId);
    }
  }, []);

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

  const subscribeToAllHealth = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('SubscribeToAllHealth');
    }
  }, []);

  const unsubscribeFromAllHealth = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('UnsubscribeFromAllHealth');
    }
  }, []);

  return {
    connectionState,
    subscribeToEnvironment,
    unsubscribeFromEnvironment,
    subscribeToDeployment,
    unsubscribeFromDeployment,
    subscribeToAllHealth,
    unsubscribeFromAllHealth
  };
}
