import { useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import { systemApi, type VersionInfo } from "../api/system";

const POLL_INTERVAL_MS = 3000;
const POLL_TIMEOUT_MS = 120000;

type Phase = "preparing" | "updating" | "success" | "error";

export default function UpdateStatus() {
  const [searchParams] = useSearchParams();
  const targetVersion = searchParams.get("version") ?? "";
  const releaseUrl = searchParams.get("releaseUrl") ?? "";

  const [phase, setPhase] = useState<Phase>("preparing");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [currentVersion, setCurrentVersion] = useState<string | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const hasTriggered = useRef(false);

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

  const startPolling = useCallback(() => {
    stopPolling();

    pollRef.current = setInterval(async () => {
      try {
        const info: VersionInfo = await systemApi.getVersion();
        if (info.serverVersion === targetVersion) {
          stopPolling();
          setPhase("success");
          localStorage.removeItem("rsgo_update_dismissed");
          setTimeout(() => {
            window.location.href = "/";
          }, 2000);
        }
      } catch {
        // Server is restarting, keep polling
      }
    }, POLL_INTERVAL_MS);

    timeoutRef.current = setTimeout(() => {
      stopPolling();
      setPhase("error");
      setErrorMessage(
        "Update is taking longer than expected. The server may still be restarting — try refreshing this page in a moment."
      );
    }, POLL_TIMEOUT_MS);
  }, [stopPolling, targetVersion]);

  useEffect(() => {
    if (!targetVersion || hasTriggered.current) return;
    hasTriggered.current = true;

    const triggerUpdate = async () => {
      try {
        const info = await systemApi.getVersion();
        setCurrentVersion(info.serverVersion);
      } catch {
        // Ignore, we'll proceed without current version
      }

      setPhase("updating");

      try {
        const result = await systemApi.triggerUpdate(targetVersion);
        if (!result.success) {
          setPhase("error");
          setErrorMessage(result.message);
          return;
        }
        startPolling();
      } catch (error) {
        setPhase("error");
        setErrorMessage(
          error instanceof Error ? error.message : "Failed to trigger update."
        );
      }
    };

    triggerUpdate();
  }, [targetVersion, startPolling]);

  const handleRetry = () => {
    hasTriggered.current = false;
    setPhase("preparing");
    setErrorMessage(null);
    // Re-trigger by resetting the ref and updating phase
    setTimeout(() => {
      hasTriggered.current = false;
      // Force re-run of the effect by creating a micro-task
      const trigger = async () => {
        hasTriggered.current = true;
        setPhase("updating");
        try {
          const result = await systemApi.triggerUpdate(targetVersion);
          if (!result.success) {
            setPhase("error");
            setErrorMessage(result.message);
            return;
          }
          startPolling();
        } catch (error) {
          setPhase("error");
          setErrorMessage(
            error instanceof Error
              ? error.message
              : "Failed to trigger update."
          );
        }
      };
      trigger();
    }, 0);
  };

  return (
    <div className="relative min-h-screen bg-white dark:bg-gray-900">
      {/* Grid background */}
      <div className="absolute inset-0 overflow-hidden">
        <svg
          className="absolute inset-0 w-full h-full"
          xmlns="http://www.w3.org/2000/svg"
        >
          <defs>
            <pattern
              id="grid"
              width="40"
              height="40"
              patternUnits="userSpaceOnUse"
            >
              <path
                d="M 40 0 L 0 0 0 40"
                fill="none"
                stroke="currentColor"
                className="text-gray-100 dark:text-white/[0.03]"
                strokeWidth="1"
              />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#grid)" />
        </svg>
      </div>

      <div className="relative flex flex-col items-center justify-center min-h-screen px-6">
        <div className="w-full max-w-md text-center">
          {/* Logo */}
          <h1 className="mb-8 text-2xl font-bold text-gray-900 dark:text-white sm:text-3xl">
            ReadyStackGo
          </h1>

          {/* Preparing / Updating state */}
          {(phase === "preparing" || phase === "updating") && (
            <div className="rounded-2xl border border-brand-200 bg-brand-50/50 p-8 dark:border-brand-800 dark:bg-brand-900/10">
              {/* Spinner */}
              <div className="mx-auto mb-6 h-16 w-16">
                <svg
                  className="h-16 w-16 animate-spin text-brand-500"
                  fill="none"
                  viewBox="0 0 24 24"
                >
                  <circle
                    className="opacity-25"
                    cx="12"
                    cy="12"
                    r="10"
                    stroke="currentColor"
                    strokeWidth="3"
                  />
                  <path
                    className="opacity-75"
                    fill="currentColor"
                    d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
                  />
                </svg>
              </div>

              <h2 className="mb-2 text-xl font-semibold text-gray-900 dark:text-white">
                {phase === "preparing"
                  ? "Preparing update..."
                  : `Updating to v${targetVersion}`}
              </h2>

              <p className="mb-6 text-sm text-gray-500 dark:text-gray-400">
                RSGO will restart momentarily. This page will reload
                automatically once the new version is running.
              </p>

              {/* Version badge */}
              {currentVersion && (
                <div className="inline-flex items-center gap-2 rounded-lg bg-white px-4 py-2 text-sm dark:bg-gray-800">
                  <span className="text-gray-500 dark:text-gray-400">
                    v{currentVersion}
                  </span>
                  <svg
                    className="h-4 w-4 text-brand-500"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M13 7l5 5m0 0l-5 5m5-5H6"
                    />
                  </svg>
                  <span className="font-medium text-brand-600 dark:text-brand-400">
                    v{targetVersion}
                  </span>
                </div>
              )}
            </div>
          )}

          {/* Success state */}
          {phase === "success" && (
            <div className="rounded-2xl border border-green-200 bg-green-50/50 p-8 dark:border-green-800 dark:bg-green-900/10">
              <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-full bg-green-100 dark:bg-green-900/30">
                <svg
                  className="h-8 w-8 text-green-600 dark:text-green-400"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M5 13l4 4L19 7"
                  />
                </svg>
              </div>

              <h2 className="mb-2 text-xl font-semibold text-gray-900 dark:text-white">
                Update complete!
              </h2>

              <p className="mb-4 text-sm text-gray-500 dark:text-gray-400">
                Successfully updated to v{targetVersion}. Redirecting...
              </p>

              <div className="inline-flex items-center gap-2 rounded-lg bg-white px-4 py-2 text-sm font-medium text-green-700 dark:bg-gray-800 dark:text-green-400">
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
                    d="M5 13l4 4L19 7"
                  />
                </svg>
                v{targetVersion}
              </div>
            </div>
          )}

          {/* Error state */}
          {phase === "error" && (
            <div className="rounded-2xl border border-red-200 bg-red-50/50 p-8 dark:border-red-800 dark:bg-red-900/10">
              <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-full bg-red-100 dark:bg-red-900/30">
                <svg
                  className="h-8 w-8 text-red-600 dark:text-red-400"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4.5c-.77-.833-2.694-.833-3.464 0L3.34 16.5c-.77.833.192 2.5 1.732 2.5z"
                  />
                </svg>
              </div>

              <h2 className="mb-2 text-xl font-semibold text-gray-900 dark:text-white">
                Update failed
              </h2>

              <p className="mb-6 text-sm text-red-700 dark:text-red-300">
                {errorMessage}
              </p>

              <div className="flex flex-col gap-3">
                <button
                  onClick={handleRetry}
                  className="inline-flex items-center justify-center rounded-lg bg-brand-500 px-5 py-3 text-sm font-medium text-white hover:bg-brand-600"
                >
                  Retry update
                </button>
                <a
                  href="/"
                  className="inline-flex items-center justify-center rounded-lg border border-gray-300 px-5 py-3 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
                >
                  Back to Dashboard
                </a>
              </div>
            </div>
          )}

          {/* Release notes link */}
          {releaseUrl && phase !== "error" && (
            <a
              href={releaseUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="mt-6 inline-flex items-center gap-1.5 text-sm text-gray-500 hover:text-brand-600 dark:text-gray-400 dark:hover:text-brand-400"
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

        {/* Footer */}
        <p className="absolute bottom-6 left-1/2 -translate-x-1/2 text-center text-sm text-gray-400 dark:text-gray-500">
          &copy; {new Date().getFullYear()} ReadyStackGo
        </p>
      </div>
    </div>
  );
}
