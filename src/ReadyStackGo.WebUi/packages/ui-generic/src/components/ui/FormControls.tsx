import { useEffect, useId, useState, type InputHTMLAttributes, type ReactNode } from "react";
import { CheckIcon, CopyIcon } from "../sso/icons";

// Form controls of the single sign-on design (docs/specs/identity-provider-vorlagen/entwurf,
// components "Input", "Copy Field", "Toggle").

const fieldClasses = (state: "default" | "error" | "readonly" = "default") =>
  [
    "h-11 w-full rounded-[10px] border px-3.5 text-sm text-fg outline-none transition-colors placeholder:text-fg-muted",
    "focus:border-primary focus:ring-1 focus:ring-primary disabled:bg-raised disabled:text-fg-muted",
    state === "error"
      ? "border-status-unhealthy"
      : state === "readonly"
        ? "border-line-strong/55 bg-raised"
        : "border-line-strong/55 bg-surface",
  ].join(" ");

type TextFieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, "id"> & {
  label: ReactNode;
  hint?: ReactNode;
  error?: ReactNode;
  testId?: string;
};

/** Label, 44px field and hint; an error replaces the hint in the status color. */
export function TextField({ label, hint, error, testId, readOnly, className = "", ...rest }: TextFieldProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const message = error ?? hint;
  return (
    <div className={`flex flex-col gap-1.5 ${className}`}>
      <label htmlFor={id} className="text-sm font-medium text-fg">
        {label}
      </label>
      <input
        id={id}
        data-testid={testId}
        readOnly={readOnly}
        aria-invalid={error ? true : undefined}
        aria-describedby={message ? hintId : undefined}
        className={fieldClasses(error ? "error" : readOnly ? "readonly" : "default")}
        {...rest}
      />
      {message && (
        <p id={hintId} className={`text-xs ${error ? "text-status-unhealthy" : "text-fg-muted"}`}>
          {message}
        </p>
      )}
    </div>
  );
}

/** Read-only value in monospace with a Copy button that shows "Copied" for 2 s. */
export function CopyField({ label, value, testId }: { label: ReactNode; value: string; testId?: string }) {
  const id = useId();
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timer = window.setTimeout(() => setCopied(false), 2000);
    return () => window.clearTimeout(timer);
  }, [copied]);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
    } catch {
      // Clipboard not available (insecure context): select the text so it can be copied by hand.
      (document.getElementById(id) as HTMLInputElement | null)?.select();
    }
  };

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium text-fg">
        {label}
      </label>
      <div className="flex gap-2">
        <input
          id={id}
          readOnly
          value={value}
          data-testid={testId}
          onFocus={(e) => e.currentTarget.select()}
          className="h-11 min-w-0 flex-1 rounded-[10px] border border-line bg-raised px-3.5 font-mono text-[13px] text-fg outline-none focus:border-primary"
        />
        <button
          type="button"
          onClick={copy}
          className="inline-flex h-11 shrink-0 items-center gap-2 rounded-[10px] border border-line-strong bg-surface px-4 text-sm font-semibold transition-colors hover:bg-raised focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus"
        >
          {copied ? (
            <>
              <CheckIcon size={16} className="text-status-healthy" />
              <span className="text-status-healthy">Copied</span>
            </>
          ) : (
            <>
              <CopyIcon size={16} className="text-fg" />
              <span className="text-fg">Copy</span>
            </>
          )}
        </button>
      </div>
    </div>
  );
}

/** Switch with label and description; disabled keeps its position (e.g. locked on). */
export function Toggle({
  checked,
  onChange,
  disabled,
  label,
  description,
  extra,
  testId,
}: {
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  label: ReactNode;
  description?: ReactNode;
  extra?: ReactNode;
  testId?: string;
}) {
  const id = useId();
  return (
    <div className="flex gap-3.5">
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        aria-describedby={description ? `${id}-desc` : undefined}
        disabled={disabled}
        data-testid={testId}
        onClick={() => onChange(!checked)}
        className={`relative mt-px h-[22px] w-10 shrink-0 rounded-full transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus disabled:cursor-not-allowed disabled:opacity-45 ${
          checked ? "bg-primary" : "bg-line-strong/60"
        }`}
      >
        <span
          aria-hidden="true"
          className={`absolute top-0.5 h-[18px] w-[18px] rounded-full bg-white shadow transition-[left] ${checked ? "left-5" : "left-0.5"}`}
        />
      </button>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2.5">
          <label htmlFor={id} className={`text-sm font-medium ${disabled ? "text-fg-muted" : "text-fg"}`}>
            {label}
          </label>
          {extra}
        </div>
        {description && (
          <p id={`${id}-desc`} className="mt-0.5 text-[13px] text-fg-muted">
            {description}
          </p>
        )}
      </div>
    </div>
  );
}
