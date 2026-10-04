// Maps the status values of the API to the tones of StatusBadge
// (design: docs/specs/theme-und-logo/entwurf, component "Status Badge").
// Orange is the brand accent and never stands for a status.

export type StatusTone = "healthy" | "degraded" | "unhealthy" | "unknown" | "progress";

/** Product deployment status (Running, Deploying, Failed, PartiallyRunning, Stopped, Removing, ...). */
export function toneForProductStatus(status: string | null | undefined): StatusTone {
  switch ((status ?? "").toLowerCase()) {
    case "running":
      return "healthy";
    case "deploying":
    case "upgrading":
      return "progress";
    case "failed":
      return "unhealthy";
    case "partiallyrunning":
      return "degraded";
    default:
      // Stopped, Removing and anything unknown stay neutral.
      return "unknown";
  }
}

/** Stack/service health (healthy, degraded, unhealthy, notfound, unknown). */
export function toneForHealthStatus(status: string | null | undefined): StatusTone {
  switch ((status ?? "").toLowerCase()) {
    case "healthy":
      return "healthy";
    case "degraded":
      return "degraded";
    case "unhealthy":
      return "unhealthy";
    default:
      return "unknown";
  }
}

/** Operation mode (normal, migrating, maintenance, stopped, failed). */
export function toneForOperationMode(mode: string | null | undefined): StatusTone {
  switch ((mode ?? "").toLowerCase()) {
    case "normal":
      return "healthy";
    case "migrating":
      return "progress";
    case "maintenance":
      return "degraded";
    case "failed":
      return "unhealthy";
    default:
      return "unknown";
  }
}

/** Token classes of a tone, for pills that are not rendered with StatusBadge. */
export function toneClasses(tone: StatusTone): { bgColor: string; textColor: string; dotColor: string } {
  switch (tone) {
    case "healthy":
      return { bgColor: "bg-status-healthy-bg", textColor: "text-status-healthy", dotColor: "bg-status-healthy" };
    case "degraded":
      return { bgColor: "bg-status-degraded-bg", textColor: "text-status-degraded", dotColor: "bg-status-degraded" };
    case "unhealthy":
      return { bgColor: "bg-status-unhealthy-bg", textColor: "text-status-unhealthy", dotColor: "bg-status-unhealthy" };
    case "progress":
      return { bgColor: "bg-primary-subtle", textColor: "text-fg-brand", dotColor: "bg-primary" };
    default:
      return { bgColor: "bg-status-unknown-bg", textColor: "text-status-unknown", dotColor: "bg-status-unknown" };
  }
}
