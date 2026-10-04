import type React from "react";
import { toneClasses, type StatusTone } from "./statusTone";

// Status pill: dot + label (design: docs/specs/theme-und-logo/entwurf, "Status Badge").

interface StatusBadgeProps {
  tone: StatusTone;
  children: React.ReactNode;
  /** Optional icon shown instead of the dot. */
  icon?: React.ReactNode;
  className?: string;
}

export function StatusBadge({ tone, children, icon, className = "" }: StatusBadgeProps) {
  const classes = toneClasses(tone);
  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-medium ${classes.bgColor} ${classes.textColor} ${className}`}
      data-tone={tone}
    >
      {icon ?? <span aria-hidden="true" className={`h-2 w-2 rounded-full ${classes.dotColor}`} />}
      {children}
    </span>
  );
}

export default StatusBadge;
