# Entwurf: Neues Theme und Logo

- **Vorhaben:** #477 „Vorhaben: Neues Theme und Logo im Stil von Wiesenwischer Works“
- **Spezifikation:** [`docs/specs/theme-und-logo.md`](../../theme-und-logo.md), ergänzt um Marcus' Entscheidungen vom
  03.10.2026 mit PR #480
- **Figma-Datei:** [ReadyStackGo Design](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1), Key
  `RxVNdSKNs7PpJqkYgvb6a1`, eingetragen in `Wiesenwischer/works`, `products/readystackgo.md` (works#143)
- **Seiten:** Foundations `0:1`, Logo `2:97`, Components `2:98`, App `2:99`, Website `2:100`
- **Freigabe:** freigegeben mit dem Merge von PR #PR_NUMMER

## Rahmen

Bezugsgröße: Desktop 1440 px breit (App 1440 × 900, Website 1440 × 1450). Jeder Rahmen ist an die Variablen gebunden;
das Theme setzt der explizite Variablen-Modus der Sammlung „Theme“ am Rahmen.

| Oberfläche | Zustand | Node-ID | Link | Bild |
|---|---|---|---|---|
| App / Deployments | Leiste ausgeklappt, Türkis hell | `10:2` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=10-2) | `app-deployments-tuerkis-hell.png` |
| App / Deployments | Leiste ausgeklappt, Türkis dunkel | `11:158` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=11-158) | `app-deployments-tuerkis-dunkel.png` |
| App / Deployments | Leiste ausgeklappt, Pastellgrün hell | `11:288` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=11-288) | `app-deployments-pastellgruen-hell.png` |
| App / Deployments | Leiste ausgeklappt, Pastellgrün dunkel | `11:418` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=11-418) | `app-deployments-pastellgruen-dunkel.png` |
| App / Deployments | Leiste eingeklappt, Türkis hell | `11:548` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=11-548) | `app-eingeklappt-tuerkis-hell.png` |
| App / Deployments | Leiste eingeklappt, Türkis dunkel | `11:747` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=11-747) | `app-eingeklappt-tuerkis-dunkel.png` |
| App / Deployments | Leiste eingeklappt, Pastellgrün hell | `11:946` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=11-946) | `app-eingeklappt-pastellgruen-hell.png` |
| App / Deployments | Leiste eingeklappt, Pastellgrün dunkel | `11:1145` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=11-1145) | `app-eingeklappt-pastellgruen-dunkel.png` |
| App / Settings – Appearance | Türkis gewählt, hell | `18:938` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=18-938) | `app-einstellungen-darstellung-tuerkis-hell.png` |
| App / Settings – Appearance | Türkis gewählt, dunkel | `18:1259` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=18-1259) | `app-einstellungen-darstellung-tuerkis-dunkel.png` |
| App / Settings – Appearance | Pastellgrün gewählt, hell | `18:1580` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=18-1580) | `app-einstellungen-darstellung-pastellgruen-hell.png` |
| App / Settings – Appearance | Classic gewählt, dunkel | `18:1901` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=18-1901) | `app-einstellungen-darstellung-classic-dunkel.png` |
| Website / Home | Türkis hell | `12:2` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=12-2) | `website-start-tuerkis-hell.png` |
| Website / Home | Türkis dunkel | `13:813` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=13-813) | `website-start-tuerkis-dunkel.png` |
| Website / Home | Pastellgrün hell (nur Vergleich) | `13:878` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=13-878) | `website-start-pastellgruen-hell.png` |
| Website / Home | Pastellgrün dunkel (nur Vergleich) | `13:943` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=13-943) | `website-start-pastellgruen-dunkel.png` |
| Logo / Proposals (fal.ai) | zwölf Rastervorschläge | `13:1101` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=13-1101) | `logo-vorschlaege-fal.png` |
| Logo / Mark and Lockup | Vektor-Zeichen 16 bis 180 px, Schriftzug, App-Icon | `13:1143` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=13-1143) | `logo-zeichen-und-schriftzug.png` |
| Foundations / Colours | Grundfarben und Theme-Tokens je Modus | `13:1341` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=13-1341) | `farben-tokens.png` |

## Komponenten (Seite Components `2:98`)

