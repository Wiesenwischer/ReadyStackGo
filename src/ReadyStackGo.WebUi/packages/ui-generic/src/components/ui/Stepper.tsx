import { Fragment } from "react";
import { CheckIcon } from "../sso/icons";

// Step indicator (design: docs/specs/identity-provider-vorlagen/entwurf, component "Stepper Item").

export function Stepper({ steps, current, testId }: { steps: string[]; current: number; testId?: string }) {
  return (
    <ol className="flex flex-wrap items-center gap-3" aria-label="Progress" data-testid={testId}>
      {steps.map((label, i) => {
        const state = i < current ? "done" : i === current ? "current" : "upcoming";
        return (
          <Fragment key={label}>
            {i > 0 && <li aria-hidden="true" className="h-px w-7 bg-line-strong/50" />}
            <li
              className="flex items-center gap-2"
              aria-current={state === "current" ? "step" : undefined}
              data-state={state}
            >
              <span
                className={`flex h-6 w-6 items-center justify-center rounded-full text-xs ${
                  state === "done"
                    ? "bg-primary text-on-primary"
                    : state === "current"
                      ? "border-2 border-primary bg-primary-subtle font-semibold text-fg-brand"
                      : "border border-line-strong/60 bg-surface font-medium text-fg-muted"
                }`}
              >
                {state === "done" ? <CheckIcon size={14} /> : i + 1}
              </span>
              <span
                className={`text-[13px] ${
                  state === "current"
                    ? "font-semibold text-fg"
                    : state === "done"
                      ? "font-medium text-fg-secondary"
                      : "text-fg-muted"
                }`}
              >
                {label}
              </span>
            </li>
          </Fragment>
        );
      })}
    </ol>
  );
}

export default Stepper;
