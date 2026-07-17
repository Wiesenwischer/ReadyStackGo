import { useEffect, useState } from 'react';
import Markdown from 'react-markdown';
import rehypeSanitize from 'rehype-sanitize';
import { getProductReleaseNotes, type ProductReleaseNotesResponse } from '@rsgo/core';

interface ReleaseNotesViewerProps {
  environmentId: string;
  productDeploymentId: string;
  version: string;
  onClose: () => void;
}

/**
 * Modal that shows release notes for a product version: own CHANGELOG.md rendered as
 * sanitized markdown, or an external URL shown as a link (never embedded).
 */
// Best-effort default: the browser's primary language (e.g. "de-DE" -> "de"), so a
// German browser gets the German changelog when available. The backend falls back
// to a neutral / first-available changelog when this language isn't present.
const browserLocale = typeof navigator !== 'undefined'
  ? navigator.language?.split('-')[0]?.toLowerCase()
  : undefined;

const localeLabel = (code: string) => code.toUpperCase();

export default function ReleaseNotesViewer({
  environmentId,
  productDeploymentId,
  version,
  onClose,
}: ReleaseNotesViewerProps) {
  const [data, setData] = useState<ProductReleaseNotesResponse | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  // undefined = let the backend pick (browser language on first load); a user selection
  // pins a specific language and re-fetches.
  const [selectedLocale, setSelectedLocale] = useState<string | undefined>(browserLocale);

  useEffect(() => {
    setLoading(true);
    setError('');
    getProductReleaseNotes(environmentId, productDeploymentId, version, selectedLocale)
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load release notes'))
      .finally(() => setLoading(false));
  }, [environmentId, productDeploymentId, version, selectedLocale]);

  const availableLocales = data?.availableLocales ?? [];
  const showLanguageSelector = availableLocales.length > 1;
  const activeLocale = data?.locale;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={onClose}
    >
      <div
        className="max-h-[80vh] w-full max-w-2xl overflow-hidden rounded-2xl bg-white shadow-xl dark:bg-gray-900"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between gap-4 border-b border-gray-200 px-6 py-4 dark:border-gray-700">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            Release notes — v{version}
          </h3>
          <div className="flex items-center gap-3">
            {showLanguageSelector && (
              <div className="flex items-center gap-1" role="group" aria-label="Language">
                {availableLocales.map((loc) => (
                  <button
                    key={loc}
                    onClick={() => setSelectedLocale(loc)}
                    aria-pressed={activeLocale === loc}
                    className={`rounded-md px-2 py-1 text-xs font-medium transition-colors ${
                      activeLocale === loc
                        ? 'bg-brand-100 text-brand-700 dark:bg-brand-900/40 dark:text-brand-300'
                        : 'text-gray-500 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800'
                    }`}
                  >
                    {localeLabel(loc)}
                  </button>
                ))}
              </div>
            )}
            <button
              onClick={onClose}
              aria-label="Close"
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200"
            >
              <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>

        <div className="max-h-[60vh] overflow-y-auto px-6 py-5">
          {loading && <p className="text-sm text-gray-500 dark:text-gray-400">Loading…</p>}

          {!loading && error && (
            <p className="text-sm text-red-700 dark:text-red-400">{error}</p>
          )}

          {!loading && !error && data?.mode === 'markdown' && (
            <div className="prose prose-sm max-w-none dark:prose-invert">
              <Markdown rehypePlugins={[rehypeSanitize]}>{data.content ?? ''}</Markdown>
            </div>
          )}

          {!loading && !error && data?.mode === 'url' && (
            <p className="text-sm text-gray-700 dark:text-gray-300">
              Release notes are hosted externally:{' '}
              <a
                href={data.url}
                target="_blank"
                rel="noopener noreferrer"
                className="text-brand-600 underline hover:text-brand-700"
              >
                {data.url}
              </a>
            </p>
          )}

          {!loading && !error && (!data || data.mode === 'none') && (
            <p className="text-sm text-gray-500 dark:text-gray-400">No release notes available.</p>
          )}
        </div>
      </div>
    </div>
  );
}
