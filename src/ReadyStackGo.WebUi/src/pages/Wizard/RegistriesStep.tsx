import { useState, useEffect } from 'react';
import { detectRegistries, checkRegistryAccess, verifyRegistryAccess, type DetectedRegistryArea, type RegistryInputDto } from '../../api/wizard';

type CardStatus = 'action-required' | 'verified' | 'skipped';
type VerifyStatus = 'idle' | 'verifying' | 'success' | 'error';
type InitialCheckStatus = 'pending' | 'checking' | 'done';

interface RegistryCardState {
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

interface RegistriesStepProps {
  onNext: (registries: RegistryInputDto[]) => Promise<void>;
}

// Icons as reusable components
function SpinnerIcon({ className }: { className?: string }) {
  return (
    <svg className={`animate-spin ${className ?? 'h-8 w-8 text-brand-600 dark:text-brand-400'}`} fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
    </svg>
  );
}

function CheckIcon({ className }: { className?: string }) {
  return (
    <svg className={className ?? 'w-4 h-4'} fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
    </svg>
  );
}

function LockIcon({ className }: { className?: string }) {
  return (
    <svg className={className ?? 'w-4 h-4'} fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
    </svg>
  );
}

function QuestionIcon({ className }: { className?: string }) {
  return (
    <svg className={className ?? 'w-4 h-4'} fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
    </svg>
  );
}

function ShieldCheckIcon({ className }: { className?: string }) {
  return (
    <svg className={className ?? 'w-4 h-4'} fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
    </svg>
  );
}

export default function RegistriesStep({ onNext }: RegistriesStepProps) {
  const [cards, setCards] = useState<RegistryCardState[]>([]);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSkipping, setIsSkipping] = useState(false);
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

  const updateCard = (index: number, updates: Partial<RegistryCardState>) => {
    setCards(prev => prev.map((card, i) => i === index ? { ...card, ...updates } : card));
  };

