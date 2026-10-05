import { useRef, type KeyboardEvent, type ReactNode } from "react";
import { Link } from "react-router-dom";
import type { ThemeSummary } from "@rsgo/core";
import { useTheme } from "../../../context/ThemeContext";
import type { ModePreference } from "../../../context/themeStorage";

// Settings → Appearance (design: docs/specs/theme-und-logo/entwurf, frames "App / Settings – Appearance",
// components "Theme Orb" and "Mode Switch").

/**
 * Roving focus for a radio group: arrow keys, Home and End select and focus the next option.
 * Returns the key handler and a ref setter per option.
 */
function useRadioKeys(count: number, select: (index: number) => void) {
  const refs = useRef<(HTMLButtonElement | null)[]>([]);
  const onKeyDown = (e: KeyboardEvent<HTMLButtonElement>, index: number) => {
    let next = -1;
    if (e.key === "ArrowRight" || e.key === "ArrowDown") next = (index + 1) % count;
    else if (e.key === "ArrowLeft" || e.key === "ArrowUp") next = (index - 1 + count) % count;
    else if (e.key === "Home") next = 0;
    else if (e.key === "End") next = count - 1;
    if (next < 0) return;
    e.preventDefault();
    select(next);
    refs.current[next]?.focus();
  };
  const setRef = (index: number) => (el: HTMLButtonElement | null) => {
    refs.current[index] = el;
  };
  return { onKeyDown, setRef };
}

/** Color orb: the theme's navigation color in light (left) and dark (right), its brand color in the core. */
function ThemeOrb({
  theme,
  selected,
  onSelect,
  tabIndex,
  onKeyDown,
  buttonRef,
}: {
  theme: ThemeSummary;
  selected: boolean;
  onSelect: () => void;
  tabIndex: number;
  onKeyDown: (e: KeyboardEvent<HTMLButtonElement>) => void;
  buttonRef: (el: HTMLButtonElement | null) => void;
}) {
  return (
    <button
      ref={buttonRef}
      type="button"
      role="radio"
      aria-checked={selected}
      tabIndex={tabIndex}
      onClick={onSelect}
      onKeyDown={onKeyDown}
      data-testid={`theme-orb-${theme.id}`}
      title={theme.description ?? undefined}
      className="group flex min-w-20 flex-col items-center gap-2 px-1 pt-1 outline-none"
    >
      <span className="relative flex h-[72px] w-[72px] items-center justify-center">
        <span
          aria-hidden="true"
          className={`absolute inset-0 rounded-full ring-inset transition-shadow group-focus-visible:ring-[2.5px] group-focus-visible:ring-focus ${
            selected
              ? "shadow-[0_0_18px_-4px_var(--rsgo-primary-default)] ring-[2.5px] ring-primary"
              : "group-hover:ring-[1.5px] group-hover:ring-line-strong"
          }`}
        />
        <span aria-hidden="true" className="relative flex h-16 w-16 overflow-hidden rounded-full">
          <span data-theme={theme.id} data-mode="light" className="h-full w-1/2 bg-nav" />
          <span data-theme={theme.id} data-mode="dark" className="h-full w-1/2 bg-nav" />
          <span
            data-theme={theme.id}
            data-mode="light"
            className="absolute left-1/2 top-1/2 h-[29px] w-[29px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-primary"
          />
          <span className="absolute inset-0 rounded-full border border-line-strong/70" />
        </span>
      </span>
      <span
        className={`whitespace-nowrap text-[13px] leading-[18px] ${selected ? "font-semibold text-fg" : "font-medium text-fg-secondary"}`}
      >
        {theme.name}
      </span>
    </button>
  );
}

const iconProps = {
  width: 14,
  height: 14,
  viewBox: "0 0 16 16",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 1.5,
  strokeLinecap: "round" as const,
  "aria-hidden": true,
};

const MODES: { value: ModePreference; label: string; icon: ReactNode }[] = [
  {
    value: "light",
    label: "Light",
    icon: (
      <svg {...iconProps}>
        <circle cx="8" cy="8" r="3" />
        <path d="M8 1.5v1.5M8 13v1.5M1.5 8H3M13 8h1.5M3.4 3.4l1 1M11.6 11.6l1 1M3.4 12.6l1-1M11.6 4.4l1-1" />
      </svg>
    ),
  },
  {
    value: "dark",
    label: "Dark",
    icon: (
      <svg {...iconProps}>
        <path d="M13.5 9.5A5.5 5.5 0 0 1 6.5 2.5a5.5 5.5 0 1 0 7 7Z" />
      </svg>
    ),
  },
  {
    value: "system",
    label: "System",
    icon: (
      <svg {...iconProps}>
        <circle cx="8" cy="8" r="5.75" />
        <path d="M8 2.25a5.75 5.75 0 0 1 0 11.5Z" fill="currentColor" stroke="none" />
      </svg>
    ),
  },
];

