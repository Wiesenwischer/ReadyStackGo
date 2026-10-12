import type { ReactNode } from "react";
import { AlertTriangleIcon, CheckCircleIcon, InfoIcon, XCircleIcon } from "../sso/icons";

// Inline message box (design: docs/specs/identity-provider-vorlagen/entwurf, component "Alert").

export type AlertTone = "info" | "success" | "warning" | "error";

const TONE: Record<AlertTone, { box: string; icon: string; Icon: typeof InfoIcon }> = {
  info: { box: "bg-primary-subtle border-primary/35", icon: "text-fg-brand", Icon: InfoIcon },
  success: { box: "bg-status-healthy-bg border-status-healthy/35", icon: "text-status-healthy", Icon: CheckCircleIcon },
  warning: { box: "bg-status-degraded-bg border-status-degraded/35", icon: "text-status-degraded", Icon: AlertTriangleIcon },
  error: { box: "bg-status-unhealthy-bg border-status-unhealthy/35", icon: "text-status-unhealthy", Icon: XCircleIcon },
};

export function Alert({
  tone,
  title,
  children,
  className = "",
  testId,
}: {
  tone: AlertTone;
  title: ReactNode;
  children?: ReactNode;
  className?: string;
  testId?: string;
}) {
  const t = TONE[tone];
  return (
    <div
      role={tone === "error" || tone === "warning" ? "alert" : "status"}
      data-testid={testId}
      data-tone={tone}
      className={`flex gap-3 rounded-xl border px-4 py-3.5 ${t.box} ${className}`}
    >
      <t.Icon size={20} className={`mt-px shrink-0 ${t.icon}`} />
      <div className="min-w-0 flex-1">
        <div className="text-sm font-semibold leading-5 text-fg">{title}</div>
        {children && <div className="mt-0.5 text-[13px] leading-[19px] text-fg-secondary">{children}</div>}
      </div>
    </div>
  );
}

export default Alert;