| Komponente | Node-ID | Varianten |
|---|---|---|
| Nav Item | `4:52` | State = Default `4:2`, Hover `4:8`, Active `4:14`, Focus `4:21` (Collapsed = No); dieselben eingeklappt: `4:27`, `4:33`, `4:39`, `4:46`. Eigenschaften „Label“ (Text) und „Icon“ (Instanz-Tausch) |
| Button | `5:52` | Variant = Primary, Secondary, Go × State = Default, Hover, Focus, Disabled (`5:28` bis `5:50`). Eigenschaft „Label“ |
| Status Badge | `5:68` | Tone = Healthy `5:53`, Degraded `5:56`, Unhealthy `5:59`, Unknown `5:62`, Progress `5:65` |
| Theme Card | `18:190` | State = Default `18:69`, Hover `18:99`, Selected `18:129`, Focus `18:160`. Eigenschaften „Name“, „Description“ |
| Theme Preview | `18:46` | Miniatur der App, zeigt das Theme, dessen Modus an der Instanz gesetzt ist |
| Segmented Control / Mode | `18:201` | Selected = Light `18:191`, Dark `18:196` |
| Logo/Mark | `6:40` | vereinfachtes Vektor-Zeichen, 48 × 48 |
| Logo/Lockup | `6:67` | Context = Page `6:41`, Nav `6:54` |
| Icons | `3:2` | 18 Strich-Symbole 24 px (`Icon/Dashboard` `3:8` … `Icon/Arrow Right` `3:80`) |

Bilder der Komponenten: `komponente-navigationseintrag.png`, `komponente-button.png`, `komponente-status-badge.png`,
`komponente-theme-karte.png`.

## Tokens und Variablen

Neu angelegt, in der Datei gab es vorher keine Variablen.

- **Sammlung „Primitives“** (ein Modus „Value“, versteckt): Skalen `turquoise/50–950` (500 = `#00CED1`),
  `orange/50–900` (500 = `#FF6B35`), `pastel/50–950`, `neutral/0–950`, `ink/50–950` (blaustichiges Anthrazit des
  dunklen Modus, aus dem Bild der Projektseite), `glass/800–950`, `forest/800–950`, `aqua/25`, `mint/25`, Status
  `green`, `amber`, `red`, `slate`, dazu `classic/brand-*` und `classic/gray-*` mit den heutigen Werten aus
  `apps/rsgo-generic/src/index.css` (Brand 500 = `#465FFF`).
- **Sammlung „Theme“** mit sechs Modi: Turquoise Light, Turquoise Dark, Pastel Green Light, Pastel Green Dark,
  Classic Light, Classic Dark. 38 semantische Tokens, jeweils als Alias auf eine Grundfarbe:
  `bg/page`, `bg/surface`, `bg/raised`, `border/default`, `border/strong`, `text/primary`, `text/secondary`,
  `text/muted`, `text/brand`, `text/on-primary`, `text/on-go`, `primary/default`, `primary/hover`, `primary/subtle`,
  `accent/go`, `accent/go-hover`, `accent/go-text`, `focus/ring`, `nav/bg`, `nav/text`, `nav/text-muted`,
  `nav/hover-bg`, `nav/active-bg`, `nav/active-text`, `nav/active-marker`, `nav/border`, `status/healthy`,
  `status/degraded`, `status/unhealthy`, `status/unknown` (je mit `-bg`), `logo/ready`, `logo/go`, `logo/stack`,
  `logo/stack-on-nav`. Die Werte je Modus zeigt `farben-tokens.png`; die Code-Syntax ist `var(--color-<name>)`.
- Quelle: Spezifikation (Farben aus dem Schriftzug, Pastellgrün, dunkler Modus wie Wiesenwischer Works) und Marcus im
  Chat am 03.10.2026 (pastellige, moderne Richtung; Classic als drittes Theme).

