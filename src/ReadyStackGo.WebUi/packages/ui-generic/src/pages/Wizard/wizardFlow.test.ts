import { describe, expect, it } from "vitest";
import type { WizardSsoRunDto } from "@rsgo/core";
import {
  canContinueFromMethod,
  currentStepIndex,
  errorContent,
  nextRunAction,
  showCountdown,
  viewForRun,
  waitingRows,
  wizardSteps,
} from "./wizardFlow";

const run = (overrides: Partial<WizardSsoRunDto> = {}): WizardSsoRunDto => ({
  state: "started",
  startedAt: "2026-10-06T10:00:00Z",
  expiresAt: "2026-10-06T10:15:00Z",
  templateId: "wysch",
  templateName: "WYSCH",
  baseUrl: "https://rsgo.example.com",
  registrationReturned: false,
  ...overrides,
});

const options = { templateName: "WYSCH", authority: "https://id.wysch.wiesenwischer.de/", windowOpen: true, providerSaved: false };

describe("steps and selection", () => {
  it("shows the sign-in method step only when a template is offered", () => {
    expect(wizardSteps(true)).toEqual(["Sign-in method", "Administrator", "Email"]);
    expect(wizardSteps(false)).toEqual(["Administrator", "Email"]);
  });

  it("maps views to the step indicator", () => {
    expect(currentStepIndex("method", true)).toBe(0);
    expect(currentStepIndex("admin", true)).toBe(1);
    expect(currentStepIndex("ssoWaiting", true)).toBe(1);
    expect(currentStepIndex("smtp", true)).toBe(2);
    expect(currentStepIndex("admin", false)).toBe(0);
    expect(currentStepIndex("smtp", false)).toBe(1);
  });

  it("Continue needs a selection", () => {
    expect(canContinueFromMethod(null)).toBe(false);
    expect(canContinueFromMethod("")).toBe(false);
    expect(canContinueFromMethod("built-in")).toBe(true);
    expect(canContinueFromMethod("wysch")).toBe(true);
  });

  it("hides the countdown while waiting and once an administrator exists", () => {
    expect(showCountdown("method")).toBe(true);
    expect(showCountdown("ssoAddress")).toBe(true);
    expect(showCountdown("ssoWaiting")).toBe(false);
    expect(showCountdown("ssoSignedIn")).toBe(false);
    expect(showCountdown("smtp")).toBe(false);
  });
});

describe("run states", () => {
  it("maps each state to a view", () => {
    expect(viewForRun(run({ registrationReturned: true }))).toBe("ssoWaiting");
    expect(viewForRun(run())).toBe("ssoAddress");
    expect(viewForRun(run({ state: "registered" }))).toBe("ssoWaiting");
    expect(viewForRun(run({ state: "signedIn" }))).toBe("ssoSignedIn");
    expect(viewForRun(run({ state: "failed", failureReason: "cancelled" }))).toBe("ssoError");
  });

  it("continues automatically only where nothing is asked from the user", () => {
    expect(nextRunAction(run({ registrationReturned: true }))).toBe("continue");
    expect(nextRunAction(run())).toBe("none");
    expect(nextRunAction(run({ state: "registered" }))).toBe("signIn");
    expect(nextRunAction(run({ state: "signedIn" }))).toBe("none");
    expect(nextRunAction(run({ state: "failed" }))).toBe("none");
  });

  it("waiting rows follow the progress", () => {
    expect(waitingRows(run({ registrationReturned: true })).map((r) => r.result)).toEqual(["passed", "running", "skipped"]);
    expect(waitingRows(run({ state: "registered" })).map((r) => r.result)).toEqual(["passed", "passed", "running"]);
    expect(waitingRows(run())[0].title).toBe("Confirmed in WYSCH");
  });
});

describe("errorContent", () => {
  it("not reachable names the provider address and retries", () => {
    const c = errorContent("unreachable", { ...options, detail: "connection timed out." });
    expect(c.title).toBe("WYSCH is not reachable");
    expect(c.body).toContain("https://id.wysch.wiesenwischer.de/ (connection timed out)");
    expect(c.primary).toBe("retry");
    expect(c.builtIn).toBe(true);
  });

  it("cancelled offers Try again and built-in sign-in", () => {
    const c = errorContent("cancelled", options);
    expect(c.title).toBe("Connection cancelled");
    expect(c.primaryLabel).toBe("Try again");
    expect(c.builtIn).toBe(true);
  });

  it("expired with the window open starts again and mentions the disabled provider", () => {
    const c = errorContent("expired", { ...options, providerSaved: true });
    expect(c.primary).toBe("restart");
    expect(c.primaryLabel).toBe("Start again");
    expect(c.providerStaysDisabled).toBe(true);
  });

  it("expired with the window closed only shows the restart hint", () => {
    const c = errorContent("expired", { ...options, windowOpen: false });
    expect(c.primary).toBe("restartContainer");
    expect(c.builtIn).toBe(false);
    expect(c.body).toBe("The setup window has expired. To try again, restart the container.");
  });

  it("another path won: only go to sign-in", () => {
    const c = errorContent("completed_elsewhere", options);
    expect(c.primary).toBe("signIn");
    expect(c.builtIn).toBe(false);
  });

  it("another browser starts again", () => {
    expect(errorContent("foreign", options).primary).toBe("restart");
    expect(errorContent("foreign", { ...options, windowOpen: false }).primary).toBe("restartContainer");
  });

  it("unverified email asks to confirm it", () => {
    expect(errorContent("email_unverified", options).title).toBe("Your WYSCH email address is not confirmed");
  });

  it("unknown reasons fall back to the detail", () => {
    const c = errorContent("something_new", { ...options, detail: "Provider said no." });
    expect(c.title).toBe("Connection failed");
    expect(c.body).toBe("Provider said no.");
    expect(errorContent(null, options).body).toContain("WYSCH did not complete the connection");
  });
});
