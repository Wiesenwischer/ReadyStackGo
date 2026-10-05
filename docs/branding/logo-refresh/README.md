# Logo: drei Würfel und Wortmarke

Das Logo von ReadyStackGo, ausgewählt von Marcus am 05.10.2026. Es löst die Pyramide aus sechs Würfeln (Zeichen B1 aus
`docs/specs/theme-und-logo/entwurf/`) ab.

- **Auftrag:** [`ReadyStackGo_Logo_Umstellung_Claude.md`](ReadyStackGo_Logo_Umstellung_Claude.md)
- **Referenz:** [`referenz/ReadyStackGo_Neues_Logo.jpeg`](referenz/ReadyStackGo_Neues_Logo.jpeg), unverändert. Der
  dunkle Hintergrund und die Ränder gehören nicht zum Logo.
- **Masterquelle:** [`build_logo.py`](build_logo.py). Alle Logo-Dateien entstehen daraus, nichts wird von Hand
  nachgezeichnet. Die PNGs rendert [`render_png.mjs`](render_png.mjs) mit Chromium aus den erzeugten SVGs.

```bash
python docs/branding/logo-refresh/build_logo.py   # SVGs und logoArtwork.ts, braucht fonttools und brotli
node docs/branding/logo-refresh/render_png.mjs     # favicon.png, apple-touch-icon.png
```

Danach `pnpm run build` in `src/ReadyStackGo.WebUi`, damit `src/ReadyStackGo.Api/wwwroot` die Dateien bekommt.

## Zeichen

Drei gleich große isometrische Würfel: oben Orange, unten links Türkis, unten rechts Weiß. Die Maße stammen aus der
Referenz, in Würfelkanten E:

| Größe | Wert |
|---|---|
| Breite eines Würfels | √3 · E |
| Höhe eines Würfels | 2 · E |
| Abstand der Spitzen, oberer zu unteren Würfeln | 4/3 · E |
| Fuge zwischen den Würfeln | 0,071 · E (transparent) |
| Fuge der kleinen Fassung (16 bis 32 px, Favicon) | 0,16 · E |

Die unteren Würfel liegen vor dem oberen. Die Fuge schneidet eine Maske aus dem orangefarbenen Würfel. Es gibt keine
Konturen, Schatten oder Glanzeffekte.

| Würfel | Oben | Links | Rechts |
|---|---|---|---|
| Orange | `#FF7B43` | `#FF6B35` (Marke) | `#D84B16` |
| Türkis | `#0BDCDD` | `#00CED1` (Marke) | `#00A3A5` |
| Weiß | `#FFFFFF` | `#E4E8E9` | `#C7CCCF` |

Die linken Flächen tragen die Markenfarben von Orange und Türkis. Ober- und rechte Flächen sind nach dem
Helligkeitsverhältnis der Referenz daraus abgeleitet. JPEG-Artefakte und Glanzpixel wurden nicht übernommen.

## Wortmarke

„ReadyStackGo“ in **Figtree** mit Gewicht 760 und einer Laufweite von −0,055 em, in Pfade umgewandelt. Zur Laufzeit
wird keine Schrift geladen.

- **Ready:** `#00CED1`, **Go:** `#FF6B35`
- **Stack:** Weiß `#FFFFFF` auf dunklem Grund. Auf hellem Grund Graphit `#0E171A`: Das ist die einzige
  Kontrastvariante, Würfel, „Ready“ und „Go“ bleiben unverändert.
- In der WebUi folgt „Stack“ den Theme-Tokens `logo-stack` und `logo-stack-on-nav`.

Lage, gemessen an der Referenz, in Höhen des Zeichens:
- Die Wortmarke beginnt 0,19 rechts vom Zeichen.
- Die Versalhöhe beträgt 0,419.
- Die Grundlinie liegt 0,742 unter der Oberkante des Zeichens.

Figtree steht unter der SIL Open Font License 1.1 ([`font/OFL.txt`](font/OFL.txt)), Copyright The Figtree Project
Authors. Die Datei `font/figtree-latin-wght-normal.woff2` stammt aus `@fontsource-variable/figtree` 5.3.0. Die OFL
erlaubt Logos aus den Umrissen der Schrift.

**Abweichung von der Referenz:** Die Schrift im Bild ließ sich keiner freien Schrift zuordnen. Montserrat, Inter,
Outfit, Plus Jakarta Sans, Mulish und Lexend kamen in Frage. Figtree trifft die Buchstabenformen am besten: das „y“ mit
gebogenem Schwanz, das „G“ ohne Sporn, das zweistöckige „a“. Sie läuft aber etwas breiter. Die Wortmarke ist bei
gleicher Höhe rund 3 % breiter als im Bild. Den Vergleich zeigt [`bilder/vergleich-referenz.png`](bilder/vergleich-referenz.png).

## Dateien

| Datei | Inhalt | Wo |
|---|---|---|
| `images/logo/readystackgo-lockup-dark.svg` | Zeichen und Wortmarke für dunkle Flächen | WebUi, Website (Kopf, Fuß, Doku dunkel) |
| `images/logo/readystackgo-lockup-light.svg` | dasselbe mit Graphit-„Stack“ für helle Flächen | Website (Kopf, Fuß, Doku hell) |
| `images/logo/readystackgo-mark.svg` | Zeichen allein, quadratisch | WebUi, Website, Starlight (`src/assets`) |
| `images/logo/readystackgo-mark-small.svg`, `favicon.svg` | Zeichen mit breiterer Fuge für kleine Größen | Favicon |
| `images/logo/readystackgo-app-icon.svg` | Zeichen auf dunkler Kachel | Vorlage für `apple-touch-icon.png` |
| `favicon.png` (32 px), `apple-touch-icon.png` (180 px) | aus den SVGs gerendert | WebUi, Website |
| `packages/ui-generic/src/components/brand/logoArtwork.ts` | dieselbe Geometrie und Pfade als TypeScript | Komponenten `Logo` und `LogoMark` der WebUi |

Die Dateien liegen jeweils in `src/ReadyStackGo.WebUi/apps/rsgo-generic/public/` und
`src/ReadyStackGo.PublicWeb/public/`. Kopien zum Ansehen liegen unter [`assets/`](assets/).

## Prüfbilder

Die Bilder unter [`bilder/`](bilder/) zeigen:
- App: Leiste aus- und eingeklappt, mobile Kopfzeile, jeweils hell und dunkel
- Website: Kopf und Doku-Kopf
- Icons in 16, 24, 32 und 64 px, Apple-Icon
- Lockups auf hellem und dunklem Grund
