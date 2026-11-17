import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { checkProductUpgrade, type CheckProductUpgradeResponse } from '@rsgo/core';

interface ProductUpdateBadgeProps {
  environmentId: string;
  productDeploymentId: string;
}

/**
 * Shows an "update available" badge when a newer version of the product exists in the
 * catalog, with a link to its release notes (when available). Self-contained: fetches the
 * upgrade status itself via the existing CheckProductUpgrade API. The release-notes link
 * navigates to the dedicated /release-notes/:productId page.
 */
export default function ProductUpdateBadge({ environmentId, productDeploymentId }: ProductUpdateBadgeProps) {
  const [status, setStatus] = useState<CheckProductUpgradeResponse | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    let cancelled = false;
    checkProductUpgrade(environmentId, productDeploymentId)
      .then((s) => { if (!cancelled) setStatus(s); })
      .catch(() => { /* badge is best-effort; ignore errors */ });
    return () => { cancelled = true; };
  }, [environmentId, productDeploymentId]);

  if (!status?.upgradeAvailable || !status.latestVersion) {
    return null;
  }

  return (
    <span className="inline-flex items-center gap-1.5 rounded-full bg-brand-100 px-2.5 py-1 text-xs font-medium text-brand-800 dark:bg-brand-900/30 dark:text-brand-300">
      <svg className="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 10l7-7m0 0l7 7m-7-7v18" />
      </svg>
      Update available: v{status.latestVersion}
      {status.latestHasReleaseNotes && status.latestProductId && (
        <button
          type="button"
          onClick={() => navigate(`/release-notes/${encodeURIComponent(status.latestProductId!)}`)}
          className="ml-1 underline hover:no-underline"
        >
          Release notes
        </button>
      )}
    </span>
  );
}
