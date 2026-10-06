import { type ReactNode } from 'react';
import type { WizardTimeoutInfo } from '@rsgo/core';
import WizardCountdown from '../../components/wizard/WizardCountdown';
import { Logo } from '../../components/brand/Logo';
import { Stepper } from '../../components/ui/Stepper';

// Wizard frame (design: docs/specs/identity-provider-vorlagen/entwurf, frames "Wizard / …"):
// logo, title, subtitle, countdown of the setup window, step indicator and a 672px card.

interface WizardLayoutProps {
  children: ReactNode;
  subtitle: string;
  timeout?: WizardTimeoutInfo | null;
  onTimeout?: () => void;
  showCountdown?: boolean;
  steps?: string[];
  currentStep?: number;
}

export default function WizardLayout({
  children,
  subtitle,
  timeout,
  onTimeout,
  showCountdown = true,
  steps,
  currentStep = 0,
}: WizardLayoutProps) {
  return (
    <div className="relative min-h-screen bg-page px-4 py-10">
      <div className="mx-auto flex w-full max-w-2xl flex-col items-center gap-6">
        <div className="flex flex-col items-center gap-2 text-center">
          <Logo context="page" height={32} />
          <h1 className="mt-1 text-[28px] font-bold leading-9 text-fg">Welcome to ReadyStackGo</h1>
          <p className="text-[15px] text-fg-secondary">{subtitle}</p>
          {showCountdown && timeout && onTimeout && !timeout.isTimedOut && (
            <div className="mt-1.5 flex justify-center">
              <WizardCountdown timeout={timeout} onTimeout={onTimeout} />
            </div>
          )}
        </div>

        {steps && steps.length > 1 && <Stepper steps={steps} current={currentStep} testId="wizard-steps" />}

        <div className="w-full rounded-[20px] border border-line bg-surface p-8 shadow-[0_8px_24px_-8px_rgba(13,38,43,0.08)]">
          {children}
        </div>
      </div>
    </div>
  );
}
