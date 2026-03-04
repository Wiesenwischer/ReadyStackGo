import { useState, useEffect, useCallback } from 'react';
import { getWizardRegistry, setSources, type WizardRegistrySource } from '../api/wizard';

export interface UseOnboardingSourcesStoreReturn {
  sources: WizardRegistrySource[];
  selectedIds: Set<string>;
  error: string;
  isSubmitting: boolean;
  isFetching: boolean;
  toggleSource: (id: string) => void;
  submit: () => Promise<boolean>;
}

export function useOnboardingSourcesStore(): UseOnboardingSourcesStoreReturn {
  const [sources, setSourcesState] = useState<WizardRegistrySource[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isFetching, setIsFetching] = useState(true);

  useEffect(() => {
    const fetchRegistry = async () => {
      try {
        const entries = await getWizardRegistry();
        setSourcesState(entries);
        const featured = new Set(entries.filter(e => e.featured).map(e => e.id));
        setSelectedIds(featured);
      } catch {
        setError('Failed to load available sources');
      } finally {
        setIsFetching(false);
      }
    };
    fetchRegistry();
  }, []);

  const toggleSource = useCallback((id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

  const submit = useCallback(async (): Promise<boolean> => {
    if (isSubmitting) return false;
    setError('');
    setIsSubmitting(true);
    try {
      await setSources({ registrySourceIds: Array.from(selectedIds) });
      return true;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add sources');
      return false;
    } finally {
      setIsSubmitting(false);
    }
  }, [isSubmitting, selectedIds]);

  return {
    sources,
    selectedIds,
    error,
    isSubmitting,
    isFetching,
    toggleSource,
    submit,
  };
}
