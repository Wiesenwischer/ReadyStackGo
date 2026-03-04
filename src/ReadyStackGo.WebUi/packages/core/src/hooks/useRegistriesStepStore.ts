import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  detectRegistries,
  checkRegistryAccess,
  verifyRegistryAccess,
  type DetectedRegistryArea,
  type RegistryInputDto,
} from '../api/wizard';

export type CardStatus = 'action-required' | 'verified' | 'skipped';
export type VerifyStatus = 'idle' | 'verifying' | 'success' | 'error';
export type InitialCheckStatus = 'pending' | 'checking' | 'done';

export interface RegistryCardState {
  area: DetectedRegistryArea;
  status: CardStatus;
  verifyStatus: VerifyStatus;
  verifyError: string;
  username: string;
  password: string;
  name: string;
  pattern: string;
  accessLevel: 'Public' | 'AuthRequired' | 'Unknown' | null;
  initialCheck: InitialCheckStatus;
}

export interface UseRegistriesStepStoreReturn {
  cards: RegistryCardState[];
  error: string;
  isFetching: boolean;
  checksComplete: boolean;
  verifiedCards: RegistryCardState[];
  actionCards: RegistryCardState[];
  skippedCards: RegistryCardState[];
  checksRunning: boolean;
  updateCard: (index: number, updates: Partial<RegistryCardState>) => void;
  handleVerify: (index: number) => Promise<void>;
  handleSkipCard: (index: number) => void;
  handleUndoSkip: (index: number) => void;
  handleUndoVerified: (index: number) => void;
  getRegistriesToSubmit: () => RegistryInputDto[];
}

export function useRegistriesStepStore(): UseRegistriesStepStoreReturn {
  const [cards, setCards] = useState<RegistryCardState[]>([]);
  const [error, setError] = useState('');
  const [isFetching, setIsFetching] = useState(true);
  const [checksComplete, setChecksComplete] = useState(false);

  useEffect(() => {
    const fetchAndCheck = async () => {
      try {
        // Phase 1: Load areas fast (no access check)
        const response = await detectRegistries();
        const cardStates: RegistryCardState[] = response.areas
          .filter(a => !a.isConfigured)
          .map(area => ({
            area,
            status: 'action-required' as const,
            verifyStatus: 'idle' as const,
            verifyError: '',
            username: '',
            password: '',
            name: area.suggestedName,
            pattern: area.suggestedPattern,
            accessLevel: null,
            initialCheck: area.images.length > 0 ? 'pending' as const : 'done' as const,
          }));
        setCards(cardStates);
        setIsFetching(false);

        // Phase 2: Check each area via backend (parallel, per-card updates)
        const checksToRun = cardStates
          .map((card, index) => ({ card, index }))
          .filter(({ card }) => card.area.images.length > 0);

        if (checksToRun.length === 0) {
          setChecksComplete(true);
          return;
        }

        // Mark all as checking
        setCards(prev => prev.map(c =>
          c.initialCheck === 'pending' ? { ...c, initialCheck: 'checking' as const } : c
        ));

        // Fire all checks in parallel, update each card as result arrives
        await Promise.all(checksToRun.map(async ({ card, index }) => {
          try {
            const result = await checkRegistryAccess(card.area.images[0]);
            setCards(prev => prev.map((c, i) => {
              if (i !== index) return c;
              if (result.accessLevel === 'Public') {
                return {
                  ...c,
                  initialCheck: 'done' as const,
                  status: 'verified' as const,
                  accessLevel: 'Public' as const,
                };
              }
              return {
                ...c,
                initialCheck: 'done' as const,
                accessLevel: result.accessLevel,
              };
            }));
          } catch {
            setCards(prev => prev.map((c, i) => i === index ? {
              ...c,
              initialCheck: 'done' as const,
              accessLevel: 'Unknown' as const,
            } : c));
          }
        }));

        setChecksComplete(true);
      } catch {
        setError('Failed to detect container registries');
        setIsFetching(false);
        setChecksComplete(true);
      }
    };
    fetchAndCheck();
  }, []);

  const updateCard = useCallback((index: number, updates: Partial<RegistryCardState>) => {
    setCards(prev => prev.map((card, i) => i === index ? { ...card, ...updates } : card));
  }, []);

  const handleVerify = useCallback(async (index: number) => {
    const card = cards[index];
    if (!card || card.area.images.length === 0) return;

    updateCard(index, { verifyStatus: 'verifying', verifyError: '' });

    try {
      const result = await verifyRegistryAccess({
        image: card.area.images[0],
        username: card.username || undefined,
        password: card.password || undefined,
      });

      if (result.accessLevel === 'Public') {
        updateCard(index, {
          verifyStatus: 'success',
          status: 'verified',
          accessLevel: (card.username && card.password) ? 'AuthRequired' : 'Public',
        });
      } else if (result.accessLevel === 'AuthRequired') {
        if (card.username && card.password) {
          updateCard(index, {
            verifyStatus: 'error',
            verifyError: 'Access denied — check your credentials',
            accessLevel: 'AuthRequired',
          });
        } else {
          updateCard(index, {
            verifyStatus: 'error',
            verifyError: 'Credentials required for this registry',
            accessLevel: 'AuthRequired',
          });
        }
      } else {
        updateCard(index, {
          verifyStatus: 'error',
          verifyError: 'Could not reach registry — try again later',
          accessLevel: 'Unknown',
        });
      }
    } catch {
      updateCard(index, {
        verifyStatus: 'error',
        verifyError: 'Network error — could not verify access',
      });
    }
  }, [cards, updateCard]);

  const handleSkipCard = useCallback((index: number) => {
    updateCard(index, { status: 'skipped' });
  }, [updateCard]);

  const handleUndoSkip = useCallback((index: number) => {
    updateCard(index, { status: 'action-required', verifyStatus: 'idle', verifyError: '' });
  }, [updateCard]);

  const handleUndoVerified = useCallback((index: number) => {
    updateCard(index, {
      status: 'action-required',
      verifyStatus: 'idle',
      verifyError: '',
      accessLevel: null,
    });
  }, [updateCard]);

  const getRegistriesToSubmit = useCallback((): RegistryInputDto[] => {
    return cards
      .filter(c => c.status === 'verified' && c.accessLevel === 'AuthRequired' && c.username && c.password)
      .map(card => ({
        name: card.name,
        host: card.area.host,
        pattern: card.pattern,
        requiresAuth: true,
        username: card.username,
        password: card.password,
      }));
  }, [cards]);

  const verifiedCards = useMemo(() => cards.filter(c => c.status === 'verified'), [cards]);
  const actionCards = useMemo(() => cards.filter(c => c.status === 'action-required' && c.initialCheck === 'done'), [cards]);
  const skippedCards = useMemo(() => cards.filter(c => c.status === 'skipped'), [cards]);
  const checksRunning = useMemo(() => cards.some(c => c.initialCheck === 'checking'), [cards]);

  return {
    cards,
    error,
    isFetching,
    checksComplete,
    verifiedCards,
    actionCards,
    skippedCards,
    checksRunning,
    updateCard,
    handleVerify,
    handleSkipCard,
    handleUndoSkip,
    handleUndoVerified,
    getRegistriesToSubmit,
  };
}
