import { useCallback, useEffect, useRef, useState } from 'react';
import { systemApi, type VersionInfo } from '../api/system';
import { useUpdateHub } from '../realtime/useUpdateHub';

const VERSION_POLL_INTERVAL_MS = 3000;
const VERSION_POLL_TIMEOUT_MS = 120000;

export type UpdatePhase = 'connecting' | 'triggering' | 'pulling' | 'creating' | 'starting' | 'restarting' | 'success' | 'error';

export interface UseUpdateStoreReturn {
  phase: UpdatePhase;
  errorMessage: string | null;
  currentVersion: string | null;
  pullPercent: number;
  isWorking: boolean;
  phaseMessage: string;
  retry: () => void;
}

export function useUpdateStore(targetVersion: string): UseUpdateStoreReturn {
  const [phase, setPhase] = useState<UpdatePhase>('connecting');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [currentVersion, setCurrentVersion] = useState<string | null>(null);
  const [pullPercent, setPullPercent] = useState(0);

  const hasTriggered = useRef(false);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const { connectionState, progress } = useUpdateHub();

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

  useEffect(() => () => stopPolling(), [stopPolling]);

  const startVersionPolling = useCallback(() => {
    stopPolling();

    pollRef.current = setInterval(async () => {
      try {
        const info: VersionInfo = await systemApi.getVersion();
        if (info.serverVersion === targetVersion) {
          stopPolling();
          setPhase('success');
          localStorage.removeItem('rsgo_update_dismissed');
          setTimeout(() => {
            window.location.href = '/';
          }, 2000);
        }
      } catch {
        // Server is restarting, keep polling
      }
    }, VERSION_POLL_INTERVAL_MS);

    timeoutRef.current = setTimeout(() => {
      stopPolling();
      setPhase('error');
      setErrorMessage(
        'Update is taking longer than expected. The server may still be restarting — try refreshing this page in a moment.'
      );
    }, VERSION_POLL_TIMEOUT_MS);
  }, [stopPolling, targetVersion]);

  // React to SignalR progress updates
  useEffect(() => {
    if (!progress) return;

    switch (progress.phase) {
      case 'pulling':
        setPhase('pulling');
        setPullPercent(progress.progressPercent ?? 0);
        break;
      case 'creating':
        setPhase('creating');
        break;
      case 'starting':
        setPhase('starting');
        break;
      case 'handed_off':
        setPhase('restarting');
        startVersionPolling();
        break;
      case 'error':
        setPhase('error');
        setErrorMessage(progress.message ?? 'Update failed.');
        break;
    }
  }, [progress, startVersionPolling]);

  // Trigger update once connected
  useEffect(() => {
    if (!targetVersion || hasTriggered.current || connectionState !== 'connected') return;

    // If progress shows update already in progress (e.g. page refresh), don't re-trigger
    if (progress && progress.phase !== 'idle' && progress.phase !== 'error') return;

    hasTriggered.current = true;
    setPhase('triggering');

    const triggerUpdate = async () => {
      try {
        const info = await systemApi.getVersion();
        setCurrentVersion(info.serverVersion);
      } catch {
        // Ignore, proceed without current version
      }

      try {
        const result = await systemApi.triggerUpdate(targetVersion);
        if (!result.success) {
          setPhase('error');
          setErrorMessage(result.message);
        }
      } catch (error) {
        setPhase('error');
        setErrorMessage(
          error instanceof Error ? error.message : 'Failed to trigger update.'
        );
      }
    };

    triggerUpdate();
  }, [targetVersion, connectionState, progress, startVersionPolling]);

  // If SignalR connection drops while in an active phase, assume server is restarting
  useEffect(() => {
    if (connectionState === 'disconnected' && (phase === 'pulling' || phase === 'creating' || phase === 'starting')) {
      setPhase('restarting');
      startVersionPolling();
    }
  }, [connectionState, phase, startVersionPolling]);

  const retry = useCallback(() => {
    hasTriggered.current = false;
    setPhase('triggering');
    setErrorMessage(null);
    setPullPercent(0);

    const trigger = async () => {
      hasTriggered.current = true;
      try {
        const result = await systemApi.triggerUpdate(targetVersion);
        if (!result.success) {
          setPhase('error');
          setErrorMessage(result.message);
        }
      } catch (error) {
        setPhase('error');
        setErrorMessage(
          error instanceof Error ? error.message : 'Failed to trigger update.'
        );
      }
    };
    trigger();
  }, [targetVersion]);

  const isWorking = phase === 'connecting' || phase === 'triggering' || phase === 'pulling' || phase === 'creating' || phase === 'starting' || phase === 'restarting';

  let phaseMessage = '';
  switch (phase) {
    case 'connecting': phaseMessage = 'Connecting...'; break;
    case 'triggering': phaseMessage = `Updating to v${targetVersion}`; break;
    case 'pulling': phaseMessage = `Downloading v${targetVersion}`; break;
    case 'creating': phaseMessage = 'Preparing new container...'; break;
    case 'starting': phaseMessage = 'Starting update process...'; break;
    case 'restarting': phaseMessage = 'Restarting with new version...'; break;
  }

  return {
    phase,
    errorMessage,
    currentVersion,
    pullPercent,
    isWorking,
    phaseMessage,
    retry,
  };
}
