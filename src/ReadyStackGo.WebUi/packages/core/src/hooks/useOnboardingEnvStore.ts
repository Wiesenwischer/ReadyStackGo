import { useState, useEffect, useCallback } from 'react';
import { getWizardStatus } from '../api/wizard';
import { createEnvironment } from '../api/environments';

export interface UseOnboardingEnvStoreReturn {
  name: string;
  socketPath: string;
  error: string;
  isSubmitting: boolean;
  setName: (value: string) => void;
  setSocketPath: (value: string) => void;
  submit: () => Promise<boolean>;
}

export function useOnboardingEnvStore(): UseOnboardingEnvStoreReturn {
  const [name, setName] = useState('Local Docker');
  const [socketPath, setSocketPath] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    const fetchDefaultSocketPath = async () => {
      try {
        const status = await getWizardStatus();
        setSocketPath(status.defaultDockerSocketPath || 'unix:///var/run/docker.sock');
      } catch {
        setSocketPath('unix:///var/run/docker.sock');
      }
    };
    fetchDefaultSocketPath();
  }, []);

  const submit = useCallback(async (): Promise<boolean> => {
    setError('');
    setIsSubmitting(true);

    try {
      const response = await createEnvironment({
        name: name.trim(),
        type: 'DockerSocket',
        socketPath: socketPath.trim(),
      });
      if (response.success) {
        return true;
      } else {
        setError(response.message || 'Failed to create environment');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create environment');
      return false;
    } finally {
      setIsSubmitting(false);
    }
  }, [name, socketPath]);

  return {
    name,
    socketPath,
    error,
    isSubmitting,
    setName,
    setSocketPath,
    submit,
  };
}
