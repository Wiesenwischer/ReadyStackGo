import { describe, expect, it } from "vitest";
import { lastResultLabel, relativeTime } from "./oidcFormat";

// Dates at 11:30 UTC are the same calendar day from UTC-11 to UTC+12, so the tests do not depend
// on the time zone of the machine (formatDate shows the local date).
const now = new Date("2026-10-12T11:30:00Z");

describe("lastResultLabel", () => {
  it("recent results show the relative time", () => {
    expect(lastResultLabel({ kind: "signIn", passed: false, at: "2026-10-12T11:25:00Z" }, now)).toBe("Sign-in failed · 5 min ago");
    expect(lastResultLabel({ kind: "testSignIn", passed: true, at: "2026-10-12T11:29:30Z" }, now)).toBe("Test passed · just now");
    expect(lastResultLabel({ kind: "checks", passed: true, at: "2026-10-12T08:30:00Z" }, now)).toBe("Test passed · 3 h ago");
  });

  it("older results show the date", () => {
    expect(lastResultLabel({ kind: "testSignIn", passed: true, at: "2026-10-02T11:30:00Z" }, now)).toBe("Test passed · Oct 2, 2026");
  });

  it("a failed test or check shows its reason, without one the time", () => {
    expect(lastResultLabel({ kind: "checks", passed: false, at: "2026-10-12T11:00:00Z", message: "Issuer does not match" }, now)).toBe(
      "Issuer does not match",
    );
    expect(lastResultLabel({ kind: "testSignIn", passed: false, at: "2026-10-12T08:30:00Z", message: null }, now)).toBe(
      "Test failed · 3 h ago",
    );
  });

  it("a failed sign-in keeps its time even with a message", () => {
    expect(lastResultLabel({ kind: "signIn", passed: false, at: "2026-10-12T11:28:00Z", message: "invalid_client" }, now)).toBe(
      "Sign-in failed · 2 min ago",
    );
  });
});

describe("relativeTime", () => {
  it("rounds down at the boundaries", () => {
    expect(relativeTime(new Date(now.getTime() - 59_000).toISOString(), now)).toBe("just now");
    expect(relativeTime(new Date(now.getTime() - 60_000).toISOString(), now)).toBe("1 min ago");
    expect(relativeTime(new Date(now.getTime() - 3_570_000).toISOString(), now)).toBe("59 min ago");
    expect(relativeTime(new Date(now.getTime() - 3_600_000).toISOString(), now)).toBe("1 h ago");
    expect(relativeTime(new Date(now.getTime() - 86_000_000).toISOString(), now)).toBe("23 h ago");
  });

  it("switches to the date after a day, and the future counts as now", () => {
    expect(relativeTime("2026-10-11T11:00:00Z", now)).toBe("Oct 11, 2026");
    expect(relativeTime("2026-10-12T13:00:00Z", now)).toBe("just now");
  });
});
