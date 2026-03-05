import { useState, useCallback } from 'react';
import { createOrganization } from '../api/organizations';

export interface UseSetupOrganizationStoreReturn {
  name: string;
  setName: (value: string) => void;
  loading: boolean;
  error: string | null;
  submit: () => Promise<boolean>;
}

export function useSetupOrganizationStore(): UseSetupOrganizationStoreReturn {
  const [name, setName] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = useCallback(async (): Promise<boolean> => {
    if (!name.trim()) {
      setError('Organization name is required');
      return false;
    }

    try {
      setLoading(true);
      setError(null);
      const response = await createOrganization({ name: name.trim() });
      if (response.success) {
        return true;
      } else {
        setError('Failed to create organization');
        return false;
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create organization');
      return false;
    } finally {
      setLoading(false);
    }
  }, [name]);

  return {
    name,
    setName,
    loading,
    error,
    submit,
  };
}
