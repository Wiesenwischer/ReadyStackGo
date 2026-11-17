# Auftrag an Claude: ReadyStackGo auf das neue Logo umstellen

Stand: 05.10.2026 · Repository: https://github.com/Wiesenwischer/ReadyStackGo

## 1. Auftrag

Stelle die vorhandenen ReadyStackGo-Markenassets und deren bestehende Verwendungsstellen auf das vom Benutzer ausgewählte Logo um. Die verbindliche visuelle Referenz liegt unter `referenz/ReadyStackGo_Neues_Logo.jpeg`.

Dieses Dokument ist ein eigenständiger Implementierungsauftrag. Der frühere Chat und die umfangreiche UI-Spezifikation werden nicht benötigt. Erstelle einen kurzen konkreten Plan und führe die Logo-Umstellung anschließend selbstständig durch. Liefere eine lokal prüfbare Umsetzung mit Screenshots und Abschlussbericht.

**Der Umfang ist ausschließlich die Logo-Umstellung.** Keine neue UI, kein Dashboard-Redesign, kein Lime-Theme, keine neuen Produktfunktionen und keine Änderungen der fachlichen Logik. Bereits laufende Theme-/Preview-Arbeiten bewahren. Frühere Sechs-Würfel-Entwürfe und alternative Graphit-/Lime-Marken sind für diesen Auftrag verworfen. Die ausgewählte Drei-Würfel-Marke ist maßgeblich.

## 2. Arbeitsweise und Branch

1. ZIP in ein temporäres Verzeichnis entpacken, Inhalt prüfen und dieses Briefing plus Referenz nach bestehender Repo-Konvention ablegen, beispielsweise `docs/branding/logo-refresh/`. Keine absoluten ZIP-Pfade oder Pfadtraversierung zulassen, keine fremden Dateien überschreiben.
2. AGENTS.md und vorhandene Entwicklungs-/Branding-Konventionen lesen. Arbeitsverzeichnis, Branch und nicht eingecheckte Änderungen prüfen.
3. Auf einem separaten Feature-/Integrationsbranch arbeiten, beispielsweise `feature/readystackgo-logo-refresh`. Ist bereits ein passender Branch vorhanden, diesen nutzen. Unabhängige lokale Änderungen erhalten; nicht zurücksetzen oder ungefragt in eigene Commits aufnehmen.
4. Keine direkte Änderung von main, kein automatischer Merge, kein Release und keine produktive Veröffentlichung. Das Ergebnis muss vor einer späteren Freigabe prüfbar sein.
5. Für normale Asset-/Integrationsentscheidungen nicht nach jedem Schritt eine Freigabe verlangen. Bei einem tatsächlichen Blocker präzise berichten.

## 3. Verbindliches Erscheinungsbild

### Symbol

Genau **drei gleich große isometrische Würfel**, kompakt dreieckig angeordnet:

- oben: Orange;
- unten links: Türkis;
- unten rechts: Weiß mit hellgrauen Seitenflächen.

Gleiche Perspektive, identische Kantenlängen und konsistente schmale Zwischenräume. Die Seitenflächen vermitteln dezente Tiefe. Keine zusätzlichen Würfel, keinen Rahmen um das Hauptzeichen, keine dicken grauen Konturen und keine schwarzen Linien, die in der Mitte über Würfelflächen hinausragen. Kein Kreuz/Stern zwischen den Würfeln. Keine Glows, Neon-Effekte, Chromoptik oder neue Schatten. Geometrie und räumliche Flächenwirkung eng am ausgewählten Bild halten.

### Wortmarke

Exakter zusammenhängender Text: **ReadyStackGo**.

| Segment | Farbe in der Referenz |
|---|---|
| Ready | Türkis |
| Stack | Weiß |
| Go | Orange |

Groß-/Kleinschreibung, Segmentierung und Reihenfolge erhalten. Keine Leerzeichen zwischen den Segmenten, keine neue Tagline und kein anderer Produktname. Schrift kräftig, modern und gut lesbar, mit dem Referenzbild vergleichbaren Buchstabenformen und Proportionen. Nicht übermäßig breit oder schwer neu gestalten. Symbol links, Wortmarke rechts; Abstand und optische Ausrichtung am Referenzmotiv orientieren. Symbol und Text immer proportional skalieren, niemals unabhängig strecken.

### Farben und Produktionsvorlage

