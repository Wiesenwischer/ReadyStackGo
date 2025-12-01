import { useState, useEffect } from 'react';
import type { WizardTimeoutInfo } from '../../api/wizard';

interface WizardCountdownProps {
  timeout: WizardTimeoutInfo;
  onTimeout: () => void;
}

export default function WizardCountdown({ timeout, onTimeout }: WizardCountdownProps) {
  const [remainingSeconds, setRemainingSeconds] = useState<number>(
    timeout.remainingSeconds ?? 0
  );

  useEffect(() => {
    // Reset when timeout info changes
    setRemainingSeconds(timeout.remainingSeconds ?? 0);
  }, [timeout.remainingSeconds]);

  useEffect(() => {
    if (remainingSeconds <= 0) {
      onTimeout();
      return;
    }

    const timer = setInterval(() => {
      setRemainingSeconds((prev) => {
        if (prev <= 1) {
          clearInterval(timer);
          onTimeout();
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [remainingSeconds, onTimeout]);

  // Format time as mm:ss
  const formatTime = (seconds: number): string => {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  // Calculate progress percentage
  const progressPercent = (remainingSeconds / timeout.timeoutSeconds) * 100;

  // Warning threshold (1 minute)
  const isWarning = remainingSeconds <= 60;
  const isCritical = remainingSeconds <= 30;

  return (
    <div className="flex items-center gap-3">
      {/* Timer icon */}
      <svg
        className={`w-5 h-5 ${
          isCritical
            ? 'text-red-500 animate-pulse'
            : isWarning
            ? 'text-amber-500'
            : 'text-gray-400 dark:text-gray-500'
        }`}
        fill="none"
        stroke="currentColor"
        viewBox="0 0 24 24"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={2}
          d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"
        />
      </svg>

      {/* Time display */}
      <div className="flex flex-col">
        <span
          className={`text-sm font-medium ${
            isCritical
              ? 'text-red-600 dark:text-red-400'
              : isWarning
              ? 'text-amber-600 dark:text-amber-400'
              : 'text-gray-600 dark:text-gray-400'
          }`}
        >
          {formatTime(remainingSeconds)}
        </span>
        <span className="text-xs text-gray-400 dark:text-gray-500">remaining</span>
      </div>

      {/* Progress bar */}
      <div className="w-24 h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
        <div
          className={`h-full transition-all duration-1000 ${
            isCritical
              ? 'bg-red-500'
              : isWarning
              ? 'bg-amber-500'
              : 'bg-brand-500'
          }`}
          style={{ width: `${progressPercent}%` }}
        />
      </div>
    </div>
  );
}
