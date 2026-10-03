# Neues Theme und Logo für ReadyStackGo

## Ziel

ReadyStackGo soll aussehen wie ein eigenes Produkt von Wiesenwischer Works und nicht wie eine Admin-Vorlage. Die
Weboberfläche und die Website bekommen dafür ein gemeinsames Farbschema mit hellem und dunklem Modus, eine
Navigationsleiste links in der Markenfarbe und ein neues Logo aus leuchtenden Würfeln. Das Bild auf der Projektseite
von wiesenwischer.de gibt die Richtung vor.

## Was dazugehört

- **Logo:** ein neues Zeichen aus halbtransparenten, leuchtenden Würfeln, die sich zu einem Stapel fügen, dazu der
  Schriftzug „ReadyStackGo“. Als Vektor-Master (SVG) mit Ableitungen: Zeichen allein, Zeichen mit Schriftzug, für
  hellen und dunklen Grund, Favicon und App-Icon.
- **Farbschema:** Markenfarben mit Abstufungen als Tokens, für einen hellen und einen dunklen Modus.
- **Weboberfläche** (`src/ReadyStackGo.WebUi`, Paket `@rsgo/ui-generic`): Navigationsleiste links, Kopfzeile,
  Buttons, Formulare, Tabellen, Status-Anzeigen und Diagramme im neuen Schema. Das Logo in der Navigationsleiste,
  ein- und ausgeklappt.
- **Website** (`src/ReadyStackGo.PublicWeb`): Startseite, Feature-Seiten und Dokumentation (Starlight) im selben
  Schema und mit dem neuen Logo.

## Was ausdrücklich nicht dazugehört

- Neue Funktionen oder geänderte Abläufe. Es ändert sich nur das Aussehen.
- Custom Distributions: Eine Distribution bringt weiter ihr eigenes Design mit. Das neue Theme gilt nur für die
  Standard-Oberfläche `@rsgo/ui-generic`.
- Die Adresse der Website (Domain-Wechsel ist ein eigenes Thema).

## Vorgaben

- **Farben aus dem Schriftzug** (Marcus, 03.10.2026: „die drei Farben aus dem ReadyStackGo Schriftzug für die ganze
  Web Präsenz, eventuell noch weitere Abstufungen“): Türkis `#00CED1` („Ready“), Weiß („Stack“, auf hellem Grund die
  dunkle Textfarbe) und Orange `#FF6B35` („Go“). Daraus entsteht eine Skala mit Abstufungen je Farbe für Flächen,
  Ränder, Hover, aktive Zustände und Text.
- **Die Würfel im Logo tragen genau diese Farben** (Marcus, 03.10.2026).
- **Navigationsleiste links**, farbig in der Markenfarbe, nicht grau (Marcus, 03.10.2026).
- **Heller und dunkler Modus**, beide vollwertig. Der dunkle Modus orientiert sich an der Website von Wiesenwischer
  Works (fast schwarzes Anthrazit), der helle ist hell und freundlich.
- **Alternative im Entwurf:** ein helles Pastellgrün als Markenfarbe (Marcus, 03.10.2026: „helles pastellfarbenes
  Grün“, „müsste man mal probieren“). Der Entwurf zeigt beide Richtungen nebeneinander, Marcus wählt.
- **Lesbarkeit:** Text und Bedienelemente erreichen in beiden Modi mindestens WCAG AA (Kontrast 4,5 : 1 für Text,
  3 : 1 für Bedienelemente und Ränder). Status-Farben (gesund, eingeschränkt, ausgefallen, unbekannt) bleiben
  unterscheidbar und werden nicht von den Markenfarben verschluckt.
- **Tokens an einer Stelle:** Die Farben stehen als Tokens im `@theme` der App (`apps/rsgo-generic/src/index.css`)
  und der Website, nicht verstreut in Komponenten. Die Skala `brand-*` wird ersetzt, nicht ergänzt.
- **Logo als Vektor:** Das Zeichen muss bei 16 px (Favicon) noch als Würfelstapel erkennbar sein; für kleine Größen
  gibt es eine vereinfachte Fassung.

## Oberfläche und Bilder

Ein freigegebener Entwurf fehlt noch. Er entsteht vor der Planung mit `/vorhaben-entwerfen` und liegt danach unter
`docs/specs/theme-und-logo/entwurf/`. Er zeigt mindestens:

- beide Farbrichtungen (Schriftzug-Farben und Pastellgrün) in hellem und dunklem Modus,
- die Navigationsleiste links, ein- und ausgeklappt, mit aktivem Eintrag,
- eine typische Seite der App (Deployments mit Status) und die Startseite der Website,
- Logo-Varianten (Zeichen, Zeichen mit Schriftzug, klein).

Logo-Vorschläge als Rasterbild entstehen über fal.ai (`/vorhaben-grafik`); der Vektor-Master wird danach gezeichnet
oder nachgezeichnet. Richtung, nicht verbindlich: das Bild „P-RSG“ auf der Projektseite von wiesenwischer.de
(`Wiesenwischer/works`, `website/public/images/visuals/project-readystackgo.webp`).

## Abnahme

- Die App zeigt in hellem und dunklem Modus auf allen Seiten das neue Schema und das neue Logo; keine Stelle zeigt
  mehr das alte Blau `#465FFF`.
- Die Website samt Dokumentation zeigt dasselbe Schema und Logo.
- Kontrast in beiden Modi gemessen, kein Wert unter WCAG AA.
- Das Favicon ist bei 16 px als Würfelstapel erkennbar.
- Bilder aus App und Website in beiden Modi liegen neben den Bildern des Entwurfs im Umsetzungs-PR.
- „Build & Test“ und die Browsertests gegen den Container sind grün.

## Material

- Projektseite auf wiesenwischer.de mit Bild und Texten (`Wiesenwischer/works`, `website/src/content/projects.ts`).
- Design der Website von Wiesenwischer Works: `Wiesenwischer/works`, `website/docs/specs/website/` (Sheet, Entwurf,
  Tokens).
- Heutiges Logo: `src/ReadyStackGo.Api/wwwroot/images/logo/`, Schriftzug-Farben in
  `src/ReadyStackGo.PublicWeb/src/components/Header.astro`.
- Custom Distributions: `docs/Architecture/Distribution-Architecture.md`.

## Offene Fragen

- Welche Farbrichtung: Schriftzug-Farben oder Pastellgrün? Entscheidung nach dem Entwurf.
- Schrift: bleibt Outfit, oder Montserrat und Inter wie bei Wiesenwischer Works?
- Bleibt die Rakete Teil des Logos, oder trägt der Würfelstapel allein?
- Übernimmt wiesenwischer.de das neue Logo auf Karte und Projektseite? (Vorschlag: ja, im selben Zug.)