Farben nach Möglichkeit aus den tatsächlich vorhandenen passenden Brand-Assets übernehmen und mit der ausgewählten Referenz abgleichen. Andernfalls saubere Flächenfarben aus der Referenz ableiten und zentral dokumentieren. JPEG-Artefakte und einzelne Glanzpixel nicht als Farbdefinition verwenden. Keine willkürlichen neuen Türkis-/Orange-Werte über verschiedene Assets verteilen.

Das JPEG besitzt einen dunklen Präsentationshintergrund und große Außenränder. **Diese gehören nicht zum transparenten Produktionslogo.** Das JPEG unverändert als Referenz erhalten. Nicht das gesamte Bild als Headerlogo einbauen. Keine automatische Neugenerierung mit KI: Das Motiv ist ausgewählt; es geht um präzise technische Aufbereitung und Integration.

## 4. Produktionsassets

Zuerst prüfen, ob im Repository bereits brauchbare Vektorquellen für dieses Motiv oder passende Wortmarken existieren. Wiederverwenden, wenn sie mit der Referenz übereinstimmen. Andernfalls das ausgewählte Symbol geometrisch sauber als echtes SVG rekonstruieren; kein JPEG/PNG in ein SVG einbetten und als Vektor ausgeben. Wortmarke mit passender vorhandener/lizenzierter Schrift setzen und für das transportable Logo nach Möglichkeit in Pfade umwandeln. Verwendete Schrift und Lizenz dokumentieren; keine bezahlte Schrift ungefragt beschaffen. Ist nur eine nahe Annäherung verfügbar, Abweichung im Abschlussbericht benennen.

Mindestens liefern bzw. vorhandene Entsprechungen aktualisieren:

- horizontales Logo für dunkle Flächen, transparenter Hintergrund;
- Symbol ohne Wortmarke, transparenter Hintergrund;
- geeignete kontrastfähige Variante für helle bestehende Flächen;
- Favicon-/App-Icon-Derivate in den tatsächlich vom Projekt benötigten Formaten/Größen;
- bei bestehenden Raster-Verbrauchern passende PNG-Exporte aus derselben Vektorquelle.

Dateinamen an Repo-Konvention anpassen; denkbar sind `readystackgo-logo-dark.svg`, `readystackgo-logo-light.svg`, `readystackgo-mark.svg`. SVGs mit sinnvollem viewBox, ohne riesige leere Ränder, ohne externe Referenzen oder Scripts. Kein Fontdownload zur Laufzeit für die Wortmarke. Alle Exporte müssen aus derselben Masterquelle stammen; nicht mehrere leicht unterschiedliche Symbole manuell nachbauen.

### Helle Flächen

Die Dark-Referenz bleibt die Hauptmarke. Weißer Stack-Text ist auf Weiß nicht lesbar. Prüfe die vorhandene Light-Logo-Konvention: Für direkt helle Flächen darf ausschließlich der **Stack-Schriftzug** als dokumentierte Kontrastvariante in dunklem Graphit ausgeführt werden. Ready bleibt Türkis, Go bleibt Orange; der weiße Würfel bleibt weiß mit ausreichend sichtbaren grauen Seitenflächen. Die Produktidentität wird dadurch nicht neu eingefärbt. Ist die bestehende Logo-Fläche ohnehin dunkel, das Original unverändert verwenden. Keine neue dunkle Sidebar oder großflächige Hintergrundplatte allein für die Logo-Umstellung einführen.

Für sehr kleine Icons das Drei-Würfel-Symbol ohne Wortmarke nutzen. Details/Abstände nur soweit optisch nötig vereinfachen, ohne Farbe oder Anordnung zu ändern. Bei 16, 24 und 32 px prüfen, bei größeren Icons scharfe transparente Kanten. Für vorhandene App-Icon-Kacheln deren Plattformanforderungen respektieren.

## 5. Inventar und Integration

Alle bestehenden ReadyStackGo-Logo-Verwendungen ermitteln; zuerst `rg`/Asset-Inventar und dann tatsächliche Imports/CSS/HTML/Manifest-Verweise prüfen. Kein ungesichtetes globales Suchen-und-Ersetzen.

Prüfen, soweit im Repository vorhanden:

- Web UI: Sidebar expanded/collapsed, Header und mobile Navigation;
- Login, Setup/Wizard, OIDC-/Auth-Seiten und bestehende Wartungsseiten;
- klassische und Preview-Oberfläche, falls bereits vorhanden;
- Public Website: Header/Footer und bestehende Produktmarken;
- favicon, Webmanifest, Apple-/PWA-Icons und vorhandene App-/Installer-Assets;
- aktive Dokumentation/README-Bilder und aktuelle sonstige Branding-Verwendungen.

