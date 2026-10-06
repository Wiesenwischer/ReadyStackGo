import type { CheckReportDto, SetupSessionDto } from "@rsgo/core";

// Pure logic of "Add provider" (plan docs/plans/identity-provider-vorlagen.md E10–E14;
// design docs/specs/identity-provider-vorlagen/entwurf, frames "add provider: …").

export type SetupStepId = "template" | "provider" | "installation" | "connect" | "register" | "test" | "save";

export interface SetupStep {
  id: SetupStepId;
  label: string;
}

/** Template data the steps depend on (before a session exists only the template is known). */
export interface StepSource {
  interaction: "manualEntry" | "connect";
  hasFixedAuthority: boolean;
}

/**
 * The steps of a template: WYSCH → Template, This installation, Connect, Test, Save;
 * Generic OIDC → Template, Provider address, This installation, Register, Test, Save.
 * A step without content (authority fixed by the template) does not appear.
 */
export function setupSteps(source: StepSource): SetupStep[] {
  const steps: SetupStep[] = [{ id: "template", label: "Template" }];
  if (!source.hasFixedAuthority) steps.push({ id: "provider", label: "Provider address" });
  steps.push({ id: "installation", label: "This installation" });
  steps.push(source.interaction === "connect" ? { id: "connect", label: "Connect" } : { id: "register", label: "Register" });
  steps.push({ id: "test", label: "Test" }, { id: "save", label: "Save" });
  return steps;
}

export function stepSourceOf(session: SetupSessionDto): StepSource {
  return {
    interaction: session.interaction,
    hasFixedAuthority: session.template?.hasFixedAuthority ?? !!session.authority,
  };
}

/** Discovery checks 1–3 passed for the entered address. */
export function discoveryPassed(report: CheckReportDto | null | undefined): boolean {
  return !!report && report.passed && report.failedCount === 0;
}

/**
 * Whether "Continue" is possible in a step. Steps that send their input on "Continue"
 * (installation, register) only need valid input; the server validates again.
 */
export function canContinue(
  step: SetupStepId,
  session: SetupSessionDto | null,
  input: { templateSelected?: boolean; authorityUnchangedSinceCheck?: boolean; baseUrlValid?: boolean; nameValid?: boolean; clientId?: string },
): boolean {
  switch (step) {
    case "template":
      return !!input.templateSelected;
    case "provider":
      // A failed check blocks until the address changes; an unchecked address is checked on Continue.
      return !(session?.discovery && !discoveryPassed(session.discovery) && input.authorityUnchangedSinceCheck);
    case "installation":
      return !!input.baseUrlValid && (input.nameValid ?? true);
    case "register":
      return !!input.clientId?.trim();
    case "connect":
      return !!session?.registeredInSession && !session.registrationPending && !session.registrationError;
    case "test":
      return !!session?.clientId;
    case "save":
      return false;
  }
}

/** The step to show when a session is opened (also after a return from the provider). */
export function initialStep(session: SetupSessionDto): SetupStepId {
  const steps = setupSteps(stepSourceOf(session)).map((s) => s.id);
  if (session.testSignIn || session.checks) return "test";
  if (session.registrationPending || session.registrationError || session.registeredInSession || (session.isNew && session.clientId)) {
    return steps.includes("connect") ? "connect" : "register";
  }
  if (steps.includes("provider") && !discoveryPassed(session.discovery)) return "provider";
  return "installation";
}

/** Provider names: lowercase letters, digits and dashes, 1–40, starting with a letter or digit. */
export function isValidProviderName(name: string): boolean {
  return /^[a-z0-9][a-z0-9-]{0,39}$/.test(name);
}

/** Redirect URI of a provider, as the server builds it. */
export function redirectUriFor(baseUrl: string, name: string): string {
  return `${baseUrl.replace(/\/+$/, "")}/api/auth/oidc/${name}/callback`;
}

/** Label of the badge next to "Enable provider". */
export function testBadge(session: SetupSessionDto): { tone: "healthy" | "unhealthy" | "unknown"; label: string } {
  if (session.canEnable) return { tone: "healthy", label: "Test sign-in passed" };
  if ((session.checks && !session.checks.passed) || (session.testSignIn && !session.testSignIn.passed)) {
    return { tone: "unhealthy", label: "Test failed" };
  }
  return { tone: "unknown", label: "Not tested" };
}