export default function AppearanceSettingsPage() {
  const { modePreference, setModePreference, colorTheme, setColorTheme, availableThemes, themesLoaded } =
    useTheme();

  const selectedThemeIndex = Math.max(
    0,
    availableThemes.findIndex((t) => t.id === colorTheme),
  );
  const themeKeys = useRadioKeys(availableThemes.length, (i) => setColorTheme(availableThemes[i].id));
  const modeKeys = useRadioKeys(MODES.length, (i) => setModePreference(MODES[i].value));

  return (
    <div className="mx-auto max-w-screen-2xl p-4 md:p-6 2xl:p-10">
      <nav aria-label="Breadcrumb" className="mb-4 flex items-center gap-1.5 text-[13px]">
        <Link to="/settings" className="font-medium text-fg-brand hover:underline">
          Settings
        </Link>
        <span className="text-fg-muted">/</span>
        <span className="text-fg-muted">Appearance</span>
      </nav>
      <div className="mb-6">
        <h2 className="text-[26px] font-bold leading-[34px] text-fg">Appearance</h2>
        <p className="mt-1 text-sm text-fg-secondary">Choose the color theme and the mode of the web interface.</p>
      </div>

      <div className="flex flex-col gap-6">
        {themesLoaded && availableThemes.length > 1 && (
          <section className="rounded-2xl border border-line bg-surface px-6 pb-6 pt-5" aria-labelledby="appearance-theme">
            <h3 id="appearance-theme" className="text-base font-semibold text-fg">
              Theme
            </h3>
            <p className="mt-1 text-[13px] text-fg-muted">The color theme of the web interface. Saved in this browser.</p>
            <div role="radiogroup" aria-labelledby="appearance-theme" className="mt-4 flex flex-wrap gap-6">
              {availableThemes.map((t, i) => (
                <ThemeOrb
                  key={t.id}
                  theme={t}
                  selected={t.id === colorTheme}
                  onSelect={() => setColorTheme(t.id)}
                  tabIndex={i === selectedThemeIndex ? 0 : -1}
                  onKeyDown={(e) => themeKeys.onKeyDown(e, i)}
                  buttonRef={themeKeys.setRef(i)}
                />
              ))}
            </div>
          </section>
        )}

        <section className="rounded-2xl border border-line bg-surface px-6 pb-6 pt-5" aria-labelledby="appearance-mode">
          <h3 id="appearance-mode" className="text-base font-semibold text-fg">
            Mode
          </h3>
          <p className="mt-1 text-[13px] text-fg-muted">
            Light or dark, or follow the operating system. The button in the header switches light and dark as well.
          </p>
          <div
            role="radiogroup"
            aria-labelledby="appearance-mode"
            className="mt-4 inline-flex gap-0.5 rounded-[10px] border border-line bg-page p-[3px]"
          >
            {MODES.map((m, i) => {
              const active = m.value === modePreference;
              return (
                <button
                  key={m.value}
                  ref={modeKeys.setRef(i)}
                  type="button"
                  role="radio"
                  aria-checked={active}
                  tabIndex={active ? 0 : -1}
                  onClick={() => setModePreference(m.value)}
                  onKeyDown={(e) => modeKeys.onKeyDown(e, i)}
                  data-testid={`mode-${m.value}`}
                  className={`flex items-center gap-2 rounded-[7px] border py-1.5 pl-3 pr-3.5 text-[13px] leading-5 transition-colors focus-visible:border-focus focus-visible:outline-1 focus-visible:outline-focus ${
                    active
                      ? "border-line-strong bg-raised font-semibold text-fg shadow-theme-xs"
                      : "border-transparent font-medium text-fg-secondary hover:bg-raised hover:text-fg"
                  }`}
                >
                  <span className={active ? "text-fg-brand" : "text-fg-muted"}>{m.icon}</span>
                  {m.label}
                </button>
              );
            })}
          </div>
        </section>
      </div>
    </div>
  );
}
