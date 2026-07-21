import { useEffect, useState } from 'react';
import { useParams, useSearchParams, useNavigate } from 'react-router';
import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeSanitize from 'rehype-sanitize';
import { getCatalogProductReleaseNotes, type ProductReleaseNotesResponse } from '@rsgo/core';

// Best-effort default language: the browser's primary language (e.g. "de-DE" -> "de").
const browserLocale = typeof navigator !== 'undefined'
  ? navigator.language?.split('-')[0]?.toLowerCase()
  : undefined;

const localeLabel = (code: string) => code.toUpperCase();

/**
 * Full-page release notes for a catalog product version. Reached from the Stack Catalog
 * and from the deployment update badge via /release-notes/:productId. The selected
 * language is kept in the URL (?locale=) so the page is shareable and bookmarkable.
 */
export default function ReleaseNotesPage() {
  const { productId } = useParams<{ productId: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  const selectedLocale = searchParams.get('locale') ?? browserLocale;

  const [data, setData] = useState<ProductReleaseNotesResponse | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!productId) return;
    setLoading(true);
    setError('');
    getCatalogProductReleaseNotes(productId, selectedLocale ?? undefined)
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load release notes'))
      .finally(() => setLoading(false));
  }, [productId, selectedLocale]);

  const availableLocales = data?.availableLocales ?? [];
  const showLanguageSelector = availableLocales.length > 1;
  const activeLocale = data?.locale;

  const selectLocale = (loc: string) => {
    const next = new URLSearchParams(searchParams);
    next.set('locale', loc);
    setSearchParams(next, { replace: true });
  };

  return (
    <div className="mx-auto max-w-4xl p-4 md:p-6 2xl:p-10">
      {/* Back */}
      <div className="mb-6">
        <button
          onClick={() => navigate(-1)}
          className="inline-flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Back
        </button>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
        {/* Header */}
        <div className="flex items-center justify-between gap-4 border-b border-gray-200 px-6 py-4 dark:border-gray-700">
          <h1 className="text-xl font-semibold text-gray-900 dark:text-white">
            Release notes{data?.version ? ` — v${data.version}` : ''}
          </h1>
          {showLanguageSelector && (
            <div className="flex items-center gap-1" role="group" aria-label="Language">
              {availableLocales.map((loc) => (
                <button
                  key={loc}
                  onClick={() => selectLocale(loc)}
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
        </div>

        {/* Body */}
        <div className="px-6 py-5">
          {loading && <p className="text-sm text-gray-500 dark:text-gray-400">Loading…</p>}

          {!loading && error && (
            <p className="text-sm text-red-700 dark:text-red-400">{error}</p>
          )}

          {!loading && !error && data?.mode === 'markdown' && (
            <div className="prose prose-sm max-w-none overflow-x-auto dark:prose-invert">
              <Markdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeSanitize]}>
                {data.content ?? ''}
              </Markdown>
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
