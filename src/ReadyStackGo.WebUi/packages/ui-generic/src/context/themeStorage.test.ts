import { describe, expect, it } from "vitest";
import { isValidThemeId, parseMode, resolveColorTheme } from "./themeStorage";

const offered = ["turquoise", "pastel-green", "classic"];

describe("parseMode", () => {
  it("keeps light and dark", () => {
    expect(parseMode("light")).toBe("light");
    expect(parseMode("dark")).toBe("dark");
  });

  it.each([null, undefined, "", "Dark", "system", "auto"])("falls back to light for %s", (value) => {
    expect(parseMode(value as string | null | undefined)).toBe("light");
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
