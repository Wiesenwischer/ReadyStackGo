import type { WizardSsoRunDto } from "@rsgo/core";

// Pure logic of the setup wizard with sign-in methods (plan docs/plans/identity-provider-vorlagen.md,
// E19/E20; design docs/specs/identity-provider-vorlagen/entwurf).

export type WizardView =
  | "method"
  | "admin"
  | "ssoAddress"
  | "ssoWaiting"
  | "ssoSignedIn"
  | "ssoError"
  | "smtp";

/** Selection of the built-in sign-in in the first step. */
export const BUILT_IN = "built-in";

export const STEPS_WITH_METHOD = ["Sign-in method", "Administrator", "Email"];
export const STEPS_WITHOUT_METHOD = ["Administrator", "Email"];

/** The steps of the indicator: "Sign-in method" only exists when a template is offered in setup. */
export function wizardSteps(hasMethodStep: boolean): string[] {
  return hasMethodStep ? STEPS_WITH_METHOD : STEPS_WITHOUT_METHOD;
}

/** Index of the current step in the indicator. */
export function currentStepIndex(view: WizardView, hasMethodStep: boolean): number {
  const offset = hasMethodStep ? 1 : 0;
  switch (view) {
    case "method":
      return 0;
    case "smtp":
      return 1 + offset;
    default:
      return offset;
  }
}

/** "Continue" of the first step is possible only with a selection. */
export function canContinueFromMethod(selection: string | null): boolean {
  return selection !== null && selection !== "";
}

/** The countdown of the setup window is hidden once the run waits or an administrator exists. */
export function showCountdown(view: WizardView): boolean {
  return view === "method" || view === "admin" || view === "ssoAddress" || view === "ssoError";
}

/** Where a wizard run of this browser stands, mapped to a view. */
export function viewForRun(run: WizardSsoRunDto): WizardView {
  switch (run.state) {
    case "signedIn":
      return "ssoSignedIn";
    case "registered":
      return "ssoWaiting";
    case "started":
      // Back from the provider with a code: finishing; otherwise the browser went back without one.
      return run.registrationReturned ? "ssoWaiting" : "ssoAddress";
    default:
      return "ssoError";
  }
}

/** What the wizard does next for a run, without user interaction. */
export function nextRunAction(run: WizardSsoRunDto): "continue" | "signIn" | "none" {
  if (run.state === "started" && run.registrationReturned) return "continue";
  if (run.state === "registered") return "signIn";
  return "none";
}

/** Progress rows of the waiting state ("Confirmed in WYSCH", "Receiving …", "Signing you in"). */
export function waitingRows(run: WizardSsoRunDto): { title: string; result: "passed" | "running" | "skipped" }[] {
  const registered = run.state === "registered" || run.state === "signedIn";
  return [
    { title: `Confirmed in ${run.templateName}`, result: "passed" },
    { title: "Receiving client ID and secret", result: registered ? "passed" : "running" },
    { title: "Signing you in", result: registered ? "running" : "skipped" },
  ];
}

export interface WizardErrorContent {
  title: string;
  body: string;
  /** "retry": "Try again" inside the run; "restart": start a new run; "signIn": go to sign-in; "restartContainer": only the restart hint. */
  primary: "retry" | "restart" | "signIn" | "restartContainer";
  primaryLabel: string;
  /** Show "Use built-in sign-in instead". */
  builtIn: boolean;
  /** Info that a paired provider stays disabled. */
  providerStaysDisabled: boolean;
  /** Icon tone. */
  tone: "error" | "warning";
}

/**
 * Text and actions of the error card for a failure reason (design frames 172:2794, 172:2859,
 * 172:2923; texts without frame from the design README and plan E27).
 */
export function errorContent(
  reason: string | null | undefined,
  options: { templateName: string; authority?: string | null; detail?: string | null; windowOpen: boolean; providerSaved: boolean },
): WizardErrorContent {
  const name = options.templateName;
  const base = { builtIn: true, providerStaysDisabled: false } as const;
  switch (reason) {
    case "unreachable":
      return {
        ...base,
        tone: "error",
        title: `${name} is not reachable`,
        body:
          `ReadyStackGo could not reach ${options.authority ?? name}` +
          `${options.detail ? ` (${options.detail.replace(/\.$/, "")})` : ""}. ` +
          `Check that this server has internet access, or continue with built-in sign-in and add ${name} later under Settings › Single Sign-On.`,
        primary: "retry",
        primaryLabel: "Try again",
      };
    case "cancelled":
      return {
        ...base,
        tone: "warning",
        title: "Connection cancelled",
        body: `The connection was cancelled in ${name}, so no administrator was created. Try again, or use built-in sign-in instead.`,
        primary: "retry",
        primaryLabel: "Try again",
      };
    case "limit_reached":
      return {
        ...base,
        tone: "warning",
        title: "Too many connected installations",
        body: `Your ${name} account has reached the maximum number of connected installations. Remove one in your ${name} account, then try again.`,
        primary: "retry",
        primaryLabel: "Try again",
      };
    case "email_unverified":
      return {
        ...base,
        tone: "warning",
        title: `Your ${name} email address is not confirmed`,
        body: `Confirm your email address in ${name}, then try again.`,
        primary: "retry",
        primaryLabel: "Try again",
      };
    case "email_invalid":
      return {
        ...base,
        tone: "error",
        title: "This email address cannot be used",
        body: `ReadyStackGo cannot use the email address of this ${name} account. Use built-in sign-in instead.`,
        primary: "retry",
        primaryLabel: "Try again",
      };
    case "completed_elsewhere":
      return {
        ...base,
        builtIn: false,
        tone: "warning",
        title: "Setup was completed elsewhere",
        body: "A system administrator was created in the meantime. Sign in instead.",
        primary: "signIn",
        primaryLabel: "Go to sign-in",
      };
    case "foreign":
      return {
        ...base,
        tone: "warning",
        title: "This sign-in run belongs to another browser",
        body: "Start the setup again in this browser.",
        primary: options.windowOpen ? "restart" : "restartContainer",
        primaryLabel: "Start again",
      };
    case "expired":
      return {
        ...base,
        tone: "warning",
        title: "Time window expired",
        body: options.windowOpen
          ? `This sign-in run started more than 15 minutes ago and has expired. Start again to connect with ${name}, or use built-in sign-in.`
          : "The setup window has expired. To try again, restart the container.",
        primary: options.windowOpen ? "restart" : "restartContainer",
        primaryLabel: "Start again",
        builtIn: options.windowOpen,
        providerStaysDisabled: options.providerSaved,
      };
    default:
      return {
        ...base,
        tone: "error",
        title: "Connection failed",
        body: options.detail ?? `${name} did not complete the connection. Try again, or use built-in sign-in instead.`,
        primary: "retry",
        primaryLabel: "Try again",
      };
  }
}
