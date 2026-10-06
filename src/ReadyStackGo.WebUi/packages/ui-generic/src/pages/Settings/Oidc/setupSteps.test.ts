import { describe, expect, it } from "vitest";
import type { CheckReportDto, SetupSessionDto } from "@rsgo/core";
import {
  canContinue,
  discoveryPassed,
  initialStep,
  isValidProviderName,
  redirectUriFor,
  setupSteps,
  testBadge,
} from "./setupSteps";

const report = (passed: boolean, failedCount = passed ? 0 : 1): CheckReportDto => ({
  items: [],
  executedCount: 3,
  failedCount,
  passed,
  current: true,
});

const session = (overrides: Partial<SetupSessionDto> = {}): SetupSessionDto => ({
  id: "s1",
  isNew: true,
  templateId: "wysch",
  registrationKind: "pairing",
  interaction: "connect",
  requireHttps: true,
  name: "wysch",
  displayName: "WYSCH",
  scopes: "openid profile email",
  baseUrlConfigured: false,
  hasClientSecret: false,
  registrationPending: false,
  registeredInSession: false,
  trustUnverifiedEmail: false,
  canEnable: false,
  template: {
    id: "wysch",
    name: "WYSCH",
    description: "",
    setupDescription: "",
    hasFixedAuthority: true,
    registrationKind: "pairing",
    interaction: "connect",
    requireHttps: true,
  },
  ...overrides,
});

const generic = (overrides: Partial<SetupSessionDto> = {}) =>
  session({
    templateId: "generic-oidc",
    registrationKind: "manual",
    interaction: "manualEntry",
    requireHttps: false,
    template: {
      id: "generic-oidc",
      name: "Generic OIDC",
      description: "",
      setupDescription: "",
      hasFixedAuthority: false,
      registrationKind: "manual",
      interaction: "manualEntry",
      requireHttps: false,
    },
    ...overrides,
  });

describe("setupSteps", () => {
  it("WYSCH has no provider address step and connects", () => {
    expect(setupSteps({ interaction: "connect", hasFixedAuthority: true }).map((s) => s.label)).toEqual([
      "Template",
      "This installation",
      "Connect",
      "Test",
      "Save",
    ]);
  });

  it("Generic OIDC asks for the provider address and registers manually", () => {
    expect(setupSteps({ interaction: "manualEntry", hasFixedAuthority: false }).map((s) => s.label)).toEqual([
      "Template",
      "Provider address",
      "This installation",
      "Register",
      "Test",
      "Save",
    ]);
  });
});

describe("canContinue", () => {
  it("template needs a selection", () => {
    expect(canContinue("template", null, { templateSelected: false })).toBe(false);
    expect(canContinue("template", null, { templateSelected: true })).toBe(true);
  });

  it("provider address is blocked only after a failed check of the same address", () => {
    expect(canContinue("provider", generic(), {})).toBe(true);
    const failed = generic({ discovery: report(false) });
    expect(canContinue("provider", failed, { authorityUnchangedSinceCheck: true })).toBe(false);
    expect(canContinue("provider", failed, { authorityUnchangedSinceCheck: false })).toBe(true);
    expect(canContinue("provider", generic({ discovery: report(true) }), { authorityUnchangedSinceCheck: true })).toBe(true);
  });

  it("installation needs a valid address and name", () => {
    expect(canContinue("installation", session(), { baseUrlValid: false })).toBe(false);
    expect(canContinue("installation", session(), { baseUrlValid: true, nameValid: false })).toBe(false);
    expect(canContinue("installation", session(), { baseUrlValid: true, nameValid: true })).toBe(true);
  });

  it("register needs a client ID, whitespace does not count", () => {
    expect(canContinue("register", generic(), { clientId: "  " })).toBe(false);
    expect(canContinue("register", generic(), { clientId: "rsgo" })).toBe(true);
  });

  it("connect needs credentials from this session and no pending or failed pairing", () => {
    expect(canContinue("connect", session(), {})).toBe(false);
    // Reconnect: the credentials of the existing provider do not count.
    expect(canContinue("connect", session({ isNew: false, clientId: "old" }), {})).toBe(false);
    expect(canContinue("connect", session({ registeredInSession: true, registrationPending: true }), {})).toBe(false);
    expect(canContinue("connect", session({ registeredInSession: true, registrationError: "access_denied" }), {})).toBe(false);
    expect(canContinue("connect", session({ clientId: "c", registeredInSession: true }), {})).toBe(true);
  });

  it("save has no Continue", () => {
    expect(canContinue("save", session({ clientId: "c", canEnable: true }), {})).toBe(false);
  });
});

describe("initialStep", () => {
  it("a fresh WYSCH session starts at this installation, even with a configured base URL", () => {
    expect(initialStep(session({ baseUrl: "https://rsgo.example.com", redirectUri: "https://rsgo.example.com/api/auth/oidc/wysch/callback" }))).toBe(
      "installation",
    );
  });

  it("a fresh generic session starts at the provider address", () => {
    expect(initialStep(generic())).toBe("provider");
  });

  it("after a passed discovery the generic session continues at this installation", () => {
    expect(initialStep(generic({ discovery: report(true) }))).toBe("installation");
  });

  it("back from the provider with a code or an error opens the connect step", () => {
    expect(initialStep(session({ registrationPending: true }))).toBe("connect");
    expect(initialStep(session({ registrationError: "access_denied" }))).toBe("connect");
  });

  it("results of checks or a test sign-in open the test step", () => {
    expect(initialStep(session({ clientId: "c", checks: report(true) }))).toBe("test");
    expect(
      initialStep(
        session({ clientId: "c", testSignIn: { passed: true, current: true, at: "2026-10-06T10:00:00Z", claims: [] } }),
      ),
    ).toBe("test");
  });
});

describe("helpers", () => {
  it("discoveryPassed needs a passed report without failures", () => {
    expect(discoveryPassed(null)).toBe(false);
    expect(discoveryPassed(report(true))).toBe(true);
    expect(discoveryPassed(report(false))).toBe(false);
  });

  it("validates provider names like the server", () => {
    expect(isValidProviderName("wysch")).toBe(true);
    expect(isValidProviderName("wysch-2")).toBe(true);
    expect(isValidProviderName("")).toBe(false);
    expect(isValidProviderName("-wysch")).toBe(false);
    expect(isValidProviderName("WYSCH")).toBe(false);
    expect(isValidProviderName("a".repeat(41))).toBe(false);
    expect(isValidProviderName("../x")).toBe(false);
  });

  it("builds the redirect URI without a double slash", () => {
    expect(redirectUriFor("https://rsgo.example.com/", "wysch")).toBe("https://rsgo.example.com/api/auth/oidc/wysch/callback");
  });

  it("badge: passed, failed or not tested", () => {
    expect(testBadge(session({ canEnable: true })).label).toBe("Test sign-in passed");
    expect(testBadge(session({ checks: report(false) })).label).toBe("Test failed");
    expect(
      testBadge(session({ testSignIn: { passed: false, current: true, at: "2026-10-06T10:00:00Z", claims: [] } })).label,
    ).toBe("Test failed");
    expect(testBadge(session()).label).toBe("Not tested");
  });
});
