"use client";

import type React from "react";
import { createContext, useCallback, useContext, useEffect, useState } from "react";
import { listThemes, type ThemeSummary } from "@rsgo/core";
import {
  COLOR_THEME_CSS_STORAGE_KEY,
  COLOR_THEME_STORAGE_KEY,
  MODE_STORAGE_KEY,
  isValidThemeId,
  parseModePreference,
  resolveColorTheme,
  resolveMode,
  toggledPreference,
  type Mode,
  type ModePreference,
} from "./themeStorage";

type ThemeContextType = {
  /** The mode that is shown, light or dark. */
  theme: Mode;
  /** Switches what is shown to the opposite mode as an explicit choice. */
  toggleTheme: () => void;
  /** The mode the user chose: light, dark or system. */
  modePreference: ModePreference;
  setModePreference: (preference: ModePreference) => void;
  /** Id of the active theme package, or null until known. */
  colorTheme: string | null;
  setColorTheme: (id: string) => void;
  /** Theme packages offered by this installation. */
  availableThemes: ThemeSummary[];
  themesLoaded: boolean;
};

const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

const THEME_LINK_ATTRIBUTE = "data-rsgo-theme";
const DARK_QUERY = "(prefers-color-scheme: dark)";

function systemPrefersDark(): boolean {
  return typeof window !== "undefined" && typeof window.matchMedia === "function"
    ? window.matchMedia(DARK_QUERY).matches
    : false;
}

function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStorage(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Storage may be unavailable (private mode); the theme still applies for this page.
  }
}

/** Adds one stylesheet link per offered theme package (all are needed for the previews). */
function ensureThemeStylesheets(themes: ThemeSummary[]): void {
  for (const theme of themes) {
    const existing = document.head.querySelector(`link[${THEME_LINK_ATTRIBUTE}="${theme.id}"]`);
    if (existing) continue;
    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = theme.cssUrl;
    link.setAttribute(THEME_LINK_ATTRIBUTE, theme.id);
    document.head.appendChild(link);
  }
}

export const ThemeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [modePreference, setModePreferenceState] = useState<ModePreference>(() =>
    typeof window !== "undefined" ? parseModePreference(readStorage(MODE_STORAGE_KEY)) : "system",
  );
  const [systemDark, setSystemDark] = useState<boolean>(systemPrefersDark);
  const theme = resolveMode(modePreference, systemDark);
  const [colorTheme, setColorThemeState] = useState<string | null>(() => {
    if (typeof window === "undefined") return null;
    const stored = readStorage(COLOR_THEME_STORAGE_KEY);
    return isValidThemeId(stored) ? stored : null;
  });
  const [availableThemes, setAvailableThemes] = useState<ThemeSummary[]>([]);
  const [themesLoaded, setThemesLoaded] = useState(false);

  useEffect(() => {
    if (typeof window.matchMedia !== "function") return;
    const query = window.matchMedia(DARK_QUERY);
    const onChange = (event: MediaQueryListEvent) => setSystemDark(event.matches);
    query.addEventListener("change", onChange);
    return () => query.removeEventListener("change", onChange);
  }, []);

  useEffect(() => {
    writeStorage(MODE_STORAGE_KEY, modePreference);
  }, [modePreference]);

  useEffect(() => {
    const root = document.documentElement;
    root.classList.toggle("dark", theme === "dark");
    root.setAttribute("data-mode", theme);
  }, [theme]);

  useEffect(() => {
    let cancelled = false;
    listThemes()
      .then((response) => {
        if (cancelled) return;
        setAvailableThemes(response.themes);
        ensureThemeStylesheets(response.themes);
        const resolved = resolveColorTheme(
          readStorage(COLOR_THEME_STORAGE_KEY),
          response.themes.map((t) => t.id),
          response.default,
        );
        setColorThemeState(resolved);
      })
      .catch(() => {
        // Without the theme list the stored theme (applied by index.html) or the built-in fallback stays.
      })
      .finally(() => {
        if (!cancelled) setThemesLoaded(true);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    const root = document.documentElement;
    if (!colorTheme) {
      root.removeAttribute("data-theme");
      return;
    }
    root.setAttribute("data-theme", colorTheme);
    writeStorage(COLOR_THEME_STORAGE_KEY, colorTheme);
    const summary = availableThemes.find((t) => t.id === colorTheme);
    if (summary) writeStorage(COLOR_THEME_CSS_STORAGE_KEY, summary.cssUrl);
  }, [colorTheme, availableThemes]);

  const toggleTheme = useCallback(() => {
    setModePreferenceState(toggledPreference(theme));
  }, [theme]);

  const setModePreference = useCallback(
    (preference: ModePreference) => setModePreferenceState(preference),
    [],
  );

  const setColorTheme = useCallback(
    (id: string) => {
      if (availableThemes.some((t) => t.id === id)) setColorThemeState(id);
    },
    [availableThemes],
  );

  return (
    <ThemeContext.Provider
      value={{
        theme,
        toggleTheme,
        modePreference,
        setModePreference,
        colorTheme,
        setColorTheme,
        availableThemes,
        themesLoaded,
      }}
    >
      {children}
    </ThemeContext.Provider>
  );
};

export const useTheme = () => {
  const context = useContext(ThemeContext);
  if (context === undefined) {
    throw new Error("useTheme must be used within a ThemeProvider");
  }
  return context;
};
