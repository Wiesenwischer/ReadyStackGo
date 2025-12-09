# Claude Code Projekt-Hinweise

## Git Branching

- **Main Branch**: `main` ist der einzige permanente Branch
- **Feature Branches**: Für neue Features `feature/<name>` von `main` ableiten
- **Bugfix Branches**: Für Fehlerbehebungen `bugfix/<name>` von `main` ableiten
- **KEIN** `develop` Branch - direkt von/nach `main` arbeiten
- Nach Merge den Feature/Bugfix Branch löschen

## Commit-Regeln

- **KEIN Footer** in Commit-Messages (kein "Generated with Claude Code", kein "Co-Authored-By")
- Commit-Messages auf Deutsch oder Englisch, kurz und prägnant

## Projekt-Sprache

- Dokumentation: Deutsch mit englischen Fachbegriffen
- Code/Kommentare: Englisch

## Sonstiges

- SSL-Verifizierung für Git ist deaktiviert (abgelaufenes TFS-Zertifikat)
