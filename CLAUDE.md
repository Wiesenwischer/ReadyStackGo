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
- Commit-Messages auf Deutsch oder Englisch, kurz und prägnant

## Pull Request-Regeln

- **KEIN Footer** in PR-Beschreibungen (kein "🤖 Generated with Claude Code" o.ä.)

## Projekt-Sprache

- Dokumentation: Deutsch mit englischen Fachbegriffen
- Code/Kommentare: Englisch

## Tests

- **NICHT nur Happy-Path Tests** schreiben!
- **Edge Cases** sind das Wichtigste: Was passiert bei leeren Inputs, null-Werten, ungültigen IDs?
- **Fehler-Cases** abdecken: Was passiert wenn ein Service nicht erreichbar ist? Wenn eine Entity bereits in einem bestimmten Status ist?
- **State-Transitions** testen: Besonders bei Domain-Entities alle ungültigen Übergänge testen
- **Filterlogik** testen: Wenn Daten gefiltert werden (z.B. "Removed" ausblenden), explizit testen dass der Filter funktioniert
- Vor dem Schreiben von Code überlegen: "Welche Bugs könnten hier entstehen?" und dafür Tests schreiben

## Docker / Container

- **Container immer mit `docker compose` bauen und starten** (im Projektroot)
  - `docker compose build` - Image bauen
  - `docker compose up -d` - Container starten
  - `docker compose down -v` - Container stoppen und Volumes löschen
- **Port: 8080** - Die Anwendung läuft auf http://localhost:8080 (NICHT 5080!)

## Sonstiges

- SSL-Verifizierung für Git ist deaktiviert (abgelaufenes TFS-Zertifikat)
