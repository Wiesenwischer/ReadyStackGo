import { useState, useEffect } from 'react';
import { detectRegistries, type DetectedRegistryArea, type RegistryInputDto } from '../../api/wizard';

interface RegistryCardState {
  area: DetectedRegistryArea;
  requiresAuth: boolean;
  username: string;
  password: string;
  name: string;
  pattern: string;
}

interface RegistriesStepProps {
  onNext: (registries: RegistryInputDto[]) => Promise<void>;
}

export default function RegistriesStep({ onNext }: RegistriesStepProps) {
  const [cards, setCards] = useState<RegistryCardState[]>([]);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSkipping, setIsSkipping] = useState(false);
  const [isFetching, setIsFetching] = useState(true);
  const [expandedCards, setExpandedCards] = useState<Set<number>>(new Set());

  useEffect(() => {
    const fetchRegistries = async () => {
      try {
        const response = await detectRegistries();
        const cardStates: RegistryCardState[] = response.areas
          .filter(a => !a.isConfigured)
          .map(area => ({
            area,
            requiresAuth: !area.isLikelyPublic,
            username: '',
            password: '',
            name: area.suggestedName,
            pattern: area.suggestedPattern,
          }));
        setCards(cardStates);
        // Expand cards that need auth by default
        const authCards = new Set<number>();
        cardStates.forEach((card, index) => {
          if (!card.area.isLikelyPublic) {
            authCards.add(index);
          }
        });
        setExpandedCards(authCards);
      } catch {
        setError('Failed to detect container registries');
      } finally {
        setIsFetching(false);
      }
    };
    fetchRegistries();
  }, []);

  const updateCard = (index: number, updates: Partial<RegistryCardState>) => {
    setCards(prev => prev.map((card, i) => i === index ? { ...card, ...updates } : card));
  };

  const toggleExpanded = (index: number) => {
    setExpandedCards(prev => {
      const next = new Set(prev);
      if (next.has(index)) {
        next.delete(index);
      } else {
        next.add(index);
      }
      return next;
    });
  };

  const handleSubmit = async () => {
    setError('');
    setIsLoading(true);
    try {
      const registries: RegistryInputDto[] = cards.map(card => ({
        name: card.name,
        host: card.area.host,
        pattern: card.pattern,
        requiresAuth: card.requiresAuth,
        username: card.requiresAuth ? card.username : undefined,
        password: card.requiresAuth ? card.password : undefined,
      }));
      await onNext(registries);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to configure registries');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSkip = async () => {
    setIsSkipping(true);
    try {
      await onNext([]);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to skip step');
    } finally {
      setIsSkipping(false);
    }
  };

  const authRequiredCount = cards.filter(c => c.requiresAuth).length;

  if (isFetching) {
    return (
      <div className="flex items-center justify-center py-12">
        <svg className="animate-spin h-8 w-8 text-brand-600 dark:text-brand-400" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
        </svg>
      </div>
    );
  }

  return (
    <div>
      <div className="mb-6">
        <h2 className="mb-2 text-2xl font-semibold text-gray-800 dark:text-white">
          Container Registries
        </h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Configure authentication for private container registries used by your stacks.
          Public registries like Docker Hub don't need credentials.
          You can change these later in Settings.
        </p>
      </div>

      {error && (
        <div className="mb-4 p-4 text-sm border border-red-300 rounded-lg bg-red-50 text-red-800 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
          {error}
        </div>
      )}

      {cards.length === 0 ? (
        <div className="py-8 text-center text-gray-500 dark:text-gray-400">
          <svg className="mx-auto h-12 w-12 text-gray-400 dark:text-gray-500 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
          </svg>
          <p>All detected registries are already configured.</p>
          <p className="text-xs mt-1">You can proceed to the next step.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {cards.map((card, index) => (
            <div
              key={`${card.area.host}-${card.area.namespace}`}
              className={`rounded-lg border transition-colors ${
                card.requiresAuth
                  ? 'border-brand-300 dark:border-brand-700'
                  : 'border-gray-200 dark:border-gray-700'
              }`}
            >
              {/* Card Header */}
              <button
                type="button"
                onClick={() => toggleExpanded(index)}
                className="w-full flex items-center justify-between p-4 text-left"
              >
                <div className="flex items-center gap-3 min-w-0">
                  <div className={`flex-shrink-0 w-8 h-8 rounded-full flex items-center justify-center ${
                    card.area.isLikelyPublic
                      ? 'bg-green-100 dark:bg-green-900/30'
                      : 'bg-amber-100 dark:bg-amber-900/30'
                  }`}>
                    {card.area.isLikelyPublic ? (
                      <svg className="w-4 h-4 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3.055 11H5a2 2 0 012 2v1a2 2 0 002 2 2 2 0 012 2v2.945M8 3.935V5.5A2.5 2.5 0 0010.5 8h.5a2 2 0 012 2 2 2 0 104 0 2 2 0 012-2h1.064M15 20.488V18a2 2 0 012-2h3.064M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                    ) : (
                      <svg className="w-4 h-4 text-amber-600 dark:text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                      </svg>
                    )}
                  </div>
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-gray-800 dark:text-white truncate">
                        {card.area.host}
                      </span>
                      {card.area.namespace && (
                        <span className="text-sm text-gray-500 dark:text-gray-400 truncate">
                          / {card.area.namespace}
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-gray-400 dark:text-gray-500">
                      {card.area.images.length} image{card.area.images.length !== 1 ? 's' : ''}
                      {card.area.isLikelyPublic && (
                        <span className="ml-2 inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400">
                          Public
                        </span>
                      )}
                    </p>
                  </div>
                </div>
                <svg
                  className={`w-5 h-5 text-gray-400 transition-transform ${expandedCards.has(index) ? 'rotate-180' : ''}`}
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              {/* Card Body (expanded) */}
              {expandedCards.has(index) && (
                <div className="px-4 pb-4 border-t border-gray-100 dark:border-gray-700/50">
                  {/* Auth Toggle */}
                  <div className="mt-3 flex items-center gap-4">
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="radio"
                        name={`auth-${index}`}
                        checked={card.requiresAuth}
                        onChange={() => updateCard(index, { requiresAuth: true })}
                        className="text-brand-600 focus:ring-brand-500"
                      />
                      <span className="text-sm text-gray-700 dark:text-gray-300">Requires Authentication</span>
                    </label>
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="radio"
                        name={`auth-${index}`}
                        checked={!card.requiresAuth}
                        onChange={() => updateCard(index, { requiresAuth: false })}
                        className="text-brand-600 focus:ring-brand-500"
                      />
                      <span className="text-sm text-gray-700 dark:text-gray-300">Public (no auth)</span>
                    </label>
                  </div>

                  {/* Credential Fields */}
                  {card.requiresAuth && (
                    <div className="mt-3 space-y-3">
                      <div className="grid grid-cols-2 gap-3">
                        <div>
                          <label className="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">
                            Username
                          </label>
                          <input
                            type="text"
                            value={card.username}
                            onChange={e => updateCard(index, { username: e.target.value })}
                            placeholder="Registry username"
                            className="w-full px-3 py-2 text-sm rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-800 dark:text-white placeholder-gray-400 focus:ring-2 focus:ring-brand-500/50 focus:border-brand-500"
                          />
                        </div>
                        <div>
                          <label className="block text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">
                            Password / Token
                          </label>
                          <input
                            type="password"
                            value={card.password}
                            onChange={e => updateCard(index, { password: e.target.value })}
                            placeholder="Password or access token"
                            className="w-full px-3 py-2 text-sm rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-800 dark:text-white placeholder-gray-400 focus:ring-2 focus:ring-brand-500/50 focus:border-brand-500"
                          />
                        </div>
                      </div>
                    </div>
                  )}

                  {/* Images list (collapsed) */}
                  {card.area.images.length > 0 && (
                    <details className="mt-3">
                      <summary className="text-xs text-gray-400 dark:text-gray-500 cursor-pointer hover:text-gray-600 dark:hover:text-gray-300">
                        Show {card.area.images.length} image{card.area.images.length !== 1 ? 's' : ''}
                      </summary>
                      <div className="mt-1 pl-2 space-y-0.5">
                        {card.area.images.map(img => (
                          <p key={img} className="text-xs text-gray-400 dark:text-gray-500 font-mono truncate">
                            {img}
                          </p>
                        ))}
                      </div>
                    </details>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      <div className="pt-6 flex gap-3">
        <button
          type="button"
          onClick={handleSkip}
          disabled={isLoading || isSkipping}
          className="flex-1 py-3 text-sm font-medium text-gray-700 transition-colors rounded-lg border border-gray-300 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-500/50 disabled:opacity-50 disabled:cursor-not-allowed dark:text-gray-300 dark:border-gray-600 dark:hover:bg-gray-800"
        >
          {isSkipping ? 'Skipping...' : 'Skip for now'}
        </button>
        <button
          type="button"
          onClick={handleSubmit}
          disabled={isLoading || isSkipping}
          className="flex-1 py-3 text-sm font-medium text-white transition-colors rounded-lg bg-brand-600 hover:bg-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {isLoading
            ? 'Configuring...'
            : authRequiredCount > 0
              ? `Configure ${authRequiredCount} registr${authRequiredCount !== 1 ? 'ies' : 'y'}`
              : 'Continue'
          }
        </button>
      </div>
    </div>
  );
}