**Kontrast** (WCAG, gerechnet für alle sechs Modi): Text ≥ 4,5 : 1 auf seinen Flächen (`text/*` auf `bg/*`,
`nav/text*` auf `nav/bg`, `nav/hover-bg`, `nav/active-bg`, Text auf `primary/default`, `primary/hover`, `accent/go`,
Status-Text auf seiner Badge-Fläche), Ränder und Fokus ≥ 3 : 1 (`border/strong`, `focus/ring`,
`nav/active-marker`). Kein Wert darunter. Ausnahme bewusst: die Fläche gefüllter Buttons und die Rahmenfarbe der
gewählten Theme-Karte erreichen gegen Weiß in Türkis und Pastellgrün hell nicht 3 : 1; die Bedienelemente sind über
ihre Beschriftung (Text 4,5 : 1 und mehr) bzw. den gefüllten Radio-Punkt erkennbar. Der Schriftzug ist als Logo von
der Kontrastregel ausgenommen.

## Logo

- **Rastervorschläge** (fal.ai, `fal-ai/nano-banana-pro/edit`, Lizenz laut Modellseite „Commercial use“, geprüft am
  03.10.2026, https://fal.ai/models/fal-ai/nano-banana-pro/edit): A1–A4 drei Würfel, B1–B4 Pyramide aus sechs
  Würfeln, je auf dunklem Grund; A3 und B1 zusätzlich auf hellem Grund. Stilvorlage war ein Ausschnitt des Bildes
  `project-readystackgo.webp` (Wiesenwischer/works, Bereich x 1640–2320, y 400–1110 im Original 2880 × 1280).
  4 Anfragen, 12 Bilder, geschätzt 1,80 USD. Die Vorschläge liegen nur als Bildfüllung in Figma und auf der Tafel
  `logo-vorschlaege-fal.png`, nicht als Asset im Repo.
- **Vektor-Zeichen** `Logo/Mark`: drei flache isometrische Würfel, unten links Türkis (`#00CED1`), unten rechts Weiß
  mit grauer Kante, oben Orange (`#FF6B35`); die Seitenflächen nehmen hellere und dunklere Stufen derselben Skala.
  Es ist die vereinfachte Fassung für kleine Größen (Favicon, eingeklappte Leiste) und zeigt bei 16 px noch drei
  Würfel. Der endgültige Vektor-Master entsteht, wenn Marcus ein Zeichen gewählt hat.
- **Schriftzug** `Logo/Lockup`: Zeichen 36 px und „ReadyStackGo“ in Montserrat ExtraBold 21 px, „Ready“ Türkis,
  „Stack“ dunkel auf hellem und weiß auf dunklem Grund, „Go“ Orange. Ohne Rakete.

## Interaktion und Navigation

- **Bildschirm:** Desktop 1440 px. Die Leiste ist ausgeklappt 290 px, eingeklappt 90 px breit (wie heute).
- **Navigationseintrag:** 44 px hoch, Radius 10. Hover färbt die Fläche `nav/hover-bg`, aktiv `nav/active-bg` mit
  einer 4 px breiten orangefarbenen Marke links und halbfettem Text, Fokus zeigt einen 2 px Ring in
  `nav/active-marker`. Eingeklappt nur das Symbol (44 × 44), die Abschnittsüberschriften werden zu „•••“, der Name
  erscheint als Tooltip.
- **Buttons:** 40 px hoch (Seiten-CTAs 48 px), Radius 10. Fokus: 2 px Ring `focus/ring` außen. Deaktiviert:
  45 % Deckkraft. „Go“ (Orange) nur für den Aufruf „Get Started“ auf der Website.
- **Status:** Pille mit Punkt und Text. Healthy/Running grün, Degraded/Partially Running gelb, Unhealthy/Failed rot,
  Unknown/Not Found/Stopped/Removing grau, Deploying/Upgrading in der Markenfarbe (Tone Progress). Orange bedeutet nie
  einen Status.
- **Settings / Appearance:** neue Unterseite unter Settings, dazu eine neue Karte „Appearance“ auf der
  Settings-Übersicht (Beschreibung: „Choose the color theme and light or dark mode“). Abschnitt „Theme“: drei Karten
  als Radio-Gruppe (Turquoise, Pastel Green, Classic), jede mit Miniatur im eigenen Theme und im aktuellen Modus; die
  ganze Karte ist klickbar, Pfeiltasten wechseln die Wahl, die Wahl gilt sofort ohne Speichern-Knopf. Abschnitt
  „Mode“: Umschalter Light | Dark, derselbe Zustand wie der Knopf in der Kopfzeile.
- **Eingabegeräte:** Maus und Tastatur; Tab-Reihenfolge Leiste, Kopfzeile, Inhalt.

## Festlegungen, die die Spezifikation nicht vorgibt

- Neue Figma-Datei „ReadyStackGo Design“ statt einer bestehenden Datei — Marcus im Chat, 03.10.2026.
- Logo-Vorschläge mit fal.ai in dieser Sitzung — Marcus im Chat, 03.10.2026 („Erstelle doch mit fal“).
- Schrift Montserrat (Überschriften, Schriftzug) und Inter (Text), Code in JetBrains Mono — Marcus im Chat,
  03.10.2026 (Montserrat und Inter); JetBrains Mono für Versionen und Namen ist mein Vorschlag.
- Logo ohne Rakete — Marcus im Chat, 03.10.2026.
- Pastellige, moderne Richtung statt kräftig eingefärbter Petrol-Leiste; die erste kräftige Fassung ist verworfen —
  Marcus im Chat, 03.10.2026 („sieht aktuell nicht so modern aus“, „mehr in so eine pastellfarbene Richtung“).
- Theme-Auswahl in der App auf einer Einstellungsseite, mit Türkis, Pastellgrün und dem heutigen Blau als Classic;
  keine Auswahl auf der Website — Marcus im Chat, 03.10.2026.
- Türkis ist das Standard-Theme; die Wahl wird im Browser gespeichert wie heute Hell/Dunkel (`localStorage`) — mein
  Vorschlag, folgt dem Ziel der Spezifikation und dem heutigen Verhalten.
- Texte der neuen Seite (englisch, Lokalisierung kommt vor 1.0): „Appearance“, „Choose the color theme and the mode of
  the web interface.“, „Theme“, „Applies to the whole web interface. Saved in this browser.“, „Turquoise“ / „Default.
  The colors of the ReadyStackGo wordmark.“, „Pastel Green“ / „Soft pastel green with the same orange accent.“,
  „Classic“ / „The previous blue look of ReadyStackGo.“, „Mode“, „Light or dark. The button in the header switches
  it as well.“, „Light“, „Dark“ — mein Vorschlag.
- Beispieldaten der Deployments-Seite aus `stacks/examples` (E2E Platform, Whoami, edge-bundle, backend, frontend,
  whoami); Texte der Website aus `src/ReadyStackGo.PublicWeb/src/i18n/translations.ts`.
- Das Heldenbild der Website ist das Bild der Projektseite (`project-readystackgo.webp`), auf 1232 × 840 zugeschnitten
  — mein Vorschlag.
- Strich-Symbole: eigene einfache Zeichnungen im Stil der heutigen Symbole, keine Übernahme einer Bibliothek.

## Hinweise für die Umsetzung

- Tokens: Die semantischen Tokens kommen als CSS-Variablen in `@theme` von `apps/rsgo-generic/src/index.css`; je
  Theme und Modus ein Satz Werte (z. B. über `data-theme="turquoise|pastel-green|classic"` und die Klasse `.dark` am
  `<html>`). Die Komponenten nutzen nur die semantischen Tokens, nicht `brand-*` direkt.
- `ThemeContext` bekommt neben `light|dark` das Theme; `localStorage` behält den Schlüssel `theme` für den Modus und
  bekommt einen eigenen für das Theme. Ohne Wert gilt Türkis.
- Website und Starlight: nur Türkis; die Akzentfarben in `src/styles/tailwind.css` und `starlight.css` (dort heute nur
  die Schrift) auf die Türkis-Tokens umstellen.
- Logo-Dateien unter `src/ReadyStackGo.Api/wwwroot/images/logo/` und im PublicWeb ersetzen, sobald der Vektor-Master
  freigegeben ist; Favicon aus `Logo/Mark`.
- Schriften Montserrat und Inter lokal ausliefern (heute Outfit über Google Fonts, Noto Sans lokal).

## Offene Fragen

- Welches Logo-Zeichen? Vorschlag: A3 (drei Würfel übereinander, von unten Türkis, Weiß, Orange wie „Ready Stack
  Go“) für das große Zeichen, das flache Vektor-Zeichen für kleine Größen. Danach entsteht der Vektor-Master.
- Bekommt Classic das neue Logo (so im Entwurf) oder das alte? Vorschlag: neues Logo, Classic ändert nur die Farben.
