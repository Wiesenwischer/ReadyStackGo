import { useCallback, useEffect, useRef, useState } from "react";
import { systemApi, type VersionInfo } from "../api/system";

const DISMISSED_KEY = "rsgo_update_dismissed";
const POLL_INTERVAL_MS = 3000;
const POLL_TIMEOUT_MS = 120000;

type UpdateState = "idle" | "updating" | "success" | "error";

export default function SidebarWidget() {
  const [versionInfo, setVersionInfo] = useState<VersionInfo | null>(null);
  const [isDismissed, setIsDismissed] = useState(false);
  const [loading, setLoading] = useState(true);
  const [updateState, setUpdateState] = useState<UpdateState>("idle");
  const [updateError, setUpdateError] = useState<string | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    const loadVersion = async () => {
      try {
        const info = await systemApi.getVersion();
        setVersionInfo(info);

        const dismissed = localStorage.getItem(DISMISSED_KEY);
        if (dismissed === info.latestVersion) {
          setIsDismissed(true);
        }
      } catch (error) {
        console.error("Failed to load version info:", error);
      } finally {
        setLoading(false);
      }
    };

    loadVersion();
  }, []);

  const stopPolling = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = null;
    }
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);

  useEffect(() => {
    return () => stopPolling();
  }, [stopPolling]);

  const startPolling = useCallback(
    (targetVersion: string) => {
      stopPolling();

      pollRef.current = setInterval(async () => {
        try {
          const info = await systemApi.getVersion();
          if (info.serverVersion === targetVersion) {
            stopPolling();
            setUpdateState("success");
            localStorage.removeItem(DISMISSED_KEY);
            setTimeout(() => window.location.reload(), 1000);
          }
        } catch {
          // Server is restarting, ignore connection errors and keep polling
        }
      }, POLL_INTERVAL_MS);

      timeoutRef.current = setTimeout(() => {
        stopPolling();
        setUpdateState("error");
        setUpdateError(
          "Update is taking longer than expected. The server may still be restarting."
        );
      }, POLL_TIMEOUT_MS);
    },
    [stopPolling]
  );

  const handleUpdate = async () => {
    if (!versionInfo?.latestVersion) return;

    setUpdateState("updating");
    setUpdateError(null);

    try {
      const result = await systemApi.triggerUpdate(versionInfo.latestVersion);
      if (!result.success) {
        setUpdateState("error");
        setUpdateError(result.message);
        return;
      }
      startPolling(versionInfo.latestVersion);
    } catch (error) {
      setUpdateState("error");
      setUpdateError(
        error instanceof Error ? error.message : "Failed to trigger update."
      );
    }
  };

  const handleDismiss = () => {
    if (versionInfo?.latestVersion) {
      localStorage.setItem(DISMISSED_KEY, versionInfo.latestVersion);
      setIsDismissed(true);
    }
  };

  const showUpdateBanner =
    versionInfo?.updateAvailable && !isDismissed && updateState === "idle";
  const showUpdating = updateState === "updating" || updateState === "success";
  const showError = updateState === "error";

  return (
    <div className="mx-auto mb-10 w-full max-w-60 space-y-4">
      {/* Updating state */}
      {showUpdating && versionInfo && (
        <div className="rounded-2xl bg-brand-50 px-4 py-4 dark:bg-brand-900/20 border border-brand-200 dark:border-brand-800">
          <div className="flex items-center justify-center gap-2">
            <svg
              className="h-5 w-5 animate-spin text-brand-600 dark:text-brand-400"
              fill="none"
              viewBox="0 0 24 24"
            >
              <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
              />
            </svg>
            <span className="text-sm font-medium text-brand-800 dark:text-brand-200">
              {updateState === "success"
                ? "Update complete!"
                : `Updating to v${versionInfo.latestVersion}...`}
            </span>
          </div>
          <p className="mt-2 text-center text-xs text-brand-700 dark:text-brand-300">
            {updateState === "success"
              ? "Reloading..."
              : "RSGO will restart momentarily."}
          </p>
        </div>
      )}

      {/* Error state */}
      {showError && (
        <div className="rounded-2xl bg-red-50 px-4 py-4 dark:bg-red-900/20 border border-red-200 dark:border-red-800">
          <p className="text-xs text-red-700 dark:text-red-300">
            {updateError}
          </p>
          <button
            onClick={() => {
              setUpdateState("idle");
              setUpdateError(null);
            }}
            className="mt-3 flex w-full items-center justify-center gap-2 rounded-lg bg-red-500 px-3 py-2 text-xs font-medium text-white hover:bg-red-600"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* Update notification banner */}
      {showUpdateBanner && versionInfo && (
        <div className="rounded-2xl bg-brand-50 px-4 py-4 dark:bg-brand-900/20 border border-brand-200 dark:border-brand-800">
          <div className="flex items-start justify-between gap-2">
            <div className="flex items-center gap-2">
              <svg
                className="h-5 w-5 text-brand-600 dark:text-brand-400 flex-shrink-0"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"
                />
              </svg>
              <span className="text-sm font-medium text-brand-800 dark:text-brand-200">
                v{versionInfo.latestVersion}
              </span>
            </div>
            <button
              onClick={handleDismiss}
              className="text-brand-400 hover:text-brand-600 dark:hover:text-brand-300"
              title="Dismiss"
            >
              <svg
                className="h-4 w-4"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M6 18L18 6M6 6l12 12"
                />
              </svg>
            </button>
          </div>
          <p className="mt-2 text-xs text-brand-700 dark:text-brand-300">
            Update available
          </p>
          <div className="mt-3 flex flex-col gap-2">
            <button
              onClick={handleUpdate}
              className="flex items-center justify-center gap-2 rounded-lg bg-brand-500 px-3 py-2 text-xs font-medium text-white hover:bg-brand-600"
            >
              Update now
              <svg
                className="h-3 w-3"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"
                />
              </svg>
            </button>
            <a
              href={versionInfo.latestReleaseUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center justify-center gap-2 rounded-lg border border-brand-300 px-3 py-2 text-xs font-medium text-brand-700 hover:bg-brand-100 dark:border-brand-700 dark:text-brand-300 dark:hover:bg-brand-900/30"
            >
              See what's new
              <svg
                className="h-3 w-3"
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
          </div>
        </div>
      )}

      {/* Main widget with version info */}
      <div className="rounded-2xl bg-gray-50 px-4 py-5 text-center dark:bg-white/[0.03]">
        <h3 className="mb-2 font-semibold text-gray-900 dark:text-white">
          ReadyStackGo
        </h3>
        <p className="mb-4 text-gray-500 text-theme-sm dark:text-gray-400">
          Self-hosted platform for managing Docker-based microservice stacks.
        </p>
        <a
          href="https://github.com/Wiesenwischer/ReadyStackGo"
          target="_blank"
          rel="noopener noreferrer"
          className="flex items-center justify-center p-3 font-medium text-white rounded-lg bg-brand-500 text-theme-sm hover:bg-brand-600"
        >
          View on GitHub
        </a>
        {/* Version display */}
        {!loading && versionInfo && (
          <p className="mt-3 text-xs text-gray-400 dark:text-gray-500">
            v{versionInfo.serverVersion}
          </p>
        )}
      </div>
    </div>
  );
}
