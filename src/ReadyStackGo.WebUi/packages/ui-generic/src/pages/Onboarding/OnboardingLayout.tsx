import { type ReactNode } from 'react';

interface OnboardingLayoutProps {
  children: ReactNode;
  step: number;
  totalSteps: number;
}

export default function OnboardingLayout({ children, step, totalSteps }: OnboardingLayoutProps) {
  return (
    <div className="relative min-h-screen p-6 bg-white dark:bg-gray-900">
      <div className="flex flex-col items-center justify-center w-full min-h-screen">
        {/* Header */}
        <div className="w-full max-w-2xl mb-8">
          <div className="text-center mb-8">
            <h1 className="mb-2 text-3xl font-bold text-gray-800 dark:text-white">
              Set Up ReadyStackGo
            </h1>
            <p className="text-gray-500 dark:text-gray-400">
              Configure your instance step by step
            </p>
            {/* Step Indicator */}
            <div className="mt-6 flex items-center justify-center gap-2">
              {Array.from({ length: totalSteps }, (_, i) => (
                <div
                  key={i}
                  className={`h-2 rounded-full transition-all ${
                    i + 1 < step
                      ? 'w-8 bg-brand-600 dark:bg-brand-500'
                      : i + 1 === step
                        ? 'w-8 bg-brand-600 dark:bg-brand-500'
                        : 'w-8 bg-gray-200 dark:bg-gray-700'
                  }`}
                />
              ))}
            </div>
            <p className="mt-2 text-xs text-gray-400 dark:text-gray-500">
              Step {step} of {totalSteps}
            </p>
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
