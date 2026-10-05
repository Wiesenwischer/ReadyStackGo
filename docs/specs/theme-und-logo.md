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
- **Theme-Auswahl in der App** (Marcus, 03.10.2026): Unter Settings wählt man das Theme der Weboberfläche. Zur Wahl
  stehen drei Themes, jedes mit hellem und dunklem Modus: Türkis (die Farben des Schriftzugs), Pastellgrün und das
  bisherige Blau als „Classic“. Hell oder dunkel bleibt zusätzlich über den Knopf in der Kopfzeile umschaltbar.
  Unter Settings gibt es als dritten Modus „System“, der dem Betriebssystem folgt (Marcus, 05.10.2026, Entwurf E).
- **Themes zur Laufzeit ladbar** (Marcus, 04.10.2026: „dass man die Themes irgendwie optional oder zur Laufzeit
  anladen kann, so dass … nur das eigene Theme in den Container mitgedacht wird oder vielleicht sogar an anderer
  offenerer Stelle geladen wird“): Ein Theme ist ein Paket aus Beschreibung und Token-Werten, kein fest eingebauter
  Code. Die App lädt die verfügbaren Theme-Pakete beim Start. Ein Image oder eine Installation legt fest, welche
  Pakete es gibt und welches der Standard ist; eine Distribution kann so nur ihr eigenes Theme mitliefern. Pakete
  können außer im Image auch aus einem Verzeichnis kommen, das man dem Container mitgibt. Gibt es nur ein Theme,
  zeigt Settings → Appearance keine Theme-Auswahl, nur hell und dunkel. Das Format der Pakete ist dokumentiert, damit
  Dritte eigene Themes bauen können.
- **Weboberfläche** (`src/ReadyStackGo.WebUi`, Paket `@rsgo/ui-generic`): Navigationsleiste links, Kopfzeile,
  Buttons, Formulare, Tabellen, Status-Anzeigen und Diagramme im neuen Schema. Das Logo in der Navigationsleiste,
  ein- und ausgeklappt.
- **Website** (`src/ReadyStackGo.PublicWeb`): Startseite, Feature-Seiten und Dokumentation (Starlight) im selben
  Schema und mit dem neuen Logo.

## Was ausdrücklich nicht dazugehört

- Neue Funktionen oder geänderte Abläufe, außer der Theme-Auswahl unter Settings. Sonst ändert sich nur das Aussehen.
- Eine Theme-Auswahl auf der Website und in der Dokumentation: Sie zeigen das Theme Türkis mit hellem und dunklem
  Modus (Marcus, 03.10.2026).
- Themes einer Distribution: Eine Distribution baut ihr Theme selbst und hält es in ihrem eigenen Repo. In diesem
  Repo stehen nur der Mechanismus, das Format und die drei Standard-Themes.
- Laden von Theme-Paketen über eine URL aus dem Netz: vorerst nicht, nur aus dem Image und aus einem Verzeichnis.
- Die Adresse der Website (Domain-Wechsel ist ein eigenes Thema).
- Lokalisierung der Oberfläche: Sie kommt kurz vor Version 1.0 (Marcus, 03.10.2026). Neue oder geänderte Texte
  bleiben bis dahin englisch.

## Vorgaben

- **Farben aus dem Schriftzug** (Marcus, 03.10.2026: „die drei Farben aus dem ReadyStackGo Schriftzug für die ganze
  Web Präsenz, eventuell noch weitere Abstufungen“): Türkis `#00CED1` („Ready“), Weiß („Stack“, auf hellem Grund die
  dunkle Textfarbe) und Orange `#FF6B35` („Go“). Daraus entsteht eine Skala mit Abstufungen je Farbe für Flächen,
  Ränder, Hover, aktive Zustände und Text.
- **Die Würfel im Logo tragen genau diese Farben** (Marcus, 03.10.2026).
- **Navigationsleiste links**, farbig in der Markenfarbe, nicht grau (Marcus, 03.10.2026).
- **Heller und dunkler Modus**, beide vollwertig. Der dunkle Modus orientiert sich an der Website von Wiesenwischer
  Works (fast schwarzes Anthrazit), der helle ist hell und freundlich.
- **Modern und pastellig:** Die Flächen sind hell und zart getönt, die Markenfarben setzen leuchtende Akzente wie
  die Glaswürfel im Bild der Projektseite, keine kräftig eingefärbten Flächen (Marcus, 03.10.2026: „mehr in so eine
  pastellfarbene Richtung“, „es sollte schon modern wirken“).