Historische Screenshots/Releasebelege nicht nachträglich umzeichnen. Fremdmarken wie Wiesenwischer Works, WYSCH oder Grafana unverändert lassen. Keine neuen Plattformen oder Icon-Verwendungen einführen, nur weil sie in dieser Checkliste stehen.

Vorhandene zentrale Logo-Komponente bevorzugt aktualisieren. Doppelte Inline-SVGs auf eine gemeinsame Quelle konsolidieren, soweit ohne breites Refactoring möglich. Symbol-/Wortmarken-Varianten und Themeauswahl explizit abbilden. Beschriftung bzw. accessible name `ReadyStackGo`; wenn sichtbarer Text dieselbe Information liefert, dekoratives Symbol nicht doppelt vorlesen lassen. Navigation/Links, Focus und Klickfläche erhalten.

Das neue Logo ist nach Integration die gemeinsame Produktmarke. **Keine neue Preview-Umschaltung ausschließlich für das Logo bauen.** Bestehende Classic-/Preview-Einstellungen und Features bleiben unverändert; beide dürfen dasselbe neue Markenasset verwenden. Der separate Branch ermöglicht die Prüfung vor Übernahme.

Layout nur soweit nötig für korrekte Logo-Proportionen anpassen. Bestehende Header-/Sidebar-Größen möglichst erhalten. Logo nicht winzig rendern, weil die Referenz große Außenränder enthält. Keine neuen globalen CSS-Farben, Buttonfarben, Schriftarten oder Themes allein wegen der Wortmarke ändern. Cachebusting nach vorhandener Build-Konvention für Favicons/Assets beachten.

## 6. Prüfung und Abnahme

Relevante bestehende Lint-/Type-/Build-Checks anhand aktueller Scripts ausführen. Kein eigenes großes Testsystem für statische Assets einführen; vorhandene Tests bei tatsächlich betroffenem Komponentenverhalten verwenden.

Visuell prüfen und Screenshots liefern:

- vollständiges Logo auf dunkler und heller Fläche;
- Sidebar expanded/collapsed und mobile Navigation;
- Login/Setup oder andere vorhandene prominent betroffene Screens;
- vorhandene Classic-/Preview-Modi ohne Funktions-/Themeänderungen;
- Symbol bei kleinen Icon-Größen und korrektes Favicon;
- kein Cropping, Stauchen, Farbwechsel, kaputte Imports, unsichtbarer weißer Text oder unnötige Außenränder.

Vergleich mit `referenz/ReadyStackGo_Neues_Logo.jpeg`: drei Würfel, orange oben, türkis unten links, weiß unten rechts; Wortmarke korrekt und proportional. Kleine kontrastbedingte Light-Variante separat zeigen. Keine Alternativentwürfe anstelle des ausgewählten Logos liefern.

## 7. Definition of Done

- Saubere wiederverwendbare Vektorquelle und benötigte Exporte vorhanden.
- Aktuelle tatsächlich gefundene Branding-Verwendungen auf neues Logo umgestellt.
- Alte aktive Sechs-Würfel-/sonstige Logoquellen nicht mehr verwendet, historische Belege bleiben erhalten.
- Dark/Light, responsive und ggf. Classic/Preview geprüft.
- Vorhandene Themes, Funktionen, Routes und Backend unverändert.
- Ergebnis auf separatem Branch, ohne Merge oder Veröffentlichung.
- Kurzer Bericht: Branch, geänderte Dateien/Verwendungen, Assetquellen/Farben/Schriftlizenz, ausgeführte Checks, Screenshots und reale Abweichungen oder nicht prüfbare Stellen.

## 8. Startauftrag zum direkten Verwenden

> Entpacke das beigefügte ReadyStackGo-Logo-ZIP sicher und lies diese Markdown vollständig. Betrachte die beigefügte Logo-Referenz. Prüfe den aktuellen ReadyStackGo-Stand und setze ausschließlich die beschriebene Logo-Umstellung auf einem separaten Feature-/Integrationsbranch um. Erstelle präzise Produktionsassets, aktualisiere alle vorhandenen aktiven Logo-Verwendungen und prüfe Dark/Light, kleine Icons sowie responsive Darstellung. Erhalte alle bestehenden Themes, Preview-Arbeiten und Funktionen. Keine neue UI, keine neuen Features, kein Merge und keine Veröffentlichung. Liefere eine vollständig umgesetzte, lokal prüfbare Änderung mit Screenshots und Abschlussbericht.
