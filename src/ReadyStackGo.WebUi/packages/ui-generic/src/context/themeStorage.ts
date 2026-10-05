// Pure helpers for theme persistence. Keep in sync with the inline script in
// apps/rsgo-generic/index.html, which applies the stored theme before the first render.

/** The mode that is shown. */
export type Mode = "light" | "dark";
/** The mode the user chose; "system" follows prefers-color-scheme. */
export type ModePreference = Mode | "system";

export const MODE_STORAGE_KEY = "theme";
export const COLOR_THEME_STORAGE_KEY = "colorTheme";
export const COLOR_THEME_CSS_STORAGE_KEY = "colorThemeCss";

/** Theme ids as accepted by the server (docs/Architecture/Themes.md). */
const THEME_ID_PATTERN = /^[a-z0-9][a-z0-9-]{0,39}$/;

export function isValidThemeId(value: string | null | undefined): value is string {
  return typeof value === "string" && THEME_ID_PATTERN.test(value);
}

/** Reads a stored preference. Unknown or missing values follow the operating system. */
export function parseModePreference(value: string | null | undefined): ModePreference {
  return value === "light" || value === "dark" || value === "system" ? value : "system";
}

export function resolveMode(preference: ModePreference, systemPrefersDark: boolean): Mode {
  if (preference === "system") return systemPrefersDark ? "dark" : "light";
  return preference;
}

/** The header button always switches to the opposite of what is shown, as an explicit choice. */
export function toggledPreference(shown: Mode): ModePreference {
  return shown === "dark" ? "light" : "dark";
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
