import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import {
  type EnvironmentResponse,
  loadEnvironments,
  resolveActiveEnvironment,
  selectEnvironment,
} from '@rsgo/core';

interface EnvironmentContextType {
  environments: EnvironmentResponse[];
  activeEnvironment: EnvironmentResponse | null;
  setActiveEnvironment: (id: string) => void;
  refreshEnvironments: () => Promise<void>;
  isLoading: boolean;
}

const EnvironmentContext = createContext<EnvironmentContextType | null>(null);

export function EnvironmentProvider({ children }: { children: ReactNode }) {
  const [environments, setEnvironments] = useState<EnvironmentResponse[]>([]);
  const [activeEnvironment, setActiveEnv] = useState<EnvironmentResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const refreshEnvironments = async () => {
    try {
      const envs = await loadEnvironments();
      setEnvironments(envs);
      const active = resolveActiveEnvironment(envs);
      setActiveEnv(active);
    } catch (error) {
      console.error('Failed to load environments:', error);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    refreshEnvironments();
  }, []);

  const setActiveEnvironment = (id: string) => {
    const env = selectEnvironment(environments, id);
    if (env) {
      setActiveEnv(env);
    }
  };

  return (
    <EnvironmentContext.Provider
      value={{
        environments,
        activeEnvironment,
        setActiveEnvironment,
        refreshEnvironments,
        isLoading,
      }}
    >
      {children}
    </EnvironmentContext.Provider>
  );
}

export function useEnvironment() {
  const context = useContext(EnvironmentContext);
  if (!context) {
    throw new Error('useEnvironment must be used within an EnvironmentProvider');
  }
  return context;
}
