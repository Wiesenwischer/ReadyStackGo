import { describe, expect, it } from "vitest";
import { lastResultLabel, relativeTime } from "./oidcFormat";

const now = new Date("2026-10-12T12:00:00Z");

describe("lastResultLabel", () => {
  it("recent results show the relative time", () => {
    expect(lastResultLabel({ kind: "signIn", passed: false, at: "2026-10-12T11:55:00Z" }, now)).toBe("Sign-in failed · 5 min ago");
    expect(lastResultLabel({ kind: "test", passed: true, at: "2026-10-12T11:59:30Z" }, now)).toBe("Test passed · just now");
  });

  it("older results show the date", () => {
    expect(lastResultLabel({ kind: "test", passed: true, at: "2026-10-02T09:00:00Z" }, now)).toBe("Test passed · Oct 2, 2026");
  });

  it("a failed test shows its reason, without one the time", () => {
    expect(lastResultLabel({ kind: "checks", passed: false, at: "2026-10-12T11:00:00Z", message: "Issuer does not match" }, now)).toBe(
      "Issuer does not match",
    );
    expect(lastResultLabel({ kind: "test", passed: false, at: "2026-10-12T09:00:00Z", message: null }, now)).toBe("Test failed · 3 h ago");
  });

  it("a failed sign-in keeps its time even with a message", () => {
    expect(lastResultLabel({ kind: "signIn", passed: false, at: "2026-10-12T11:58:00Z", message: "invalid_client" }, now)).toBe(
      "Sign-in failed · 2 min ago",
    );
  });
});

describe("relativeTime", () => {
  it("switches to the date after a day", () => {
    expect(relativeTime("2026-10-11T11:00:00Z", now)).toBe("Oct 11, 2026");
    expect(relativeTime("2026-10-12T13:00:00Z", now)).toBe("just now");
  });
});
