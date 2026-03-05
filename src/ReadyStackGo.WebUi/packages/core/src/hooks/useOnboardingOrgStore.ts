import { useState, useCallback } from 'react';
import { createOrganization } from '../api/organizations';

export interface UseOnboardingOrgStoreReturn {
  id: string;
  name: string;
  error: string;
  isSubmitting: boolean;
  setId: (value: string) => void;
  setName: (value: string) => void;
  submit: () => Promise<boolean>;
}

export function useOnboardingOrgStore(): UseOnboardingOrgStoreReturn {
  const [id, setIdState] = useState('');
  const [name, setName] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const setId = useCallback((value: string) => {
    setIdState(value.toLowerCase());
  }, []);

  const submit = useCallback(async (): Promise<boolean> => {
    setError('');

    if (id.length < 2) {
      setError('Organization ID must be at least 2 characters long');
      return false;
    }

    if (!/^[a-z0-9-]+$/.test(id)) {
      setError('Organization ID can only contain lowercase letters, numbers, and hyphens');
      return false;
    }

    if (name.trim().length < 3) {
      setError('Organization name must be at least 3 characters long');
      return false;
    }

    setIsSubmitting(true);
    try {
      const response = await createOrganization({ id, name: name.trim() });
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
      setIsSubmitting(false);
    }
  }, [id, name]);

  return {
    id,
    name,
    error,
    isSubmitting,
    setId,
    setName,
    submit,
  };
}
