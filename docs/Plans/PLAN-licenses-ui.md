# Phase: Third-Party Licenses UI

## Ziel

Nutzer und Betreiber sollen die lizenzierten Drittanbieter-Pakete (npm + .NET) direkt in der RSGO-App einsehen können — über eine neue "Licenses"-Seite in den Settings. Die JSON-Lizenzdaten werden bereits automatisch per CI generiert und ins Repository committed; sie müssen nur noch in das Docker-Image gebundelt und im WebUI angezeigt werden.

## Analyse

### Bestehende Architektur

- **Lizenzdaten** (`licenses/`): `npm-webui-licenses.json`, `npm-publicweb-licenses.json` vorhanden; `dotnet-licenses.json` wird vom CI-Workflow generiert (`.github/workflows/licenses.yml`) aber war beim letzten Check nicht committed
- **CI-Workflow** (`.github/workflows/licenses.yml`): Generiert alle drei JSON-Dateien automatisch bei Änderungen an `package.json`/`pnpm-lock.yaml`/`.csproj`
- **PublicWeb** hat statische Licenses-Seite (`src/content/docs/*/licenses.md`) — aber nur für die Website, nicht für die laufende App
- **Settings-Pattern**: `SettingsIndex.tsx` zeigt Cards → Subseiten (`/settings/system`, `/settings/tls`, etc.); alle exports über `src/pages/Settings/index.ts`; Route in `App.tsx`
- **Static Files**: `UseStaticFiles()` in `Program.cs` serviert `wwwroot/*` automatisch — License-JSONs können als statische Dateien in `wwwroot/licenses/` gebundelt werden

### Betroffene Bounded Contexts

- **Infrastructure / Hosting**: Dockerfile (License-JSONs nach wwwroot/licenses/ kopieren)
- **WebUI**: Neue Settings-Seite, SettingsIndex-Card, Route

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: License-JSONs in Docker-Image bundeln** — In Dockerfile nach dem `COPY --from=frontend-build` Schritt die JSON-Dateien aus `licenses/` nach `wwwroot/licenses/` kopieren
  - Betroffene Dateien: `Dockerfile`
  - Hinweis: `dotnet-licenses.json` muss ggf. per CI-Workflow generiert werden bevor es im Build vorhanden ist
  - Abhängig von: -

- [ ] **Feature 2: Licenses-Seite** — Neue React-Seite `Settings/Licenses/Licenses.tsx`; fetcht die drei JSON-Dateien von `/licenses/*.json`; zeigt alle Pakete mit Name, Version, Lizenztyp, Repository-Link; Suchfeld + Lizenztyp-Filter; Badge mit Paket-Anzahl pro Kategorie (npm WebUI / npm PublicWeb / .NET)
  - Betroffene Dateien: `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/Licenses/Licenses.tsx` (neu), `Settings/index.ts` (export)
  - Pattern-Vorlage: `Settings/System/SystemInfo.tsx` für Seiten-Layout
  - Abhängig von: Feature 1

- [ ] **Feature 3: Settings-Integration** — Card in `SettingsIndex.tsx` (Icon: DocsIcon oder ähnliches); Route `/settings/licenses` in `App.tsx`; Import in `apps/rsgo-generic/src/App.tsx`
  - Betroffene Dateien: `SettingsIndex.tsx`, `apps/rsgo-generic/src/App.tsx`
  - Abhängig von: Feature 2

## Test-Strategie

- **Manuell**: `docker compose build && docker compose up -d` → Settings → Licenses → Pakete sichtbar, Suche funktioniert
- **Unit Tests**: Keine neuen (reine Darstellungskomponente)
- **E2E Tests**: Optional — Screenshot für Doku

## Offene Punkte

- [ ] Prüfen ob `dotnet-licenses.json` aktuell im Repo vorhanden ist; ggf. CI-Workflow manuell triggern
- [ ] Entscheiden ob fehlende JSON-Datei (z.B. dotnet) silently ignoriert wird oder einen Fehler zeigt

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Datei-Serving | API-Endpoint / Static Files | Static Files (`wwwroot/licenses/`) | Einfachste Lösung, kein neuer Endpoint nötig |
| Fehlerbehandlung fehlende JSON | Fehler anzeigen / Sektion ausblenden | Sektion ausblenden | Graceful degradation — dotnet-licenses.json kann fehlen |
| Gruppierung | Alles zusammen / Nach Kategorie | Nach Kategorie (npm WebUI, npm PublicWeb, .NET) | Bessere Übersicht, klarer Bezug zur Komponente |