- **Drei Themes:** Türkis ist der Standard. Pastellgrün ist ein helles Pastellgrün als Markenfarbe (Marcus,
  03.10.2026: „helles pastellfarbenes Grün“). Classic ist das bisherige Erscheinungsbild mit dem Blau `#465FFF`. Alle
  drei stehen in der App zur Wahl (Marcus, 03.10.2026); dass Türkis der Standard ist, folgt aus dem Ziel des
  Vorhabens.
- **Drei weitere Themes** (Marcus, 05.10.2026: „Lass doch alle drei“): Aurora (Violett, dunkel Mitternachts-Indigo),
  Graphite Lime (Graphit mit Limettengrün, dunkel warmes Anthrazit) und Magenta (dunkel Pflaume und Wein). Jeder
  dunkle Modus hat einen eigenen Grundton, nicht nur eine andere Akzentfarbe (Marcus, 05.10.2026: „Der dark Mode
  sieht iwie immer gleich aus nur mit anderer akzentfarbe“). Classic steht in der Auswahl zuletzt.
- **Schrift:** Montserrat für Überschriften und Schriftzug, Inter für Text, wie bei Wiesenwischer Works (Marcus,
  03.10.2026).
- **Logo ohne Rakete:** Das Zeichen besteht nur aus dem Würfelstapel (Marcus, 03.10.2026).
- **Lesbarkeit:** Text und Bedienelemente erreichen in beiden Modi mindestens WCAG AA (Kontrast 4,5 : 1 für Text,
  3 : 1 für Bedienelemente und Ränder). Status-Farben (gesund, eingeschränkt, ausgefallen, unbekannt) bleiben
  unterscheidbar und werden nicht von den Markenfarben verschluckt.
- **Tokens an einer Stelle:** Die App definiert die Tokens im `@theme` (`apps/rsgo-generic/src/index.css`), die
  Werte je Theme liefert das Theme-Paket. Die Website hat ihre Tokens im eigenen `@theme`. Nichts verstreut in
  Komponenten. Komponenten verwenden nur semantische Tokens; jedes Theme ist ein Satz von Werten für diese Tokens, hell
  und dunkel.
- **Logo als Vektor:** Das Zeichen muss bei 16 px (Favicon) noch als Würfelstapel erkennbar sein; für kleine Größen
  gibt es eine vereinfachte Fassung.

## Oberfläche und Bilder

Verbindlich: Entwurf in `docs/specs/theme-und-logo/entwurf/` (freigegeben mit dem Merge von PR #481, Settings →
Appearance neu gestaltet mit dem Merge von PR #488). Er zeigt
mindestens:

- beide Farbrichtungen (Schriftzug-Farben und Pastellgrün) in hellem und dunklem Modus,
- die Theme-Auswahl unter Settings,
- die Navigationsleiste links, ein- und ausgeklappt, mit aktivem Eintrag,
- eine typische Seite der App (Deployments mit Status) und die Startseite der Website,
- Logo-Varianten (Zeichen, Zeichen mit Schriftzug, klein).

Logo-Vorschläge als Rasterbild entstehen über fal.ai (`/vorhaben-grafik`); der Vektor-Master wird danach gezeichnet
oder nachgezeichnet. Richtung, nicht verbindlich: das Bild „P-RSG“ auf der Projektseite von wiesenwischer.de
(`Wiesenwischer/works`, `website/public/images/visuals/project-readystackgo.webp`).

## Abnahme

- Die App zeigt in den Themes Türkis und Pastellgrün, hell und dunkel, auf allen Seiten das neue Schema und das neue
  Logo; dort zeigt keine Stelle mehr das alte Blau `#465FFF`. Classic zeigt das bisherige Blau mit dem neuen Logo.
- Das gewählte Theme bleibt nach einem Neuladen erhalten; ohne Wahl gilt der Standard der Installation (im
  Standard-Image Türkis).
- Ein Theme-Paket, das man dem Container über ein Verzeichnis mitgibt, erscheint ohne neues Image in der Auswahl;
  eine Installation mit nur einem Theme zeigt keine Theme-Auswahl.
- Die Website samt Dokumentation zeigt das Theme Türkis und das neue Logo.
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

- Welches Logo-Zeichen? Marcus wählt aus den Vorschlägen im Entwurf, danach wird der Vektor-Master gezeichnet.
- Übernimmt wiesenwischer.de das neue Logo auf Karte und Projektseite? (Vorschlag: ja, im selben Zug.)
