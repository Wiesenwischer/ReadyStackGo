// Checks the built-in theme packages (public/themes/<id>/) against the package format
// (docs/Architecture/Themes.md) and the contrast rules of the specification (WCAG AA).
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const appDir = join(__dirname, "..");
const themesDir = join(appDir, "public", "themes");

const SEMANTIC_TOKENS = [
  "bg-page", "bg-surface", "bg-raised", "border-default", "border-strong",
  "text-primary", "text-secondary", "text-muted", "text-brand", "text-on-primary", "text-on-go",
  "primary-default", "primary-hover", "primary-subtle",
  "accent-go", "accent-go-hover", "accent-go-text", "focus-ring",
  "nav-bg", "nav-text", "nav-text-muted", "nav-hover-bg", "nav-active-bg", "nav-active-text",
  "nav-active-marker", "nav-border",
  "status-healthy", "status-healthy-bg", "status-degraded", "status-degraded-bg",
  "status-unhealthy", "status-unhealthy-bg", "status-unknown", "status-unknown-bg",
  "logo-ready", "logo-go", "logo-stack", "logo-stack-on-nav",
];
const SCALE_STEPS = ["25", "50", "100", "200", "300", "400", "500", "600", "700", "800", "900", "950"];
const REQUIRED = [
  ...SEMANTIC_TOKENS,
  ...SCALE_STEPS.map((s) => `brand-${s}`),
  ...SCALE_STEPS.map((s) => `gray-${s}`),
  "gray-dark",
];

// [foreground, background, minimum ratio]
const CONTRAST_PAIRS: [string, string, number][] = [
  ["text-primary", "bg-page", 4.5],
  ["text-primary", "bg-surface", 4.5],
  ["text-secondary", "bg-surface", 4.5],
  ["text-secondary", "bg-raised", 4.5],
  ["text-muted", "bg-surface", 4.5],
  ["text-muted", "bg-raised", 4.5],
  ["text-brand", "bg-surface", 4.5],
  ["text-brand", "primary-subtle", 4.5],
  ["text-on-primary", "primary-default", 4.5],
  ["text-on-primary", "primary-hover", 4.5],
  ["text-on-go", "accent-go", 4.5],
  ["text-on-go", "accent-go-hover", 4.5],
  ["accent-go-text", "bg-surface", 4.5],
  ["nav-text", "nav-bg", 4.5],
  ["nav-text-muted", "nav-bg", 4.5],
  ["nav-text", "nav-hover-bg", 4.5],
  ["nav-active-text", "nav-active-bg", 4.5],
  ["status-healthy", "status-healthy-bg", 4.5],
  ["status-degraded", "status-degraded-bg", 4.5],
  ["status-unhealthy", "status-unhealthy-bg", 4.5],
  ["status-unknown", "status-unknown-bg", 4.5],
  ["status-healthy", "bg-surface", 4.5],
  ["status-unhealthy", "bg-surface", 4.5],
  ["border-strong", "bg-surface", 3],
  ["focus-ring", "bg-surface", 3],
  ["focus-ring", "bg-page", 3],
  ["nav-active-marker", "nav-bg", 3],
];

type Block = Map<string, string>;

function parseBlock(raw: string, selector: string): Block {
  const css = raw.replace(/\r\n/g, "\n");
  const start = css.indexOf(selector);
  if (start < 0) throw new Error(`selector not found: ${selector}`);
  const open = css.indexOf("{", start);
  const close = css.indexOf("}", open);
  const body = css.slice(open + 1, close);
  const vars = new Map<string, string>();
  for (const m of body.matchAll(/--rsgo-([a-z0-9-]+)\s*:\s*([^;]+);/g)) {
    vars.set(m[1], m[2].trim());
  }
  return vars;
}

