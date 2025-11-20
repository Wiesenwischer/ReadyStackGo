import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import { getEnvironments, type EnvironmentResponse } from '../api/environments';

interface EnvironmentContextType {
  environments: EnvironmentResponse[];
  activeEnvironment: EnvironmentResponse | null;
  setActiveEnvironment: (id: string) => void;
  refreshEnvironments: () => Promise<void>;
  isLoading: boolean;
}

const EnvironmentContext = createContext<EnvironmentContextType | null>(null);

const ACTIVE_ENV_KEY = 'rsgo_active_environment';

export function EnvironmentProvider({ children }: { children: ReactNode }) {
  const [environments, setEnvironments] = useState<EnvironmentResponse[]>([]);
  const [activeEnvironment, setActiveEnv] = useState<EnvironmentResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const refreshEnvironments = async () => {
    try {
      const response = await getEnvironments();
      if (response.success) {
        setEnvironments(response.environments);

        // Restore active environment from localStorage or use default
        const savedEnvId = localStorage.getItem(ACTIVE_ENV_KEY);
        let activeEnv: EnvironmentResponse | undefined;

        if (savedEnvId) {
          activeEnv = response.environments.find(e => e.id === savedEnvId);
        }

        if (!activeEnv) {
          // Fall back to default environment
          activeEnv = response.environments.find(e => e.isDefault);
        }

        if (!activeEnv && response.environments.length > 0) {
          // Fall back to first environment
          activeEnv = response.environments[0];
        }

        if (activeEnv) {
          setActiveEnv(activeEnv);
          localStorage.setItem(ACTIVE_ENV_KEY, activeEnv.id);
        }
      }
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
    const env = environments.find(e => e.id === id);
    if (env) {
      setActiveEnv(env);
      localStorage.setItem(ACTIVE_ENV_KEY, id);
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
