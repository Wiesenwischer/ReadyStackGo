import { describe, expect, it } from "vitest";
import {
  isValidThemeId,
  parseModePreference,
  resolveColorTheme,
  resolveMode,
  toggledPreference,
} from "./themeStorage";

const offered = ["turquoise", "pastel-green", "classic"];

describe("parseModePreference", () => {
  it.each(["light", "dark", "system"])("keeps %s", (value) => {
    expect(parseModePreference(value)).toBe(value);
  });

  it.each([null, undefined, "", "Dark", "SYSTEM", "auto", "1"])("follows the system for %s", (value) => {
    expect(parseModePreference(value as string | null | undefined)).toBe("system");
  });
});

describe("resolveMode", () => {
  it.each([
    ["light", false, "light"],
    ["light", true, "light"],
    ["dark", false, "dark"],
    ["dark", true, "dark"],
    ["system", false, "light"],
    ["system", true, "dark"],
  ] as const)("shows %s with system dark=%s as %s", (preference, systemDark, shown) => {
    expect(resolveMode(preference, systemDark)).toBe(shown);
  });
});

describe("toggledPreference", () => {
  it("switches to the opposite of what is shown, never to system", () => {
    expect(toggledPreference("light")).toBe("dark");
    expect(toggledPreference("dark")).toBe("light");
  });
});

describe("isValidThemeId", () => {
  it.each(["turquoise", "pastel-green", "a", "x1-2"])("accepts %s", (value) => {
    expect(isValidThemeId(value)).toBe(true);
  });

  it.each([null, undefined, "", "Classic", "-dash", "../etc", "a b", "a".repeat(41)])("rejects %s", (value) => {
    expect(isValidThemeId(value as string | null | undefined)).toBe(false);
  });
});

describe("resolveColorTheme", () => {
  it("keeps a stored theme that is still offered", () => {
    expect(resolveColorTheme("classic", offered, "turquoise")).toBe("classic");
  });

  it.each([null, undefined, "", "blue", "Classic"])("uses the default for stored value %s", (stored) => {
    expect(resolveColorTheme(stored as string | null | undefined, offered, "turquoise")).toBe("turquoise");
  });

  it("uses the default when the stored theme is no longer offered", () => {
    expect(resolveColorTheme("pastel-green", ["classic"], "classic")).toBe("classic");
  });

  it("uses the first offered theme when the default is not offered", () => {
    expect(resolveColorTheme(null, ["classic", "turquoise"], "business")).toBe("classic");
  });

  it("uses the first offered theme when the default is invalid", () => {
    expect(resolveColorTheme(null, ["classic"], "../x")).toBe("classic");
  });

  it("returns null when nothing is offered", () => {
    expect(resolveColorTheme("turquoise", [], "turquoise")).toBeNull();
  });
});
