import { useRef, type KeyboardEvent } from "react";
import { Link } from "react-router-dom";
import type { ThemeSummary } from "@rsgo/core";
import { useTheme } from "../../../context/ThemeContext";
import type { Mode } from "../../../context/themeStorage";

// Settings → Appearance (design: docs/specs/theme-und-logo/entwurf, frames "App / Settings – Appearance").

/** Miniature of the app, rendered in the theme given by data-theme on its container. */
function ThemePreview({ themeId }: { themeId: string }) {
  return (
    <div
      data-theme={themeId}
      aria-hidden="true"
      className="flex h-[148px] overflow-hidden rounded-[10px] border border-line bg-page"
    >
      <div className="flex w-16 flex-col gap-2 bg-nav px-3 pt-3.5">
        <div className="mb-2 flex items-center gap-1">
          <span className="h-2 w-8 rounded bg-logo-ready" />
          <span className="h-1.5 w-1.5 rounded-full bg-go" />
        </div>
        {[0, 1, 2, 3, 4].map((i) => (
          <span key={i} className={`relative h-2 w-10 rounded ${i === 1 ? "bg-nav-active" : "bg-nav-hover"}`}>
            {i === 1 && <span className="absolute -left-1 top-0 h-2 w-[3px] rounded bg-nav-marker" />}
          </span>
        ))}
      </div>
      <div className="flex flex-1 flex-col">
        <div className="h-[22px] bg-surface" />
        <div className="flex items-center justify-between px-3.5 pt-3">
          <span className="h-2 w-16 rounded bg-fg" />
          <span className="h-3.5 w-11 rounded bg-primary" />
        </div>
        <div className="mx-3.5 mt-2.5 flex flex-1 flex-col justify-center gap-3 rounded-md border border-line bg-surface px-3 mb-2.5">
          {["bg-status-healthy", "bg-status-degraded", "bg-status-unhealthy"].map((dot) => (
            <div key={dot} className="flex items-center gap-2">
              <span className="h-1.5 w-14 rounded bg-fg-secondary" />
              <span className={`h-2 w-2 rounded-full ${dot}`} />
              <span className="h-1.5 w-7 rounded bg-fg-muted" />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function ThemeCard({
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
      data-testid={`theme-card-${theme.id}`}
      className={`flex w-full max-w-[284px] flex-col gap-3 rounded-2xl p-3 pb-3.5 text-left transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus ${
        selected
          ? "border-2 border-primary bg-surface"
          : "border border-line bg-surface hover:border-line-strong hover:bg-raised"
      }`}
    >
      <ThemePreview themeId={theme.id} />
      <span className="flex gap-2.5 px-1">
        <span
          aria-hidden="true"
          className={`mt-0.5 flex h-[18px] w-[18px] flex-shrink-0 items-center justify-center rounded-full ${
            selected ? "bg-primary" : "border-[1.5px] border-line-strong bg-surface"
          }`}
        >
          {selected && <span className="h-1.5 w-1.5 rounded-full bg-on-primary" />}
        </span>
        <span className="flex flex-col gap-0.5">
          <span className="text-[15px] font-semibold leading-5 text-fg">{theme.name}</span>
          {theme.description && (
            <span className="text-[13px] leading-[18px] text-fg-secondary">{theme.description}</span>
          )}
        </span>
      </span>
    </button>
  );
}

const MODES: { value: Mode; label: string }[] = [
  { value: "light", label: "Light" },
  { value: "dark", label: "Dark" },
];

export default function AppearanceSettingsPage() {
  const { theme: mode, setTheme, colorTheme, setColorTheme, availableThemes, themesLoaded } = useTheme();
  const cardRefs = useRef<(HTMLButtonElement | null)[]>([]);

  const selectedIndex = Math.max(
    0,
    availableThemes.findIndex((t) => t.id === colorTheme),
  );

  const moveSelection = (e: KeyboardEvent<HTMLButtonElement>, index: number) => {
    const count = availableThemes.length;
    let next = -1;
    if (e.key === "ArrowRight" || e.key === "ArrowDown") next = (index + 1) % count;
    else if (e.key === "ArrowLeft" || e.key === "ArrowUp") next = (index - 1 + count) % count;
    else if (e.key === "Home") next = 0;
    else if (e.key === "End") next = count - 1;
    if (next < 0) return;
    e.preventDefault();
    setColorTheme(availableThemes[next].id);
    cardRefs.current[next]?.focus();
  };

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
            <p className="mt-1 text-[13px] text-fg-muted">Applies to the whole web interface. Saved in this browser.</p>
            <div role="radiogroup" aria-labelledby="appearance-theme" className="mt-4 flex flex-wrap gap-4">
              {availableThemes.map((t, i) => (
                <ThemeCard
                  key={t.id}
                  theme={t}
                  selected={t.id === colorTheme}
                  onSelect={() => setColorTheme(t.id)}
                  tabIndex={i === selectedIndex ? 0 : -1}
                  onKeyDown={(e) => moveSelection(e, i)}
                  buttonRef={(el) => {
                    cardRefs.current[i] = el;
                  }}
                />
              ))}
            </div>
          </section>
        )}

        <section className="rounded-2xl border border-line bg-surface px-6 pb-6 pt-5" aria-labelledby="appearance-mode">
          <h3 id="appearance-mode" className="text-base font-semibold text-fg">
            Mode
          </h3>
          <p className="mt-1 text-[13px] text-fg-muted">Light or dark. The button in the header switches it as well.</p>
          <div
            role="radiogroup"
            aria-labelledby="appearance-mode"
            className="mt-4 inline-flex gap-1 rounded-xl border border-line bg-raised p-1"
          >
            {MODES.map((m) => {
              const active = m.value === mode;
              return (
                <button
                  key={m.value}
                  type="button"
                  role="radio"
                  aria-checked={active}
                  onClick={() => setTheme(m.value)}
                  data-testid={`mode-${m.value}`}
                  className={`rounded-lg px-4 py-2 text-sm leading-5 transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus ${
                    active
                      ? "border border-line bg-surface font-semibold text-fg"
                      : "border border-transparent font-medium text-fg-secondary hover:text-fg"
                  }`}
                >
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