  const handleVerify = async (index: number) => {
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
          accessLevel: 'Public',
        });
      } else if (result.accessLevel === 'AuthRequired') {
        if (card.username && card.password) {
          // Had credentials but they didn't work
          updateCard(index, {
            verifyStatus: 'error',
            verifyError: 'Access denied — check your credentials',
            accessLevel: 'AuthRequired',
          });
        } else {
          // No credentials provided, registry requires auth
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
  };

  const handleSkipCard = (index: number) => {
    updateCard(index, { status: 'skipped' });
  };

  const handleUndoSkip = (index: number) => {
    updateCard(index, { status: 'action-required', verifyStatus: 'idle', verifyError: '' });
  };

  const handleUndoVerified = (index: number) => {
    updateCard(index, {
      status: 'action-required',
      verifyStatus: 'idle',
      verifyError: '',
      accessLevel: null,
    });
  };

  const handleSubmit = async () => {
    setError('');
    setIsLoading(true);
    try {
      // Submit verified cards that have credentials (authenticated registries)
      const registries: RegistryInputDto[] = cards
        .filter(c => c.status === 'verified' && c.accessLevel === 'AuthRequired' && c.username && c.password)
        .map(card => ({
          name: card.name,
          host: card.area.host,
          pattern: card.pattern,
          requiresAuth: true,
          username: card.username,
          password: card.password,
        }));
      await onNext(registries);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to configure registries');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSkipAll = async () => {
    setIsSkipping(true);
    try {
      await onNext([]);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to skip step');
    } finally {
      setIsSkipping(false);
    }
  };

  const verifiedCards = cards.filter(c => c.status === 'verified');
  const actionCards = cards.filter(c => c.status === 'action-required' && c.initialCheck === 'done');
  const skippedCards = cards.filter(c => c.status === 'skipped');
  const checksRunning = cards.some(c => c.initialCheck === 'checking');

  // Phase 0: initial detection
  if (isFetching) {
    return (
      <div className="flex flex-col items-center justify-center py-12 gap-3">
        <SpinnerIcon />
        <p className="text-sm text-gray-500 dark:text-gray-400">Detecting container registries...</p>
      </div>
    );
  }

  // Phase 1: checking access per area
  if (!checksComplete) {
    const done = cards.filter(c => c.initialCheck === 'done').length;
    const total = cards.length;
    return (
      <div className="flex flex-col items-center justify-center py-12 gap-4">
        <SpinnerIcon />
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Checking registry access... ({done}/{total})
        </p>
        <div className="w-full max-w-sm space-y-1">
          {cards.map(card => (
            <div key={`${card.area.host}-${card.area.namespace}`} className="flex items-center gap-2 text-xs">
              {card.initialCheck === 'done' ? (
                <CheckIcon className="w-3.5 h-3.5 text-green-500 flex-shrink-0" />
              ) : card.initialCheck === 'checking' ? (
                <SpinnerIcon className="w-3.5 h-3.5 text-blue-500 flex-shrink-0" />
              ) : (
                <div className="w-3.5 h-3.5 rounded-full border border-gray-300 dark:border-gray-600 flex-shrink-0" />
              )}
              <span className={`truncate ${card.initialCheck === 'done' ? 'text-gray-400 dark:text-gray-500' : 'text-gray-600 dark:text-gray-300'}`}>
                {card.area.host}{card.area.namespace && card.area.namespace !== '*' ? ` / ${card.area.namespace}` : ''}
              </span>
            </div>
          ))}
        </div>
      </div>
    );
  }

  // Phase 2: no registries detected at all
  if (cards.length === 0) {
    return (
      <div>
        <div className="mb-6">
          <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">Container Registries</h2>
          <p className="text-sm text-gray-500 dark:text-gray-400">No container registries detected. You can configure registries later in Settings.</p>
        </div>
        <div className="pt-6">
          <button type="button" onClick={handleSubmit} disabled={isLoading}
            className="w-full py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed">
            {isLoading ? 'Continuing...' : 'Continue'}
          </button>
        </div>
      </div>
    );
  }

  // Phase 3: Main view — two-column layout
  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">Container Registries</h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          {actionCards.length > 0
            ? 'Verify access for each registry. Enter credentials or confirm public access.'
            : 'All registries verified — you can continue.'}
        </p>
      </div>

      {error && (
        <div className="mb-4 p-4 text-sm border border-red-300 rounded-lg bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Left Column: Action Required */}
        <div className="space-y-3">
          <div className="flex items-center gap-2 mb-2">
            <div className="w-2 h-2 rounded-full bg-amber-500" />
            <h3 className="text-sm font-medium text-gray-600 dark:text-gray-400">
              Action Required {actionCards.length > 0 && <span className="text-gray-400 dark:text-gray-500">({actionCards.length})</span>}
            </h3>
          </div>

          {actionCards.length === 0 && (
            <div className="p-4 rounded-lg border border-dashed border-gray-200 dark:border-gray-700 text-center">
              <CheckIcon className="mx-auto w-6 h-6 text-green-400 dark:text-green-500 mb-1" />
              <p className="text-xs text-gray-400 dark:text-gray-500">All done!</p>
            </div>
          )}

          {cards.map((card, index) => {
            if (card.status !== 'action-required' || card.initialCheck !== 'done') return null;
            return (
              <div key={`${card.area.host}-${card.area.namespace}`}
                className={`rounded-lg border transition-colors ${
                  card.verifyStatus === 'error'
                    ? 'border-red-300 dark:border-red-700'
                    : card.accessLevel === 'Unknown'
                      ? 'border-gray-300 dark:border-gray-600'
                      : 'border-amber-300 dark:border-amber-700'
                }`}>
                {/* Card Header */}
                <div className="flex items-center gap-3 p-3">
                  <div className={`flex-shrink-0 w-7 h-7 rounded-full flex items-center justify-center ${
                    card.accessLevel === 'Unknown' ? 'bg-gray-100 dark:bg-gray-800' : 'bg-amber-100 dark:bg-amber-900/30'
                  }`}>
                    {card.accessLevel === 'Unknown'
                      ? <QuestionIcon className="w-3.5 h-3.5 text-gray-500 dark:text-gray-400" />
                      : <LockIcon className="w-3.5 h-3.5 text-amber-600 dark:text-amber-400" />
                    }
                  </div>
                  <div className="min-w-0 flex-1">
                    <span className="font-medium text-sm text-gray-800 dark:text-white truncate block">
                      {card.area.host}
                      {card.area.namespace && card.area.namespace !== '*' && (
                        <span className="text-gray-500 dark:text-gray-400"> / {card.area.namespace}</span>
                      )}
                    </span>
                    <p className="text-xs text-gray-400 dark:text-gray-500">
                      {card.area.images.length} image{card.area.images.length !== 1 ? 's' : ''}
                    </p>
                  </div>
                </div>

                {/* Credential Fields */}
                <div className="px-3 pb-3 border-t border-gray-100 dark:border-gray-700/50 space-y-2">
                  <div className="mt-2 grid grid-cols-2 gap-2">
                    <input
                      type="text"
                      value={card.username}
                      onChange={e => updateCard(index, { username: e.target.value, verifyStatus: 'idle', verifyError: '' })}
                      placeholder="Username"
                      disabled={card.verifyStatus === 'verifying'}
                      className="w-full px-2 py-1.5 text-xs rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-800 dark:text-white placeholder-gray-400 focus:ring-2 focus:ring-brand-500/50 focus:border-brand-500 disabled:opacity-50"
                    />
                    <input
                      type="password"
                      value={card.password}
                      onChange={e => updateCard(index, { password: e.target.value, verifyStatus: 'idle', verifyError: '' })}
                      placeholder="Password / Token"
                      disabled={card.verifyStatus === 'verifying'}
                      className="w-full px-2 py-1.5 text-xs rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-800 dark:text-white placeholder-gray-400 focus:ring-2 focus:ring-brand-500/50 focus:border-brand-500 disabled:opacity-50"
                    />
                  </div>

                  {/* Verify Error */}
                  {card.verifyStatus === 'error' && card.verifyError && (
                    <p className="text-xs text-red-600 dark:text-red-400">{card.verifyError}</p>
                  )}

                  {/* Action Buttons */}
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => handleVerify(index)}
                      disabled={card.verifyStatus === 'verifying'}
                      className="flex-1 flex items-center justify-center gap-1.5 px-3 py-1.5 text-xs font-medium text-white rounded-md bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                    >
                      {card.verifyStatus === 'verifying' ? (
                        <>
                          <SpinnerIcon className="w-3 h-3 text-white" />
                          Checking...
                        </>
                      ) : (
                        'Check Access'
                      )}
                    </button>
                    <button
                      type="button"
                      onClick={() => handleSkipCard(index)}
                      disabled={card.verifyStatus === 'verifying'}
                      className="px-3 py-1.5 text-xs font-medium text-gray-600 dark:text-gray-400 rounded-md border border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-gray-500/50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                    >
                      Skip
                    </button>
                  </div>

                  {/* Images list (collapsed) */}
                  {card.area.images.length > 0 && (
                    <details className="mt-1">
                      <summary className="text-xs text-gray-400 dark:text-gray-500 cursor-pointer hover:text-gray-600 dark:hover:text-gray-300">
                        Show {card.area.images.length} image{card.area.images.length !== 1 ? 's' : ''}
                      </summary>
                      <div className="mt-1 pl-2 space-y-0.5">
                        {card.area.images.map(img => (
                          <p key={img} className="text-xs text-gray-400 dark:text-gray-500 font-mono truncate">{img}</p>
                        ))}
                      </div>
                    </details>
                  )}
                </div>
              </div>
            );
          })}

          {/* Skipped section */}
          {skippedCards.length > 0 && (
            <div className="mt-2">
              <p className="text-xs text-gray-400 dark:text-gray-500 mb-1.5">Skipped</p>
              {cards.map((card, index) => {
                if (card.status !== 'skipped') return null;
                return (
                  <div key={`${card.area.host}-${card.area.namespace}`}
                    className="flex items-center justify-between py-1.5 px-2 rounded text-xs text-gray-400 dark:text-gray-500 bg-gray-50 dark:bg-gray-800/50 mb-1">
                    <span className="truncate">
                      {card.area.host}{card.area.namespace && card.area.namespace !== '*' ? ` / ${card.area.namespace}` : ''}
                    </span>
                    <button
                      type="button"
                      onClick={() => handleUndoSkip(index)}
                      className="ml-2 text-brand-600 dark:text-brand-400 hover:underline flex-shrink-0"
                    >
                      Undo
                    </button>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Right Column: Verified */}
        <div className="space-y-3">
          <div className="flex items-center gap-2 mb-2">
            <div className="w-2 h-2 rounded-full bg-green-500" />
            <h3 className="text-sm font-medium text-gray-600 dark:text-gray-400">
              Verified {verifiedCards.length > 0 && <span className="text-gray-400 dark:text-gray-500">({verifiedCards.length})</span>}
            </h3>
          </div>

          {verifiedCards.length === 0 && (
            <div className="p-4 rounded-lg border border-dashed border-gray-200 dark:border-gray-700 text-center">
              <ShieldCheckIcon className="mx-auto w-6 h-6 text-gray-300 dark:text-gray-600 mb-1" />
              <p className="text-xs text-gray-400 dark:text-gray-500">No registries verified yet</p>
            </div>
          )}

          {cards.map((card, index) => {
            if (card.status !== 'verified') return null;
            return (
              <div key={`${card.area.host}-${card.area.namespace}`}
                className="rounded-lg border border-green-200 dark:border-green-800/40 bg-green-50/50 dark:bg-green-900/10 p-3">
                <div className="flex items-center gap-3">
                  <div className="flex-shrink-0 w-7 h-7 rounded-full bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
                    <CheckIcon className="w-3.5 h-3.5 text-green-600 dark:text-green-400" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <span className="font-medium text-sm text-gray-800 dark:text-white truncate block">
                      {card.area.host}
                      {card.area.namespace && card.area.namespace !== '*' && (
                        <span className="text-gray-500 dark:text-gray-400"> / {card.area.namespace}</span>
                      )}
                    </span>
                    <div className="flex items-center gap-2 mt-0.5">
                      <span className={`inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium ${
                        card.accessLevel === 'Public'
                          ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-300'
                          : 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300'
                      }`}>
                        {card.accessLevel === 'Public' ? 'Public' : 'Authenticated'}
                      </span>
                      <span className="text-xs text-gray-400 dark:text-gray-500">
                        {card.area.images.length} image{card.area.images.length !== 1 ? 's' : ''}
                      </span>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => handleUndoVerified(index)}
                    className="text-xs text-gray-400 dark:text-gray-500 hover:text-gray-600 dark:hover:text-gray-300 flex-shrink-0"
                  >
                    Undo
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Bottom buttons */}
      <div className="pt-6 flex gap-3">
        <button
          type="button"
          onClick={handleSkipAll}
          disabled={isLoading || isSkipping}
          className="flex-1 py-3 text-sm font-medium text-gray-700 transition-colors rounded-lg border border-gray-300 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-500/50 disabled:opacity-50 disabled:cursor-not-allowed dark:text-gray-300 dark:border-gray-600 dark:hover:bg-gray-800"
        >
          {isSkipping ? 'Skipping...' : 'Skip for now'}
        </button>
        <button
          type="button"
          onClick={handleSubmit}
          disabled={isLoading || isSkipping || checksRunning}
          className="flex-1 py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {isLoading ? 'Configuring...' : checksRunning ? 'Checking registries...' : 'Continue'}
        </button>
      </div>
    </div>
  );
}
