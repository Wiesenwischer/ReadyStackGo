# Plan: Neues Theme und Logo für ReadyStackGo

Vorhaben #477 · Spezifikation [`docs/specs/theme-und-logo.md`](../specs/theme-und-logo.md) · Entwurf
[`docs/specs/theme-und-logo/entwurf/README.md`](../specs/theme-und-logo/entwurf/README.md) (freigegeben mit dem Merge von
PR #481) · Stand der Planung: `origin/main` 207b981, 04.10.2026

## 1. Kontext

ReadyStackGo sieht heute aus wie die Admin-Vorlage TailAdmin: Blau `#465FFF`, graue Leiste, Schrift Outfit, ein Logo mit
Rakete als PNG. Die Spezifikation will ein eigenes Erscheinungsbild im Stil von Wiesenwischer Works: Farben aus dem
Schriftzug (Türkis `#00CED1`, Weiß, Orange `#FF6B35`), pastellig und modern, hell und dunkel, ein Logo aus Würfeln.
Marcus hat am 03.10.2026 entschieden, dass die App drei Themes zur Wahl anbietet (Türkis als Standard, Pastellgrün und
das bisherige Blau als Classic) und die Website nur Türkis zeigt. Der freigegebene Entwurf legt Tokens, Komponenten,
Seiten und das Logo B1 fest.

## 2. Ergebnis aus Nutzersicht

- Die Weboberfläche erscheint beim ersten Start in Türkis: zart getönte Flächen, eine hell-türkise Navigationsleiste
  mit orangefarbener Marke am aktiven Eintrag, Überschriften in Montserrat, Text in Inter, das neue Würfel-Logo in der
  Leiste (ein- und ausgeklappt) und als Favicon.
- Unter **Settings → Appearance** wählt man das Theme (Turquoise, Pastel Green, Classic) und den Modus (Light, Dark).
  Die Wahl gilt sofort und bleibt im Browser nach einem Neuladen erhalten. Der Knopf in der Kopfzeile schaltet weiter
  zwischen hell und dunkel.
- Classic sieht aus wie heute (Blau), aber mit dem neuen Logo und den neuen Schriften.
- Die Website und die Dokumentation zeigen Türkis, hell und dunkel, mit dem neuen Logo, dem Bild der Projektseite auf
  der Startseite und einem orangefarbenen „Get Started“.

## 3. Umfang und Nicht-Umfang

**Umfang**

- Weboberfläche `src/ReadyStackGo.WebUi` (App `apps/rsgo-generic`, Paket `@rsgo/ui-generic`): Tokens, Leiste,
  Kopfzeile, Buttons, Status-Anzeigen, Diagramme, Logo, Favicon, Schriften, Theme-Auswahl unter Settings.
- Website `src/ReadyStackGo.PublicWeb`: Landing-Seiten, Feature-Seiten, Dokumentation (Starlight), Logo, Favicon,
  Schriften.
- Logo als Vektor-Master (SVG) mit Ableitungen.

**Nicht-Umfang**

- Neue Funktionen außer der Theme-Auswahl; Abläufe bleiben gleich.
- Custom Distributions (bringen ihr eigenes UI-Paket mit), Domain der Website, Lokalisierung der App (vor 1.0).
- Neue Screenshots der Dokumentation (`PublicWeb/public/images/docs/`, 141 Bilder): eigenes Folge-Issue (Entscheidung
  E9).
- Das Logo auf wiesenwischer.de: Issue Wiesenwischer/works#144, startet, wenn der Master auf `main` liegt.

## 4. Entscheidungen, die in diesem Plan stecken

- **E1 – Zwei Ebenen Tokens statt Umschreiben aller Klassen.** Die App nutzt `brand-*` an 909 Stellen in 96 Dateien und
  `dark:` an 4 121 Stellen in `packages/ui-generic/src`. Statt alles umzuschreiben, werden die Skalen `brand-*`
  und `gray-*` im `@theme` zu CSS-Variablen, deren Werte je Theme und Modus gesetzt werden (Türkis: `brand` = Türkis-
  Skala, `gray` = Neutral hell bzw. Ink dunkel; Pastellgrün: `brand` = Pastell, `gray` = Neutral bzw. Forest; Classic:
  die heutigen Werte). Dazu kommen die semantischen Tokens des Entwurfs (`bg/page`, `nav/bg`, `primary/default`,
  `status/healthy` …) als eigene Tailwind-Farben. Die sichtbar prägenden Stellen (Leiste, Kopfzeile, Buttons,
  Status-Pillen, Karten, Seitenhintergrund, Diagramme, Links) werden auf die semantischen Tokens umgestellt; der Rest
  folgt über die umgebogenen Skalen. Grund: deutlich kleinerer und prüfbarer Umbau, gleiches Bild. Die Spezifikation
  sagt seit #480 „Komponenten verwenden nur semantische Tokens“ – für die Bestandsklassen wird das hiermit schrittweise
  erreicht; neue und umgestellte Komponenten nutzen nur semantische Tokens.
- **E2 – Kontrast der umgebogenen Skalen.** Heute steht weißer Text auf `bg-brand-500`. In Türkis wird `brand-500` daher
  auf `turquoise/700` (`#00787B`, 5,29 : 1 mit Weiß) gelegt, nicht auf `#00CED1`; die leuchtenden Töne liegen in
  `brand-300/400`. Gleiches Prinzip in Pastellgrün (`brand-500` = `pastel/700`). Buttons, die auf die semantischen
  Tokens umgestellt werden, zeigen wie im Entwurf `primary/default` mit `text/on-primary`.
- **E3 – Speicherung des Themes.** Neuer Schlüssel `colorTheme` in `localStorage` mit `turquoise`, `pastel-green`,
  `classic`; fehlend oder ungültig → `turquoise`. Der Modus bleibt beim heutigen Schlüssel `theme` (`light`/`dark`,
  `ThemeContext.tsx:21`). Angewendet als `data-theme` am `<html>`, zusätzlich zur Klasse `dark` (`ThemeContext.tsx:35-39`).
  Ein kleines Inline-Skript in `apps/rsgo-generic/index.html` setzt beides vor dem ersten Rendern, damit nichts
  aufblitzt. Nicht serverseitig je Benutzer – so wie der Modus heute.
- **E4 – Schriften lokal.** Inter und Montserrat kommen als npm-Pakete `@fontsource-variable/inter` und
  `@fontsource-variable/montserrat` (SIL OFL) in WebUi und Website; der Google-Fonts-Import von Outfit
  (`index.css:1`) entfällt, ebenso Noto Sans auf der Website (`starlight.css:7-21`, `tailwind.css:4-18`). Grund:
  keine Anfrage an Google, dieselbe Schrift in App und Website. Zwei neue Abhängigkeiten je Projekt.
- **E5 – Code-Schrift.** Der Entwurf zeigt Versionen und Namen in JetBrains Mono. Der Plan nimmt dafür den
  System-Monospace-Stapel (`ui-monospace, SFMono-Regular, Menlo, Consolas, monospace`) statt einer dritten
  Schriftdatei. Abweichung vom Entwurf, nur in der Schriftart der Monospace-Stellen.
- **E6 – Logo als SVG im Code statt PNG.** Neue Komponente `Logo` in `@rsgo/ui-generic` (Zeichen als Inline-SVG, Name
  als Text in Montserrat ExtraBold mit den Tokens `logo/*`). Ersetzt `readystackgo-logo.png`/`readystackgo-icon.png`
  in Leiste und Kopfzeile (`AppSidebar.tsx:335-349`, `AppHeader.tsx:73`). Die Geometrie ist im Abschnitt 6, Schritt 3
  festgelegt, damit sie ohne Figma-Zugriff entsteht. Die alten Dateien `logo.svg`, `logo-dark.svg`, `logo-icon.svg`
  (blau, von keinem Code referenziert) und die PNGs werden durch die neuen Dateien ersetzt.
- **E7 – Favicon.** `favicon.svg` aus dem kleinen Zeichen, dazu `favicon.png` (32 px) und `apple-touch-icon.png`
  (180 px, Zeichen B1 auf `#091012`), gerendert einmalig mit `npx @resvg/resvg-js-cli` (keine neue Abhängigkeit).
  `index.html:5` zeigt heute auf `/vite.svg`, `index.html:7` hat den Titel `readystackgo-webui`; beides wird
  korrigiert (Titel „ReadyStackGo“). `e2e/static-files.spec.ts:99-101` wird angepasst.
- **E8 – Diagramme.** Die Status-Farben in `HealthHistoryChart.tsx:29-39`, `UptimeDonutChart.tsx:16-24` und
  `ServiceHealthTimeline.tsx:16-28` werden zu `var(--color-status-*)`; Recharts gibt `fill`/`stroke` als SVG-Attribut
  aus, dort wirken CSS-Variablen. Wartung bekommt den Token `primary/default` statt `#3b82f6`.
- **E9 – Doku-Screenshots später.** Die 141 Bilder unter `PublicWeb/public/images/docs/` entstehen aus den E2E-Specs mit
  eigenem `SCREENSHOT_DIR` (z. B. `e2e/auth-email-oidc.spec.ts:7`). Sie neu zu erzeugen sprengt den PR. Die Umsetzung
  legt dafür ein Folge-Issue an.
- **E10 – Website-Doku bekommt die Farben wirklich.** Die `--sl-color-*`-Überschreibungen stehen heute in
  `tailwind.css:162-231` (dazu Pagefind-Variablen `:492-498`), das nur `LandingLayout.astro:2` lädt – die
  Dokumentation sieht sie nicht, `starlight.css:1-4` nutzt laut Kommentar die Starlight-Standardfarben. Die
  Überschreibungen ziehen nach `starlight.css` (in `customCss`, `astro.config.mjs:104`) und bekommen die Türkis-Werte.
- **E11 – Heldenbild.** Das Bild `project-readystackgo.webp` aus `Wiesenwischer/works`
  (`website/public/images/visuals/`, eigenes Bild von Wiesenwischer Works) kommt nach
  `PublicWeb/public/images/hero/readystackgo-hero.webp`, zugeschnitten wie im Entwurf (Rahmen `12:2`).
- **E12 – Build-Ausgabe.** `src/ReadyStackGo.Api/wwwroot` ist eingecheckte Build-Ausgabe der WebUi
  (`vite.config.ts:31-34`, `emptyOutDir: true`) und wird von UI-PRs mitgezogen (z. B. #474). Die Umsetzung baut sie neu
  und committet sie.
- **E14 – Neue gemeinsame Komponenten `Button` und `StatusBadge`.** `packages/ui-generic/src/components/ui/` enthält
  heute nur `DeploymentError.tsx` und `TypeSelector.tsx`; Knöpfe und Pillen sind in den Seiten mit Tailwind-Klassen
  geschrieben (z. B. `Deployments.tsx:213`, `:249`). Die Status-Zuordnung steht mehrfach: Produkt-Status in
  `Deployments.tsx:171-189` und `ProductDeploymentDetail.tsx:63-66`, `:86-87`, eigene Logik in
  `Catalog/ProductDetail.tsx:175-182`, Health und Betriebsmodus in `@rsgo/core` (`packages/core/src/api/health.ts:246`,
  `:291`). Der Plan legt `Button` (Primary, Secondary, Go; Default, Hover, Focus, Disabled) und `StatusBadge`
  (Healthy, Degraded, Unhealthy, Unknown, Progress) nach dem Entwurf an und setzt sie auf den Seiten Deployments,
  Product Deployment Detail, Health und Catalog/Product Detail ein. Die Zuordnung Status → Ton steht in einer Funktion
  in `ui-generic`. `@rsgo/core` bleibt unverändert (Labels werden weiter von dort gelesen), damit die private
  Distribution nicht berührt wird. Folge des Entwurfs: „Stopped“ wird grau statt orange (heute orange,
  `Deployments.tsx:171-189`), weil Orange die Markenfarbe ist und zwischen Gelb und Rot schlecht unterscheidbar wäre;
  von Marcus bestätigt im Chat, 04.10.2026.
- **E13 – Texte.** Die Seite Appearance nutzt die Texte aus dem Entwurf (README, „Festlegungen“). Neue Karte auf der
  Settings-Übersicht: Titel „Appearance“, Beschreibung „Choose the color theme and light or dark mode“, Route
  `/settings/appearance`.

## 5. Oberflächen

Verbindlich ist der Entwurf `docs/specs/theme-und-logo/entwurf/README.md` (Figma-Datei „ReadyStackGo Design“, Key
`RxVNdSKNs7PpJqkYgvb6a1`). Bezugsgröße Desktop 1440 px.

| Oberfläche | Zustand | Node-ID | Bild | Was gebaut wird |
|---|---|---|---|---|
| App / Deployments | ausgeklappt, Türkis hell/dunkel, Pastellgrün hell/dunkel | `10:2`, `11:158`, `11:288`, `11:418` | `app-deployments-*.png` | Tokens, Leiste, Kopfzeile, Karten, Buttons, Status-Pillen, Logo |
| App / Deployments | eingeklappt, dieselben vier | `11:548`, `11:747`, `11:946`, `11:1145` | `app-eingeklappt-*.png` | eingeklappte Leiste mit Zeichen B1 (36 px), „•••“ statt Abschnittsnamen (wie heute `HorizontaLDots`) |
| App / Settings – Appearance | Türkis hell/dunkel, Pastellgrün hell, Classic dunkel gewählt | `18:938`, `18:1259`, `18:1580`, `18:1901` | `app-einstellungen-darstellung-*.png` | neue Seite mit Theme-Karten und Umschalter |
| Website / Home | Türkis hell/dunkel (Pastellgrün nur Vergleich) | `12:2`, `13:813` | `website-start-tuerkis-*.png` | Kopfzeile, Hero zweispaltig mit Bild, Feature-Karten |
| Logo | B1 und klein, Schriftzug, App-Icon | `13:1143` | `logo-zeichen-und-schriftzug.png` | SVG-Master, Komponente `Logo`, Favicons |
| Foundations | alle sechs Modi | `13:1341` | `farben-tokens.png` | Werte der Tokens |

Komponenten (Seite Components): Nav Item `4:52` (Default, Hover, Active mit 4-px-Marke, Focus-Ring 2 px; ein-/ausgeklappt),
Button `5:52` (Primary, Secondary, Go × Default, Hover, Focus, Disabled), Status Badge `5:68` (Healthy, Degraded,
Unhealthy, Unknown, Progress), Theme Card `18:190` (Default, Hover, Selected, Focus), Segmented Control `18:201`.
Raster-Icons: keine.

## 6. Schritte der Umsetzung

1. **Tokens WebUi** – `apps/rsgo-generic/src/index.css`:
   - Primitive Skalen aus dem Entwurf (`farben-tokens.png`, Figma `13:1341`) als CSS-Variablen; die heutigen Werte
     `brand` (`:47-58`) und `gray` (`:73-85`) bleiben als Classic-Satz erhalten.
   - Blöcke `:root[data-theme="turquoise"]`, `…[data-theme="turquoise"].dark`, ebenso `pastel-green` und `classic`,
     mit allen 38 semantischen Tokens (`--rsgo-bg-page` …) und den Werten für `--color-brand-*`/`--color-gray-*`
     (E1, E2). `:root` ohne `data-theme` = Türkis.
   - `@theme inline` für die semantischen Farben (`--color-page`, `--color-surface`, `--color-raised`, `--color-line`,
     `--color-line-strong`, `--color-fg`, `--color-fg-secondary`, `--color-fg-muted`, `--color-fg-brand`,
     `--color-primary`, `--color-primary-hover`, `--color-primary-subtle`, `--color-on-primary`, `--color-go`,
     `--color-go-hover`, `--color-on-go`, `--color-focus`, `--color-nav-*`, `--color-status-*`, `--color-logo-*`).
   - `--shadow-focus-ring` (`:153`) und die hart codierten `#465fff` (`:523`, `:529`) auf Tokens.
   - Schriften: `--font-outfit` (`:13`) ersetzen durch `--font-sans: "Inter Variable", …` und
     `--font-display: "Montserrat Variable", …`; `body` (`:192`) `font-sans bg-page`; Überschriften `font-display`.
     `@import` Google Fonts (`:1`) entfernen, Fontsource importieren (E4).
   - Menü-Utilities (`:200-246`) auf `nav/*`-Tokens.
   - `App.css` (Vite-Reste, `:15`, `:18`, `:41`) prüfen und, falls ungenutzt, entfernen.
2. **Theme-Logik** – `packages/ui-generic/src/context/ThemeContext.tsx`: um `colorTheme`, `setColorTheme`,
   `setTheme` erweitern (E3); reine Funktionen `parseColorTheme`/`parseMode` in eigene Datei
   `packages/ui-generic/src/context/themeStorage.ts` (testbar in Vitest, `vitest.config.ts:6-7` nimmt nur `.test.ts`).
   Inline-Skript in `apps/rsgo-generic/index.html`.
3. **Logo** – `packages/ui-generic/src/components/brand/Logo.tsx` mit `variant="mark" | "mark-small" | "lockup"`.
   Geometrie (aus dem Entwurf, Figma `67:153`/`6:40`): isometrischer Würfel mit Kante `e = 10`, Breite
   `w = e·√3`, Ecken T(w/2,0), R(w,e/2), C(w/2,e), L(0,e/2), BL(0,1.5e), B(w/2,2e), BR(w,1.5e); Flächen oben T-R-C-L,
   links L-C-B-BL, rechts C-R-BR-B. Abstand `g = 0,9`. Farben oben/links/rechts: Türkis `#4CDCDF/#00CED1/#00A8AB`,
   Weiß `#FFFFFF/#E2E9EA/#C4CFD1` mit Kante `#A9B6B9` 0,6, Orange `#FF9D77/#FF6B35/#E5521B`.
   B1 (`mark`): Reihen mit `dy = 1,5e + g`, `dx = w + g`: oben Orange bei (dx, 0); Mitte Weiß (dx/2, dy), Türkis
   (1,5dx, dy); unten Türkis (0, 2dy), Weiß (dx, 2dy), Türkis (2dx, 2dy); gezeichnet von oben nach unten.
   Klein (`mark-small`): Türkis (0, 1,5e+g), Weiß (w+g, 1,5e+g), Orange (w/2+g/2, 0).
   Lockup: Zeichen 36 px, Abstand 10, „ReadyStackGo“ Montserrat ExtraBold 21 px, Laufweite −0,3 px, Farben
   `logo-ready`, `logo-stack` (in der Leiste `logo-stack-on-nav`), `logo-go`.
   Dieselben SVGs als Dateien: `apps/rsgo-generic/public/images/logo/readystackgo-mark.svg`, `…-mark-small.svg`,
   `…-lockup-light.svg`, `…-lockup-dark.svg` (Schriftzug als Pfade nicht nötig; Lockup-Dateien mit eingebetteter
   Schriftangabe genügen für Doku und works#144). Dieselben Dateien in `PublicWeb/public/images/logo/`.
   Favicons nach E7.
4. **Leiste und Kopfzeile** – `AppSidebar.tsx`: `<aside>` (`:314-321`, heute `bg-white dark:bg-gray-900`,
   `border-gray-200 dark:border-gray-800`) auf `bg-nav border-nav-line`; Logo-Bereich (`:334-349`) mit
   `Logo variant="lockup"` bzw. `mark` (36 px eingeklappt); aktive Marke 4 px `nav-marker` links;
   Abschnittsüberschriften (`:366-372`, `:387-393`) `text-nav-fg-muted`. `AppHeader.tsx`: Logo (`:71-73`, nur unter
   `lg`), Flächen `bg-surface border-line`. `AppLayout.tsx:12` (heute `bg-gray-100 dark:bg-gray-950`) auf `bg-page`.
5. **Bausteine** – `Button` und `StatusBadge` nach E14 anlegen und auf den genannten Seiten einsetzen; Karten
   (`rounded-2xl`-Container der Seiten) auf `bg-surface border-line`. Status nach dem Entwurf (Healthy/Running grün,
   Degraded/Partially Running gelb, Unhealthy/Failed rot, Unknown/Not Found/Stopped/Removing grau,
   Deploying/Upgrading Markenfarbe; Orange nie Status). Diagramme nach E8.
6. **Appearance** – `packages/ui-generic/src/pages/Settings/Appearance/AppearanceSettingsPage.tsx` + `index.ts`,
   Export in `pages/Settings/index.ts`, Route in `apps/rsgo-generic/src/App.tsx` neben `:377-402`, Eintrag in
   `settingsSections` (`SettingsIndex.tsx:12-145`). Theme-Karten als Radio-Gruppe (`role="radiogroup"`, Pfeiltasten),
   Miniatur je Karte über einen Container mit `data-theme` des jeweiligen Themes; Umschalter Light/Dark über
   `setTheme`.
7. **Website** – `PublicWeb/src/styles/tailwind.css`: `brand-*` (`:57-68`) auf die Türkis-Skala nach E2,
   `gray-*` (`:70-82`) auf Neutral/Ink, Schriften nach E4; `--sl-color-*` (`:162-231`) nach `starlight.css` (E10),
   Werte Türkis hell/dunkel. `Header.astro`: Logo (`:17`) und Schriftzug (`:19`) durch das Lockup-SVG und den Text
   in Montserrat; CTA (`:66`) Orange `go` mit dunklem Text. `Footer.astro` (`:16`, `:18`) ebenso. `Hero.astro`
   zweispaltig mit Bild (E11). Feature-Karten nach Entwurf (Icon-Kachel `primary-subtle`). `astro.config.mjs`:
   `favicon: '/favicon.svg'`, Starlight-`logo` auf das Zeichen. `LandingLayout.astro:18`, `:49-51` anpassen.
8. **Build-Ausgabe** – `pnpm run build` in `src/ReadyStackGo.WebUi`, `wwwroot` committen (E12).
9. **Folge-Issue** „Doku-Screenshots im neuen Theme erneuern“ (E9) anlegen.

## 7. Tests und Abnahme

- **Unit (Vitest)**, neu, ohne die Änderung rot (Datei fehlt):
  - `packages/ui-generic/src/context/themeStorage.test.ts`: fehlender, leerer, ungültiger (`"blue"`, `"Classic"`)
    Wert → `turquoise`; gültige Werte bleiben; Modus `light`/`dark`, Unsinn → `light`.
  - `apps/rsgo-generic/src/theme-contrast.test.ts` (Include in `vitest.config.ts` ergänzen): liest `index.css`,
    löst die Tokens je Theme und Modus auf und prüft die Paare des Entwurfs (Text ≥ 4,5 : 1, Ränder/Fokus ≥ 3 : 1);
    prüft, dass jedes der sechs Theme-Blöcke alle 38 Tokens setzt und dass `#465fff` nur im Classic-Satz vorkommt.
- **E2E** (`e2e/appearance-settings.spec.ts`, gegen den Container): Settings zeigt die Karte Appearance; Klick auf
  Pastel Green setzt `data-theme="pastel-green"` am `<html>`, nach Neuladen weiter; Pfeiltasten wechseln die Auswahl;
  Light/Dark setzt `.dark` und stimmt mit dem Knopf der Kopfzeile überein; ohne gespeicherten Wert `turquoise`;
  ungültiger `localStorage`-Wert → `turquoise`. Bestehende Specs müssen grün bleiben, besonders `static-files.spec.ts`
  (Favicon, angepasst) und `not-found.spec.ts:62-71`.
- **Bilder im Umsetzungs-PR** neben denen des Entwurfs: Deployments ausgeklappt (Türkis hell/dunkel, Pastellgrün
  hell/dunkel), eingeklappt Türkis hell, Appearance (Türkis hell, Classic dunkel), Website-Start hell/dunkel, eine
  Doku-Seite hell/dunkel, Favicon 16/32 px vergrößert.
- **Pflicht-Checks**: „Build & Test“ (`ci.yml:59-77`) und die Browsertests gegen den Container nach `CLAUDE.md`.
- Manuell: `npm run build` der Website (`PublicWeb/package.json`), Startseite, eine Feature-Seite und eine Doku-Seite
  in beiden Modi ansehen.

## 8. Wo umgesetzt wird

GitHub-Runner (`ubuntu-latest`): alles läuft dort, auch die Browsertests gegen den Container. Solange der
Vorhaben-Workflow für ReadyStackGo nicht läuft (#482), baut eine Claude-Code-Sitzung am PC mit
`/vorhaben-umsetzen 477` nach denselben Schritten.

## 9. Risiken

- **Breite des Umbaus:** 909 `brand-*`-Stellen; über die umgebogenen Skalen (E1) bleiben Einzelfälle mit schlechtem
  Kontrast möglich, etwa `text-brand-500` auf `bg-brand-50` in Pastellgrün. Der Kontrasttest prüft nur die
  Token-Paare, nicht jede Klasse; die Bilder der Abnahme decken die Hauptseiten ab, nicht alle 40+ Seiten.
- **`gray-*` je Modus** (E1): Wo eine Komponente `gray-*` im dunklen Modus als Textfarbe nutzt (statt als Fläche),
  kann die Ink-Skala anders wirken als heute. Nicht nachgelesen: alle 4 000 `dark:`-Klassen. Annahme: überwiegend
  Flächen und Ränder.
- **Private Distribution AMS:** nutzt laut `docs/Architecture/Distribution-Architecture.md:13-15` ein eigenes
  Frontend-Paket `@rsgo/ui-ams`. Nicht nachgelesen (privates Repo): ob es Teile von `@rsgo/ui-generic` oder die CSS
  der App übernimmt. Annahme: nein; geändert wird nur `ui-generic` und die App, `@rsgo/core` nicht.
- **Starlight:** Die Farben wirken auf die Doku erst nach E10; Starlight-Updates können Variablennamen ändern.
- **Schriften:** Montserrat ist breiter als Outfit; lange Navigations- und Tabellentexte können umbrechen.
- **Docs-Screenshots** zeigen bis zum Folge-Issue (E9) das alte Blau.

## 10. Offene Fragen

Keine.
