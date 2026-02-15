import { type ReactNode } from 'react';
import type { WizardTimeoutInfo } from '../../api/wizard';
import WizardCountdown from '../../components/wizard/WizardCountdown';

interface WizardLayoutProps {
  children: ReactNode;
  timeout?: WizardTimeoutInfo | null;
  onTimeout?: () => void;
}

export default function WizardLayout({ children, timeout, onTimeout }: WizardLayoutProps) {
  return (
    <div className="relative min-h-screen p-6 bg-white dark:bg-gray-900">
      <div className="flex flex-col items-center justify-center w-full min-h-screen">
        {/* Header */}
        <div className="w-full max-w-2xl mb-8">
          <div className="text-center mb-8">
            <h1 className="mb-2 text-3xl font-bold text-gray-800 dark:text-white">
              Welcome to ReadyStackGo
            </h1>
            <p className="text-gray-500 dark:text-gray-400">
              Create your admin account to get started
            </p>
            {/* Timeout Countdown */}
            {timeout && onTimeout && !timeout.isTimedOut && (
              <div className="mt-4 flex justify-center">
                <WizardCountdown timeout={timeout} onTimeout={onTimeout} />
              </div>
            )}
          </div>
        </div>

        {/* Content Area */}
        <div className="w-full max-w-2xl">
          <div className="p-8 bg-white border border-gray-200 rounded-lg shadow-sm dark:bg-gray-800 dark:border-gray-700">
            {children}
          </div>
        </div>
      </div>
    </div>
  );
}
