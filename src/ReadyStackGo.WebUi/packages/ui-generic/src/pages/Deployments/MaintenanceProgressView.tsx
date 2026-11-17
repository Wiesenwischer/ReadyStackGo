import type { MaintenanceAction, MaintenanceStackStatus, MaintenanceProgressUpdate } from '@rsgo/core';

interface MaintenanceStack {
  stackName: string;
  stackDisplayName: string;
  order: number;
  serviceCount: number;
}

interface MaintenanceProgressViewProps {
  action: MaintenanceAction;
  stacks: MaintenanceStack[];
  stackStatuses: Record<string, MaintenanceStackStatus>;
  progress: MaintenanceProgressUpdate | null;
}

// Colour + wording differ between entering (stopping, yellow) and exiting (starting, green).
const THEME = {
  enter: {
    accent: 'yellow',
    title: 'Entering Maintenance Mode...',
    verb: 'Stopping',
    spinner: 'border-yellow-600',
    activeText: 'text-yellow-700 dark:text-yellow-300',
  },
  exit: {
    accent: 'green',
    title: 'Exiting Maintenance Mode...',
    verb: 'Starting',
    spinner: 'border-green-600',
    activeText: 'text-green-700 dark:text-green-300',
  },
} as const;

function StatusIcon({ status, spinnerClass }: { status: MaintenanceStackStatus; spinnerClass: string }) {
  if (status === 'active') {
    return <div className={`w-4 h-4 border-2 ${spinnerClass} border-t-transparent rounded-full animate-spin`} />;
  }
  if (status === 'done') {
    return (
      <svg className="w-4 h-4 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
      </svg>
    );
  }
  if (status === 'failed') {
    return (
      <svg className="w-4 h-4 text-red-600 dark:text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M6 18L18 6M6 6l12 12" />
      </svg>
    );
  }
  // pending
  return <div className="w-4 h-4 rounded-full border-2 border-gray-300 dark:border-gray-600" />;
}

export default function MaintenanceProgressView({
  action,
  stacks,
  stackStatuses,
  progress,
}: MaintenanceProgressViewProps) {
  const theme = THEME[action];

  const sortedStacks = stacks.slice().sort((a, b) => a.order - b.order);
  const doneCount = sortedStacks.filter(s => stackStatuses[s.stackName] === 'done').length;
  const totalStacks = progress?.totalStacks || sortedStacks.length;
  const currentStackNumber = progress?.stackIndex || Math.min(doneCount + 1, totalStacks);

  // Sub-heading: which stack, and how far through the stack list we are.
  const heading = progress?.currentStackDisplayName
    ? `${theme.verb} stack ${currentStackNumber} of ${totalStacks}: ${progress.currentStackDisplayName}`
    : `${theme.verb} containers...`;

  return (
    <div className="mx-auto max-w-screen-xl p-4 md:p-6 2xl:p-10">
      <div className="rounded-2xl border border-gray-200 bg-white p-8 dark:border-gray-800 dark:bg-white/[0.03]">
        <div className="flex flex-col items-center py-8">
          <div className={`w-16 h-16 mb-6 border-4 ${theme.spinner} border-t-transparent rounded-full animate-spin`}></div>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">
            {theme.title}
          </h1>
          <p className="text-gray-600 dark:text-gray-400 mb-6">
            {heading}
          </p>

          <div className="w-full max-w-lg">
            <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
              {sortedStacks.map((stack) => {
                const status = stackStatuses[stack.stackName] ?? 'pending';
                const isActive = status === 'active';
                // While a stack is active, show its per-container progress instead of the count.
                const showContainer = isActive
                  && progress?.currentStackName === stack.stackName
                  && !!progress?.currentContainer
                  && progress.totalContainers > 0;

                return (
                  <div
                    key={stack.stackName}
                    className="flex items-center justify-between px-4 py-3 border-b last:border-b-0 border-gray-200 dark:border-gray-700"
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      <StatusIcon status={status} spinnerClass={theme.spinner} />
                      <span
                        className={`text-sm font-medium truncate ${
                          isActive
                            ? theme.activeText
                            : status === 'failed'
                              ? 'text-red-700 dark:text-red-300'
                              : status === 'pending'
                                ? 'text-gray-400 dark:text-gray-500'
                                : 'text-gray-700 dark:text-gray-300'
                        }`}
                      >
                        {stack.stackDisplayName}
                      </span>
                    </div>
                    <span className="text-xs text-gray-500 dark:text-gray-400 whitespace-nowrap pl-3">
                      {showContainer
                        ? `${progress!.currentContainer} (${progress!.containerIndex}/${progress!.totalContainers})`
                        : `${stack.serviceCount} service${stack.serviceCount !== 1 ? 's' : ''}`}
                    </span>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
