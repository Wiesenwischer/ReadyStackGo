import { describe, expect, it } from "vitest";
import {
  themedPresentation,
  toneForHealthStatus,
  toneForOperationMode,
  toneForPresentationColor,
  toneForProductStatus,
} from "./statusTone";

describe("toneForPresentationColor", () => {
  it.each([
    ["green", "healthy"],
    ["yellow", "degraded"],
    ["red", "unhealthy"],
    ["blue", "progress"],
    ["orange", "unknown"],
    ["gray", "unknown"],
    ["", "unknown"],
  ])("maps %s to %s", (color, tone) => {
    expect(toneForPresentationColor(color)).toBe(tone);
  });
});

describe("themedPresentation", () => {
  it("keeps label and icon but swaps the fixed colors for tokens", () => {
    const result = themedPresentation({
      color: "red",
      bgColor: "bg-red-100",
      textColor: "text-red-800",
      icon: "x-circle",
      label: "Unhealthy",
    });
    expect(result.label).toBe("Unhealthy");
    expect(result.icon).toBe("x-circle");
    expect(result.bgColor).toBe("bg-status-unhealthy-bg");
    expect(result.textColor).toBe("text-status-unhealthy");
    expect(result.dotColor).toBe("bg-status-unhealthy");
  });
});

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
