# Claude Code Projekt-Hinweise

## Git Branching

- **Main Branch**: `main` ist der einzige permanente Branch
- **Feature Branches**: Für neue Features `feature/<name>` von `main` ableiten
- **Bugfix Branches**: Für Fehlerbehebungen `bugfix/<name>` von `main` ableiten
- **KEINE Versionsnummern** in Branch-Namen (z.B. `feature/state-machine-refactoring` statt `feature/v0.16-state-machine-refactoring`)
- **KEIN** `develop` Branch - direkt von/nach `main` arbeiten
- Nach Merge den Feature/Bugfix Branch löschen

## Commit-Regeln

- **KEIN Footer** in Commit-Messages (kein "Generated with Claude Code", kein "Co-Authored-By")
- Commit-Messages und Beschreibungen auf **Englisch oder Deutsch** (siehe „Projekt-Sprache“)

## Pull Request-Regeln

- **KEIN Footer** in PR-Beschreibungen (kein "🤖 Generated with Claude Code" o.ä.)
- PR-Titel und Beschreibungen auf **Englisch oder Deutsch**; PRs aus dem Vorhaben-Ablauf schreiben die Skills auf Deutsch

## Projekt-Sprache

- Dokumentation: Deutsch mit englischen Fachbegriffen
- Code und Kommentare: **Englisch** (keine deutschen Kommentare im Code!)
- Commits und PRs: **Englisch oder Deutsch** (Marcus, 03.10.2026: „Für ReadyStackGo gilt Englisch und deutsch“)
- Produkt: **zweisprachig, Englisch und Deutsch**. Die Website und die Dokumentation sind es schon; die Oberfläche
  der App wird **kurz vor Version 1.0** lokalisiert (Marcus, 03.10.2026), bis dahin bleiben neue Texte dort englisch

## Code-Qualität

- **Alles muss kompilieren** – vor jedem Commit `dotnet build` ausführen
- **Keine Kompilierungsfehler** – Code darf nicht committed werden wenn er nicht kompiliert
- **Keine Warnungen** – Compiler-Warnungen müssen behoben werden, nicht unterdrückt

## Tests

- **NICHT nur Happy-Path Tests** schreiben!
- **Edge Cases** sind das Wichtigste: Was passiert bei leeren Inputs, null-Werten, ungültigen IDs?
- **Fehler-Cases** abdecken: Was passiert wenn ein Service nicht erreichbar ist? Wenn eine Entity bereits in einem bestimmten Status ist?
- **State-Transitions** testen: Besonders bei Domain-Entities alle ungültigen Übergänge testen
- **Filterlogik** testen: Wenn Daten gefiltert werden (z.B. "Removed" ausblenden), explizit testen dass der Filter funktioniert
- Vor dem Schreiben von Code überlegen: "Welche Bugs könnten hier entstehen?" und dafür Tests schreiben

## Vorhaben: bauen und testen

Vorhaben laufen über GitHub (Issue „Vorhaben: …“ mit Spezifikation und Label `spezifiziert`, bei Oberflächen zuerst
ein Figma-Entwurf, dann Label `planen`, Plan-PR, Umsetzungs-PR). Den Ablauf, das Brett und die Labels beschreibt
`Wiesenwischer/works/docs/prozesse/vorhaben.md`. Die Schritte stecken in den Skills `vorhaben-entwerfen`
(Figma-Entwurf, Sitzung am PC), `vorhaben-grafik` (Raster-Grafik über fal.ai, Sitzung am PC), `vorhaben-planen`,
`vorhaben-umsetzen` und `vorhaben-pruefen`. Spezifikationen liegen unter `docs/specs/`, Pläne unter `docs/plans/`,
freigegebene Entwürfe unter `docs/specs/<name>/entwurf/`. Die älteren Skills `plan-feature` und `implement-feature`
gelten für Vorhaben nicht mehr.

Vor dem Umsetzungs-PR muss grün sein, auf dem GitHub-Runner `ubuntu-latest` (Docker ist dort vorhanden), genau wie
der Pflicht-Check „Build & Test“ (`.github/workflows/ci.yml`):

- `dotnet restore`, `dotnet build --configuration Release --no-restore`
- `dotnet test` für `tests/ReadyStackGo.UnitTests`, `tests/ReadyStackGo.IntegrationTests` und
  `tests/ReadyStackGo.DomainTests` (`--configuration Release --no-build`)
- in `src/ReadyStackGo.WebUi`: `corepack enable`, dann `pnpm install --frozen-lockfile`, `pnpm run lint`,
  `pnpm exec tsc -b`, `pnpm run test` und `pnpm run build`
- bei Änderungen an der Oberfläche zusätzlich die Browsertests gegen den Container: `docker compose build`,
  `docker compose up -d`, dann in `src/ReadyStackGo.WebUi` `pnpm exec playwright install --with-deps chromium` und
  `pnpm run test:e2e:container` für die betroffenen Tests; danach `docker compose down -v`. Bilder der Oberfläche
  selbst ansehen.

Es gibt noch keine Umsetzungsrolle (Label `umsetzung:<rolle>`): Jede Umsetzung läuft auf `ubuntu-latest`.

Ein Merge nach `main` rollt nichts aus. Ausgerollt wird über ein Release.

## Docker / Container

- **Container immer mit `docker compose` bauen und starten** (im Projektroot)
  - `docker compose build` - Image bauen
  - `docker compose up -d` - Container starten
  - `docker compose down -v` - Container stoppen und Volumes löschen
- **Port: 8080** - Die Anwendung läuft auf http://localhost:8080 (NICHT 5080!)

## Context7 (MCP Server)

- Bei Fragen zu externen Libraries (Playwright, ASP.NET, Blazor, Docker, YamlDotNet, MediatR, FluentAssertions, Astro/Starlight, etc.) immer **Context7** verwenden um aktuelle Dokumentation abzurufen
- Syntax: `use context7` im Prompt oder direkt die Context7 MCP Tools nutzen
- Besonders wichtig bei: Library-Updates, neuen API-Methoden, versionsspezifischen Features

## Sonstiges

- SSL-Verifizierung für Git ist deaktiviert (abgelaufenes TFS-Zertifikat)
