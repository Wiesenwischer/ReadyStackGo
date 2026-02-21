import { useEffect, useRef } from 'react';
import { type DeploymentProgressUpdate, type ConnectionState } from '../../hooks/useDeploymentHub';
import { formatPhase } from './formatPhase';

export interface DeploymentProgressPanelProps {
  progressUpdate: DeploymentProgressUpdate | null;
  initContainerLogs: Record<string, string[]>;
  connectionState: ConnectionState;
  defaultMessage?: string;
}

export function DeploymentProgressPanel({
  progressUpdate,
  initContainerLogs,
  connectionState,
  defaultMessage = 'Starting deployment...',
}: DeploymentProgressPanelProps) {
  const logEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [initContainerLogs]);

  return (
    <div className="w-full">
      {/* Progress Bar */}
      <div className="mb-4">
        <div className="flex justify-between text-sm mb-1">
          <span className="text-gray-600 dark:text-gray-400">
            {formatPhase(progressUpdate?.phase) || 'Initializing'}
          </span>
          <span className="text-gray-600 dark:text-gray-400">
            {progressUpdate?.percentComplete ?? 0}%
          </span>
        </div>
        <div className="h-3 bg-gray-200 rounded-full dark:bg-gray-700 overflow-hidden">
          <div
            className="h-full bg-brand-600 rounded-full transition-all duration-500 ease-out"
            style={{ width: `${progressUpdate?.percentComplete ?? 0}%` }}
          />
        </div>
      </div>

      {/* Status Message */}
      <div className="text-center">
        <p className="text-sm text-gray-700 dark:text-gray-300 font-medium">
          {progressUpdate?.message || defaultMessage}
        </p>

        {/* Service Progress */}
        {progressUpdate && (progressUpdate.totalServices > 0 || progressUpdate.totalInitContainers > 0) && (
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-2">
            {progressUpdate.phase === 'PullingImages'
              ? `Images: ${progressUpdate.completedServices} / ${progressUpdate.totalServices}`
              : progressUpdate.phase === 'InitializingContainers'
                ? `Init Containers: ${progressUpdate.completedInitContainers} / ${progressUpdate.totalInitContainers}`
                : `Services: ${progressUpdate.completedServices} / ${progressUpdate.totalServices}`
            }
            {progressUpdate.currentService && (
              <span className="ml-2">
                (current: <span className="font-mono">{progressUpdate.currentService}</span>)
              </span>
            )}
          </p>
        )}
      </div>

      {/* Connection Status */}
      <div className="mt-6 flex items-center justify-center gap-2 text-xs text-gray-500 dark:text-gray-400">
        <span className={`w-2 h-2 rounded-full ${
          connectionState === 'connected' ? 'bg-green-500' :
          connectionState === 'connecting' ? 'bg-yellow-500' :
          connectionState === 'reconnecting' ? 'bg-yellow-500' :
          'bg-red-500'
        }`} />
        {connectionState === 'connected' ? 'Live updates' :
         connectionState === 'connecting' ? 'Connecting...' :
         connectionState === 'reconnecting' ? 'Reconnecting...' :
         'Updates unavailable'}
      </div>

      {/* Init Container Logs */}
      {Object.keys(initContainerLogs).length > 0 && (
        <div className="mt-6 w-full">
          <div className="px-3 py-2 text-xs font-medium text-gray-600 dark:text-gray-400 bg-gray-100 dark:bg-gray-800 rounded-t-lg">
            Init Container Logs
          </div>
          <div className="bg-gray-900 rounded-b-lg p-3 max-h-80 overflow-y-auto">
            {Object.entries(initContainerLogs).map(([name, lines]) => (
              <div key={name} className="mb-2 last:mb-0">
                <div className="text-xs font-bold text-blue-400 mb-1">{name}</div>
                {lines.map((line, i) => (
                  <div key={i} className="font-mono text-xs text-green-400 whitespace-pre-wrap break-all leading-relaxed">{line}</div>
                ))}
              </div>
            ))}
            <div ref={logEndRef} />
          </div>
        </div>
      )}
    </div>
  );
}
