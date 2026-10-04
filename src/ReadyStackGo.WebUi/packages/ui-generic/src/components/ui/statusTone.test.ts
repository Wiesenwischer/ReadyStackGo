import { describe, expect, it } from "vitest";
import { toneForHealthStatus, toneForOperationMode, toneForProductStatus } from "./statusTone";

describe("toneForProductStatus", () => {
  it.each([
    ["Running", "healthy"],
    ["Deploying", "progress"],
    ["Upgrading", "progress"],
    ["Failed", "unhealthy"],
    ["PartiallyRunning", "degraded"],
    ["Stopped", "unknown"],
    ["Removing", "unknown"],
  ])("maps %s to %s", (status, tone) => {
    expect(toneForProductStatus(status)).toBe(tone);
  });

  it.each([null, undefined, "", "SomethingNew"])("keeps %s neutral", (status) => {
    expect(toneForProductStatus(status as string | null | undefined)).toBe("unknown");
  });

  it("never uses an orange-like tone for Stopped", () => {
    // Stopped used to be orange; orange is reserved for the brand accent.
    expect(toneForProductStatus("Stopped")).not.toBe("degraded");
  });
});

describe("toneForHealthStatus", () => {
  it.each([
    ["healthy", "healthy"],
    ["Healthy", "healthy"],
    ["degraded", "degraded"],
    ["unhealthy", "unhealthy"],
    ["notfound", "unknown"],
    ["unknown", "unknown"],
  ])("maps %s to %s", (status, tone) => {
    expect(toneForHealthStatus(status)).toBe(tone);
  });

  it.each([null, undefined, ""])("keeps %s neutral", (status) => {
    expect(toneForHealthStatus(status as string | null | undefined)).toBe("unknown");
  });
});

describe("toneForOperationMode", () => {
  it.each([
    ["Normal", "healthy"],
    ["Migrating", "progress"],
    ["Maintenance", "degraded"],
    ["Failed", "unhealthy"],
    ["Stopped", "unknown"],
    [null, "unknown"],
  ])("maps %s to %s", (mode, tone) => {
    expect(toneForOperationMode(mode as string | null)).toBe(tone);
  });
});
