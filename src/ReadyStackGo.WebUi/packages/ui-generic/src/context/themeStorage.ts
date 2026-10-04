// Pure helpers for theme persistence. Keep in sync with the inline script in
// apps/rsgo-generic/index.html, which applies the stored theme before the first render.

export type Mode = "light" | "dark";

export const MODE_STORAGE_KEY = "theme";
export const COLOR_THEME_STORAGE_KEY = "colorTheme";
export const COLOR_THEME_CSS_STORAGE_KEY = "colorThemeCss";

/** Theme ids as accepted by the server (docs/Architecture/Themes.md). */
const THEME_ID_PATTERN = /^[a-z0-9][a-z0-9-]{0,39}$/;

export function isValidThemeId(value: string | null | undefined): value is string {
  return typeof value === "string" && THEME_ID_PATTERN.test(value);
}

export function parseMode(value: string | null | undefined): Mode {
  return value === "dark" ? "dark" : "light";
}

/**
 * Picks the theme to show: the stored one if the installation still offers it,
 * otherwise the installation's default, otherwise the first offered theme.
 * Returns null when nothing is offered.
 */
export function resolveColorTheme(
  stored: string | null | undefined,
  offeredIds: readonly string[],
  defaultId: string | null | undefined,
): string | null {
  if (isValidThemeId(stored) && offeredIds.includes(stored)) {
    return stored;
  }
  if (isValidThemeId(defaultId) && offeredIds.includes(defaultId)) {
    return defaultId;
  }
  return offeredIds.length > 0 ? offeredIds[0] : null;
}
