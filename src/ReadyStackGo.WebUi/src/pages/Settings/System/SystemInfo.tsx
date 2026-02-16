import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useVersionInfo } from "../../../hooks/useVersionInfo";

function formatRelativeTime(isoString?: string): string {
  if (!isoString) return "Never";
  const date = new Date(isoString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMinutes = Math.floor(diffMs / 60000);
  if (diffMinutes < 1) return "Just now";
  if (diffMinutes < 60)
    return `${diffMinutes} minute${diffMinutes > 1 ? "s" : ""} ago`;
  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24)
    return `${diffHours} hour${diffHours > 1 ? "s" : ""} ago`;
  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays} day${diffDays > 1 ? "s" : ""} ago`;
}

export default function SystemInfo() {
  const { versionInfo, isLoading, error, refetch } = useVersionInfo();
  const [checking, setChecking] = useState(false);
  const [checkResult, setCheckResult] = useState<"up-to-date" | null>(null);
  const navigate = useNavigate();

  const handleCheckNow = async () => {
    setChecking(true);
    setCheckResult(null);
    try {
      await refetch(true);
      setCheckResult("up-to-date");
      setTimeout(() => setCheckResult(null), 5000);
    } finally {
      setChecking(false);
    }
  };

  const handleUpdate = () => {
    if (!versionInfo?.latestVersion) return;
    const params = new URLSearchParams({ version: versionInfo.latestVersion });
    if (versionInfo.latestReleaseUrl) {
      params.set("releaseUrl", versionInfo.latestReleaseUrl);
    }
    navigate(`/update?${params.toString()}`);
  };

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      {/* Breadcrumb */}
      <nav className="mb-6 flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
        <Link
          to="/settings"
          className="hover:text-brand-600 dark:hover:text-brand-400"
        >
          Settings
        </Link>
        <span>/</span>
        <span className="text-gray-900 dark:text-white">System</span>
      </nav>

      {/* Header */}
      <div className="mb-8">
        <h2 className="text-2xl font-bold text-black dark:text-white">
          System
        </h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Version information and update management
        </p>
      </div>

      {isLoading && !versionInfo ? (
        <div className="flex items-center justify-center py-12">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-brand-500 border-t-transparent" />
        </div>
      ) : (
        <div className="space-y-6">
          {/* System Information card */}
          <div className="rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]">
            <div className="border-b border-gray-200 px-6 py-5 dark:border-gray-700">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                System Information
              </h3>
            </div>
            <div className="px-6 py-5">
              <dl className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <dt className="text-sm text-gray-500 dark:text-gray-400">
                    Server Version
                  </dt>
                  <dd className="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                    {versionInfo?.serverVersion ?? "-"}
                  </dd>
                </div>

                <div>
                  <dt className="text-sm text-gray-500 dark:text-gray-400">
                    Latest Version
                  </dt>
                  <dd className="mt-1 flex items-center gap-3">
                    <span className="text-sm font-medium text-gray-900 dark:text-white">
                      {versionInfo?.latestVersion ?? "-"}
                    </span>
                    <button
                      onClick={handleCheckNow}
                      disabled={checking}
                      className="inline-flex items-center gap-1.5 rounded-md border border-gray-300 px-2.5 py-1 text-xs font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:opacity-50 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-800"
                    >
                      {checking ? "Checking..." : "Check now"}
                    </button>
                  </dd>
                </div>

                <div>
                  <dt className="text-sm text-gray-500 dark:text-gray-400">
                    Runtime
                  </dt>
                  <dd className="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                    {versionInfo?.build.runtimeVersion ?? "-"}
                  </dd>
                </div>

                <div>
                  <dt className="text-sm text-gray-500 dark:text-gray-400">
                    Git Commit
                  </dt>
                  <dd className="mt-1 font-mono text-sm text-gray-900 dark:text-white">
                    {versionInfo?.build.gitCommit
                      ? versionInfo.build.gitCommit.substring(0, 7) + "..."
                      : "-"}
                  </dd>
                </div>

                <div>
                  <dt className="text-sm text-gray-500 dark:text-gray-400">
                    Build Date
                  </dt>
                  <dd className="mt-1 text-sm font-medium text-gray-900 dark:text-white">
                    {versionInfo?.build.buildDate ?? "-"}
                  </dd>
                </div>
              </dl>
            </div>
          </div>

          {/* Up to date confirmation */}
          {checkResult === "up-to-date" && !versionInfo?.updateAvailable && (
            <div className="rounded-2xl border border-green-200 bg-green-50 p-4 dark:border-green-800 dark:bg-green-900/20">
              <div className="flex items-center gap-2">
                <svg className="h-5 w-5 text-green-500" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                </svg>
                <span className="text-sm font-medium text-green-800 dark:text-green-200">
                  You are running the latest version.
                </span>
              </div>
            </div>
          )}

          {/* Update Available banner */}
          {versionInfo?.updateAvailable && (
            <div className="rounded-2xl border border-blue-200 bg-blue-50 p-6 dark:border-blue-800 dark:bg-blue-900/20">
              <div className="flex items-start gap-3">
                <svg
                  className="mt-0.5 h-6 w-6 flex-shrink-0 text-blue-500"
                  fill="currentColor"
                  viewBox="0 0 20 20"
                >
                  <path
                    fillRule="evenodd"
                    d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z"
                    clipRule="evenodd"
                  />
                </svg>
                <div className="flex-1">
                  <h4 className="text-lg font-semibold text-blue-900 dark:text-blue-100">
                    Update Available
                  </h4>
                  <p className="mt-1 text-sm text-blue-700 dark:text-blue-300">
                    ReadyStackGo v{versionInfo.latestVersion} is available. You
                    are running v{versionInfo.serverVersion}.
                  </p>
                  <div className="mt-4 flex items-center gap-3">
                    <button
                      onClick={handleUpdate}
                      className="inline-flex items-center gap-2 rounded-lg bg-brand-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-brand-600"
                    >
                      Update now
                    </button>
                    {versionInfo.latestReleaseUrl && (
                      <a
                        href={versionInfo.latestReleaseUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center gap-1.5 text-sm font-medium text-blue-700 hover:text-blue-800 dark:text-blue-300 dark:hover:text-blue-200"
                      >
                        See what's new
                        <svg
                          className="h-3.5 w-3.5"
                          fill="none"
                          stroke="currentColor"
                          viewBox="0 0 24 24"
                        >
                          <path
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            strokeWidth={2}
                            d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"
                          />
                        </svg>
                      </a>
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Last checked */}
          <p className="text-xs text-gray-400 dark:text-gray-500">
            Last checked: {formatRelativeTime(versionInfo?.checkedAt)}
          </p>
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="mt-6 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800 dark:border-red-800 dark:bg-red-900/20 dark:text-red-400">
          {error}
        </div>
      )}
    </div>
  );
}
