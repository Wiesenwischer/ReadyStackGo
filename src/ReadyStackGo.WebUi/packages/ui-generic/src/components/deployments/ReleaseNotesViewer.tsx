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
export default function ReleaseNotesViewer({
  environmentId,
  productDeploymentId,
  version,
  onClose,
}: ReleaseNotesViewerProps) {
  const [data, setData] = useState<ProductReleaseNotesResponse | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getProductReleaseNotes(environmentId, productDeploymentId, version)
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load release notes'))
      .finally(() => setLoading(false));
  }, [environmentId, productDeploymentId, version]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={onClose}
    >
      <div
        className="max-h-[80vh] w-full max-w-2xl overflow-hidden rounded-2xl bg-white shadow-xl dark:bg-gray-900"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-gray-200 px-6 py-4 dark:border-gray-700">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            Release notes — v{version}
          </h3>
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
