import { useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { getOnboardingStatus, dismissOnboarding, type OnboardingStatusResponse } from '../../api/onboarding';

export default function OnboardingChecklist() {
  const [status, setStatus] = useState<OnboardingStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [dismissing, setDismissing] = useState(false);
  const navigate = useNavigate();

  const fetchStatus = useCallback(async () => {
    try {
      const data = await getOnboardingStatus();
      setStatus(data);
    } catch {
      // Silently fail — onboarding is non-critical
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchStatus();
  }, [fetchStatus]);

  const handleDismiss = async () => {
    setDismissing(true);
    try {
      await dismissOnboarding();
      setStatus(prev => prev ? { ...prev, isDismissed: true } : null);
    } catch {
      // Fallback: hide locally even if API fails
      setStatus(prev => prev ? { ...prev, isDismissed: true } : null);
    } finally {
      setDismissing(false);
    }
  };

  // Don't render while loading, if dismissed, or if data unavailable
  if (loading || !status || status.isDismissed) {
    return null;
  }

  const hasOrg = status.organization.done;

  // Border color: amber if org missing (required), blue if only optional items remain
  const borderColor = !hasOrg
    ? 'border-amber-400 dark:border-amber-500'
    : 'border-blue-400 dark:border-blue-500';
  const bgColor = !hasOrg
    ? 'bg-amber-50 dark:bg-amber-900/20'
    : 'bg-blue-50 dark:bg-blue-900/20';

  return (
    <div className={`mb-6 rounded-lg border-l-4 ${borderColor} ${bgColor} p-5`}>
      {/* Header */}
      <div className="flex items-start justify-between mb-4">
        <div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            Complete Your Setup
          </h3>
          <p className="text-sm text-gray-600 dark:text-gray-400 mt-0.5">
            {hasOrg
              ? 'Finish configuring your instance to get the most out of ReadyStackGo.'
              : 'Set up your organization to unlock all features.'}
          </p>
        </div>
        <button
          onClick={handleDismiss}
          disabled={dismissing}
          className="ml-4 text-gray-400 hover:text-gray-600 dark:text-gray-500 dark:hover:text-gray-300 transition-colors"
          title="Dismiss"
        >
          <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      {/* Checklist Items */}
      <div className="space-y-2.5">
        {/* Admin — always done (user is logged in) */}
        <ChecklistItem done label="Admin account created" />

        {/* Organization */}
        <ChecklistItem
          done={hasOrg}
          label={hasOrg
            ? `Organization configured: ${status.organization.name}`
            : 'Set up your organization'}
          actionLabel={hasOrg ? undefined : 'Configure'}
          onAction={() => navigate('/settings/organization')}
          required={!hasOrg}
        />

        {/* Environment */}
        <ChecklistItem
          done={status.environment.done}
          label={status.environment.done
            ? `${status.environment.count} environment${status.environment.count !== 1 ? 's' : ''} configured`
            : 'Add a Docker environment'}
          actionLabel={!status.environment.done && hasOrg ? 'Configure' : undefined}
          onAction={() => navigate('/environments')}
          disabled={!hasOrg}
          hint={!hasOrg ? 'Requires organization' : undefined}
        />

        {/* Stack Sources */}
        <ChecklistItem
          done={status.stackSources.done}
          label={status.stackSources.done
            ? `${status.stackSources.count} stack source${status.stackSources.count !== 1 ? 's' : ''} configured`
            : 'Configure stack sources'}
          actionLabel={!status.stackSources.done && hasOrg ? 'Configure' : undefined}
          onAction={() => navigate('/settings/stack-sources')}
          disabled={!hasOrg}
          hint={!hasOrg ? 'Requires organization' : undefined}
        />

        {/* Registries */}
        <ChecklistItem
          done={status.registries.done}
          label={status.registries.done
            ? `${status.registries.count} container registr${status.registries.count !== 1 ? 'ies' : 'y'} configured`
            : 'Set up container registries'}
          actionLabel={!status.registries.done && hasOrg ? 'Configure' : undefined}
          onAction={() => navigate('/settings/registries')}
          disabled={!hasOrg}
          hint={!hasOrg ? 'Requires organization' : undefined}
          optional
        />
      </div>

      {/* Footer Actions */}
      <div className="mt-4 flex items-center gap-3">
        <button
          onClick={handleDismiss}
          disabled={dismissing}
          className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300 transition-colors"
        >
          {dismissing ? 'Dismissing...' : 'Dismiss checklist'}
        </button>
      </div>
    </div>
  );
}

interface ChecklistItemProps {
  done: boolean;
  label: string;
  actionLabel?: string;
  onAction?: () => void;
  disabled?: boolean;
  required?: boolean;
  hint?: string;
  optional?: boolean;
}

function ChecklistItem({ done, label, actionLabel, onAction, disabled, hint, optional }: ChecklistItemProps) {
  return (
    <div className={`flex items-center gap-3 ${disabled ? 'opacity-50' : ''}`}>
      {/* Status Icon */}
      {done ? (
        <svg className="w-5 h-5 text-green-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      ) : (
        <svg className="w-5 h-5 text-gray-300 dark:text-gray-600 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <circle cx="12" cy="12" r="9" />
        </svg>
      )}

      {/* Label */}
      <span className={`text-sm flex-1 ${
        done
          ? 'text-gray-600 dark:text-gray-400'
          : 'text-gray-900 dark:text-white font-medium'
      }`}>
        {label}
        {hint && <span className="ml-1.5 text-xs text-gray-400 dark:text-gray-500">({hint})</span>}
        {optional && !done && !disabled && <span className="ml-1.5 text-xs text-gray-400 dark:text-gray-500">(optional)</span>}
      </span>

      {/* Action Button */}
      {actionLabel && onAction && !disabled && (
        <button
          onClick={onAction}
          className="text-sm font-medium text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300 transition-colors"
        >
          {actionLabel} &rarr;
        </button>
      )}
    </div>
  );
}
