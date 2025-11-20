import { type ReactNode } from 'react';

interface WizardLayoutProps {
  currentStep: number;
  totalSteps: number;
  children: ReactNode;
}

export default function WizardLayout({ currentStep, children }: WizardLayoutProps) {
  // v0.4: Simplified from 4 steps to 3 steps (Connections removed)
  const steps = [
    { number: 1, name: 'Admin', description: 'Create admin user' },
    { number: 2, name: 'Organization', description: 'Set organization' },
    { number: 3, name: 'Complete', description: 'Finish setup' },
  ];

  return (
    <div className="relative min-h-screen p-6 bg-white dark:bg-gray-900">
      <div className="flex flex-col items-center justify-center w-full min-h-screen">
        {/* Header */}
        <div className="w-full max-w-4xl mb-8">
          <div className="text-center mb-8">
            <h1 className="mb-2 text-3xl font-bold text-gray-800 dark:text-white">
              ReadyStackGo Setup Wizard
            </h1>
            <p className="text-gray-500 dark:text-gray-400">
              Let's get your system configured in 3 easy steps
            </p>
          </div>

          {/* Step Progress Indicator */}
          <div className="flex items-center justify-between mb-12">
            {steps.map((step, index) => (
              <div key={step.number} className="flex items-center flex-1">
                {/* Step Circle */}
                <div className="flex flex-col items-center flex-shrink-0">
                  <div
                    className={`flex items-center justify-center w-12 h-12 rounded-full border-2 transition-colors ${
                      currentStep >= step.number
                        ? 'bg-brand-600 border-brand-600 text-white'
                        : 'bg-white dark:bg-gray-800 border-gray-300 dark:border-gray-700 text-gray-400 dark:text-gray-500'
                    }`}
                  >
                    {currentStep > step.number ? (
                      <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                      </svg>
                    ) : (
                      <span className="text-sm font-semibold">{step.number}</span>
                    )}
                  </div>
                  <div className="mt-2 text-center">
                    <p className={`text-sm font-medium ${
                      currentStep >= step.number
                        ? 'text-gray-800 dark:text-white'
                        : 'text-gray-400 dark:text-gray-500'
                    }`}>
                      {step.name}
                    </p>
                    <p className="text-xs text-gray-400 dark:text-gray-600 hidden sm:block">
                      {step.description}
                    </p>
                  </div>
                </div>

                {/* Connector Line */}
                {index < steps.length - 1 && (
                  <div className="flex-1 h-0.5 mx-4 bg-gray-300 dark:bg-gray-700">
                    <div
                      className={`h-full transition-all duration-300 ${
                        currentStep > step.number ? 'bg-brand-600' : 'bg-transparent'
                      }`}
                    />
                  </div>
                )}
              </div>
            ))}
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