function luminance(hex: string): number {
  const h = hex.replace("#", "");
  const [r, g, b] = [0, 2, 4].map((i) => {
    const c = parseInt(h.slice(i, i + 2), 16) / 255;
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(a: string, b: string): number {
  const [la, lb] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (la + 0.05) / (lb + 0.05);
}

const packages = readdirSync(themesDir, { withFileTypes: true })
  .filter((d) => d.isDirectory())
  .map((d) => d.name)
  .sort();

describe("built-in theme packages", () => {
  it("ships turquoise, pastel-green and classic", () => {
    expect(packages).toEqual(["classic", "pastel-green", "turquoise"]);
  });

  describe.each(packages)("%s", (id) => {
    const meta = JSON.parse(readFileSync(join(themesDir, id, "theme.json"), "utf8"));
    const css = readFileSync(join(themesDir, id, "theme.css"), "utf8").replace(/\r\n/g, "\n");
    const light = parseBlock(css, `[data-theme="${id}"] {`);
    const dark = parseBlock(css, `[data-theme="${id}"][data-mode="dark"] {`);

    it("has an id matching the folder and the id rule", () => {
      expect(meta.id).toBe(id);
      expect(id).toMatch(/^[a-z0-9][a-z0-9-]{0,39}$/);
      expect(typeof meta.name).toBe("string");
      expect(meta.name.length).toBeGreaterThan(0);
      expect(typeof meta.order).toBe("number");
    });

    it("has exactly the light and the dark block, the dark one keyed on data-mode", () => {
      // The dark block must not depend on an ancestor (.dark), so a preview element can force either mode.
      const selectors = [...css.matchAll(/^([^\s/*{}][^{\n]*)\{/gm)].map((m) => m[1].trim());
      expect(selectors).toEqual([`[data-theme="${id}"]`, `[data-theme="${id}"][data-mode="dark"]`]);
    });

    it("only sets --rsgo-* custom properties", () => {
      const declarations = [...css.matchAll(/^\s*([a-z-]+)\s*:/gm)].map((m) => m[1]);
      expect(declarations.filter((d) => !d.startsWith("--rsgo-"))).toEqual([]);
    });

    it.each(["light", "dark"] as const)("sets every token in %s mode", (mode) => {
      const block = mode === "light" ? light : dark;
      const missing = REQUIRED.filter((t) => !block.has(t));
      expect(missing).toEqual([]);
      for (const value of block.values()) expect(value).toMatch(/^#[0-9A-F]{6}$/i);
    });

    it.each(["light", "dark"] as const)("meets WCAG AA in %s mode", (mode) => {
      const block = mode === "light" ? light : dark;
      const failures = CONTRAST_PAIRS.map(([fg, bg, min]) => ({
        pair: `${fg} on ${bg}`,
        ratio: contrast(block.get(fg)!, block.get(bg)!),
        min,
      })).filter((r) => r.ratio < r.min);
      expect(failures).toEqual([]);
    });

    it("keeps the logo colors fixed", () => {
      for (const block of [light, dark]) {
        expect(block.get("logo-ready")?.toUpperCase()).toBe("#00CED1");
        expect(block.get("logo-go")?.toUpperCase()).toBe("#FF6B35");
      }
    });
  });

  it("uses the old blue #465FFF only in classic", () => {
    for (const id of packages) {
      const css = readFileSync(join(themesDir, id, "theme.css"), "utf8").toUpperCase();
      expect(css.includes("#465FFF")).toBe(id === "classic");
    }
  });

  it("keeps the built-in fallback in sync with the turquoise package", () => {
    const pkg = readFileSync(join(themesDir, "turquoise", "theme.css"), "utf8");
    const fallback = readFileSync(join(appDir, "src", "theme-fallback.css"), "utf8");
    expect(parseBlock(fallback, ":where(:root) {")).toEqual(parseBlock(pkg, '[data-theme="turquoise"] {'));
    expect(parseBlock(fallback, ':where(:root[data-mode="dark"]) {')).toEqual(
      parseBlock(pkg, '[data-theme="turquoise"][data-mode="dark"] {'),
    );
  });
});
