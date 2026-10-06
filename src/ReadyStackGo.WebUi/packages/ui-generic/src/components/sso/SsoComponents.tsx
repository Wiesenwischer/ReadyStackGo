import type { KeyboardEvent, ReactNode } from "react";
import type { CheckItemDto, TestSignInClaimDto } from "@rsgo/core";
import { StatusBadge } from "../ui/StatusBadge";
import { CheckCircleIcon, KeyIcon, LoaderIcon, LockIcon, MinusCircleIcon, XCircleIcon } from "./icons";

// Single sign-on building blocks (design: docs/specs/identity-provider-vorlagen/entwurf, components
// "Provider Logo", "Provider Mark", "Provider Button", "Sign-in Option", "Check Row").

/**
 * Symbol of a provider template: its icon (SVG from the template, only through <img> so no
 * script runs), a lock for the built-in sign-in, otherwise a key.
 */
export function ProviderLogo({ iconUrl, kind, size = 24 }: { iconUrl?: string | null; kind?: "builtIn"; size?: number }) {
  if (kind === "builtIn") {
    return <LockIcon size={size} className="text-fg-brand" />;
  }
  if (iconUrl) {
    return <img src={iconUrl} alt="" width={size} height={size} className="object-contain" style={{ width: size, height: size }} />;
  }
  return <KeyIcon size={size} className="text-fg-secondary" />;
}

/** 44×44 tile with the provider symbol. */
export function ProviderMark({ iconUrl, kind }: { iconUrl?: string | null; kind?: "builtIn" }) {
  return (
    <span
      aria-hidden="true"
      className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border border-line bg-raised"
    >
      <ProviderLogo iconUrl={iconUrl} kind={kind} size={iconUrl ? 32 : 24} />
    </span>
  );
}

/** Login button of a provider: symbol + label, 48px high. */
export function ProviderButton({
  label,
  iconUrl,
  onClick,
  disabled,
  className = "",
  testId,
}: {
  label: string;
  iconUrl?: string | null;
  onClick?: () => void;
  disabled?: boolean;
  className?: string;
  testId?: string;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      data-testid={testId}
      className={`inline-flex h-12 items-center justify-center gap-2.5 rounded-[10px] border border-line-strong/55 bg-surface px-5 text-sm font-semibold text-fg transition-colors hover:border-line-strong hover:bg-raised focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus disabled:pointer-events-none disabled:opacity-45 ${className}`}
    >
      <ProviderLogo iconUrl={iconUrl} size={22} />
      {label}
    </button>
  );
}

/** Selectable tile for a sign-in method or template (same states as the theme tiles). */
export function SignInOptionCard({
  title,
  description,
  mark,
  selected,
  onSelect,
  tabIndex,
  onKeyDown,
  buttonRef,
  testId,
}: {
  title: string;
  description: string;
  mark: ReactNode;
  selected: boolean;
  onSelect: () => void;
  tabIndex: number;
  onKeyDown: (e: KeyboardEvent<HTMLButtonElement>) => void;
  buttonRef: (el: HTMLButtonElement | null) => void;
  testId?: string;
}) {
  return (
    <button
      ref={buttonRef}
      type="button"
      role="radio"
      aria-checked={selected}
      aria-label={title}
      aria-description={description}
      tabIndex={tabIndex}
      onClick={onSelect}
      onKeyDown={onKeyDown}
      data-testid={testId}
      className={`flex w-full flex-col gap-3.5 rounded-2xl bg-surface p-5 text-left outline-none transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus ${
        selected ? "border-2 border-primary p-[19px]" : "border border-line hover:border-line-strong hover:bg-raised"
      }`}
    >
      <span className="flex w-full items-start justify-between">
        {mark}
        <span
          aria-hidden="true"
          className={`flex h-5 w-5 items-center justify-center rounded-full ${
            selected ? "bg-primary" : "border-[1.5px] border-line-strong"
          }`}
        >
          {selected && <span className="h-2 w-2 rounded-full bg-on-primary" />}
        </span>
      </span>
      <span className="flex flex-col gap-1">
        <span className="text-base font-semibold text-fg">{title}</span>
        <span className="text-[13px] leading-[19px] text-fg-secondary">{description}</span>
      </span>
    </button>
  );
}

export type CheckRowResult = "passed" | "failed" | "running" | "skipped";

/** One check of the connection test with a plain-language detail. */
export function CheckRow({ result, title, detail, testId }: { result: CheckRowResult; title: ReactNode; detail?: ReactNode; testId?: string }) {
  const icon =
    result === "passed" ? (
      <CheckCircleIcon size={20} className="text-status-healthy" />
    ) : result === "failed" ? (
      <XCircleIcon size={20} className="text-status-unhealthy" />
    ) : result === "running" ? (
      <LoaderIcon size={20} className="text-fg-brand" />
    ) : (
      <MinusCircleIcon size={20} className="text-fg-muted" />
    );
  return (
    <li className="flex gap-3 py-3" data-testid={testId} data-result={result}>
      <span className="mt-px shrink-0">{icon}</span>
      <div className="min-w-0 flex-1">
        <div className={`text-sm font-medium ${result === "skipped" ? "text-fg-muted" : "text-fg"}`}>{title}</div>
        {detail && (
          <div className={`break-words text-[13px] ${result === "failed" ? "text-status-unhealthy" : "text-fg-secondary"}`}>{detail}</div>
        )}
      </div>
    </li>
  );
}

/** The checks of a report as rows. */
export function CheckList({ items, testId }: { items: CheckItemDto[]; testId?: string }) {
  return (
    <ul className="flex flex-col" data-testid={testId}>
      {items.map((item) => (
        <CheckRow key={item.id} result={item.status} title={item.title} detail={item.detail} testId={`check-${item.id}`} />
      ))}
    </ul>
  );
}

const CLAIM_BADGE: Record<TestSignInClaimDto["status"], { tone: "healthy" | "degraded" | "unknown"; label: string }> = {
  received: { tone: "healthy", label: "Received" },
  verified: { tone: "healthy", label: "Verified" },
  notVerified: { tone: "degraded", label: "Not verified" },
  missing: { tone: "unknown", label: "Missing" },
};

/** Details that arrived with a test sign-in. */
export function ClaimsTable({ claims }: { claims: TestSignInClaimDto[] }) {
  return (
    <div className="overflow-x-auto rounded-xl border border-line" data-testid="claims-table">
      <table className="w-full text-left text-[13px]">
        <thead className="bg-raised">
          <tr className="text-xs font-semibold text-fg-muted">
            <th className="px-4 py-2.5 font-semibold">Detail</th>
            <th className="px-4 py-2.5 font-semibold">Claim</th>
            <th className="px-4 py-2.5 font-semibold">Value</th>
            <th className="px-4 py-2.5 font-semibold">For sign-in</th>
          </tr>
        </thead>
        <tbody>
          {claims.map((c) => {
            const badge = CLAIM_BADGE[c.status];
            return (
              <tr key={c.detail} className="border-t border-line" data-testid={`claim-${c.claim}`}>
                <td className="px-4 py-3 font-medium text-fg">{c.detail}</td>
                <td className="px-4 py-3 font-mono text-fg-secondary">{c.claim}</td>
                <td className={`break-all px-4 py-3 ${c.value ? "text-fg" : "text-fg-muted"}`}>{c.value || "—"}</td>
                <td className="px-4 py-3">
                  <StatusBadge tone={badge.tone}>{badge.label}</StatusBadge>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
