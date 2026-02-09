---
name: implement-feature
description: Implement the next feature from the roadmap or a specific feature by name
disable-model-invocation: true
argument-hint: "[feature-name]"
---

# Feature implementieren

Implementiere ein neues Feature für ReadyStackGo.

**Feature**: $ARGUMENTS

---

## Phasen- und Branch-Konzept

Jede Roadmap-Version (z.B. v0.18) ist eine **Phase** mit mehreren Features. Die Branch-Struktur:

```
main
 └── integration/<phase-name>          (langlebig, mehrere Tage)
      ├── feature/<feature-name>       (kurzlebig, max 1 Tag)
      ├── feature/<feature-name>       (kurzlebig, max 1 Tag)
      └── feature/<feature-name>       (kurzlebig, max 1 Tag)
```

- **Integration Branch**: `integration/<phase-name>` – Sammelbranch für alle Features einer Phase
- **Feature Branches**: `feature/<name>` – Einzelne Features, werden in den Integration Branch gemerged
- **Branch-Namen OHNE Versionsnummern** (z.B. `integration/init-container-ux`, nicht `integration/v0.18`)
- Feature Branches nutzen das Prefix `feature/` damit der **Auto-Labeler** sie korrekt als `feature` labelt

### Auto-Labeler Regeln (Release Drafter)
- `feature/*` → Label `feature`
- `refactor/*` → Label `enhancement`
- `fix/*`, `bugfix/*`, `hotfix/*` → Label `bug`
- `chore/*` → Label `maintenance`

---

## Schritt 1: Kontext erfassen

1. Lies die **Roadmap** (`docs/Reference/Roadmap.md`) und identifiziere das nächste geplante Feature unter "Planned".
   - Falls `$ARGUMENTS` angegeben wurde, suche dieses spezifische Feature.
   - Falls `$ARGUMENTS` leer ist, nimm das **nächste geplante Feature** (niedrigste Version unter "Planned").
2. Lies die **Projektrichtlinien** (`CLAUDE.md`) für Branch-Konventionen, Commit-Regeln und Test-Anforderungen.
3. Lies relevante **bestehende Implementierungen** um Patterns und Architektur zu verstehen.
4. Erstelle eine Zusammenfassung des Features mit:
   - Was genau implementiert werden soll
   - Welche bestehenden Dateien betroffen sind
   - Welche neuen Dateien erstellt werden müssen

## Schritt 2: Offene Punkte klären

Bevor du mit der Implementierung beginnst:
- Identifiziere **Unklarheiten** und **Entscheidungen** die getroffen werden müssen
- Frage den User explizit nach offenen Punkten
- Kläre technische Ansätze wenn es mehrere Möglichkeiten gibt
- Stelle sicher, dass der **Scope** klar definiert ist

**Implementiere NICHTS bevor alle Fragen geklärt sind!**

## Schritt 3: Branches erstellen

### Prüfe ob ein Integration Branch für die aktuelle Phase existiert:
```bash
git checkout main && git pull
git branch -a | grep integration/
```

### Falls kein Integration Branch existiert:
```bash
git checkout -b integration/<phase-name>
git push -u origin integration/<phase-name>
```

### Feature Branch vom Integration Branch ableiten:
```bash
git checkout integration/<phase-name>
git checkout -b feature/<feature-name>
```

## Schritt 4: Implementierung planen

Nutze den Plan Mode um einen detaillierten Implementierungsplan zu erstellen:
- Identifiziere alle betroffenen Dateien
- Plane die Reihenfolge der Änderungen
- Berücksichtige bestehende Patterns im Codebase
- Plane die Test-Strategie

## Schritt 5: Tests schreiben

Für jedes Feature müssen **drei Test-Ebenen** abgedeckt werden:

### Unit Tests (xUnit + FluentAssertions)
- Pfad: `tests/ReadyStackGo.UnitTests/`
- **Nicht nur Happy-Path!** Edge Cases und Fehler-Cases sind das Wichtigste
- Teste ungültige Inputs, null-Werte, leere Collections
- Teste State-Transitions und ungültige Übergänge
- Teste Filterlogik explizit

### Integration Tests (TestContainers)
- Pfad: `tests/ReadyStackGo.IntegrationTests/`
- Teste Zusammenspiel von Services
- Teste Datenbankzugriffe und Persistenz
- Teste API-Endpoints end-to-end

### E2E Tests (Playwright)
- Pfad: `src/ReadyStackGo.WebUi/e2e/`
- **Bei JEDEM Schritt Screenshots machen:**
  ```typescript
  await page.screenshot({ path: `screenshots/<test-name>-step-01-<beschreibung>.png` });
  ```
- Teste den kompletten User-Flow durch die UI
- Teste Fehlerfälle auch in der UI (Fehlermeldungen, Validierung)
- Nutze aussagekräftige Selektoren (data-testid, ARIA roles)

## Schritt 6: Feature implementieren

- Implementiere das Feature gemäß dem Plan aus Schritt 4
- Halte dich an die bestehende Architektur (DDD, Clean Architecture, MediatR)
- Code und Kommentare auf Englisch
- Dokumentation auf Deutsch mit englischen Fachbegriffen
- Baue den Docker Container und teste lokal:
  ```bash
  docker compose build
  docker compose up -d
  ```

## Schritt 7: Verifizierung

**ALLE Tests müssen grün sein bevor ein PR erstellt wird!**

1. **Unit Tests ausführen:**
   ```bash
   dotnet test tests/ReadyStackGo.UnitTests/
   ```
2. **Integration Tests ausführen:**
   ```bash
   dotnet test tests/ReadyStackGo.IntegrationTests/
   ```
3. **E2E Tests ausführen:**
   ```bash
   cd src/ReadyStackGo.WebUi && npx playwright test
   ```
4. **Docker Container testen:**
   ```bash
   docker compose build && docker compose up -d
   ```
   Anwendung auf http://localhost:8080 prüfen.

## Schritt 8: Feature-PR erstellen

1. Alle Änderungen committen (kurze, prägnante Commit-Messages, KEIN Footer)
2. Branch pushen
3. PR erstellen **gegen den Integration Branch** (nicht gegen main!):
   ```bash
   gh pr create --base integration/<phase-name> --title "..." --body "..."
   ```
4. **KEIN Footer** in PR-Beschreibungen
5. CI-Checks abwarten
6. PR mergen und Feature Branch löschen

## Schritt 9: Dokumentation & Website (pro Phase)

Wenn **alle Features einer Phase** implementiert sind, folgende Schritte durchführen:

### Wiki / Dokumentation (`docs/`)
- Neue oder geänderte Features dokumentieren (Deutsch mit englischen Fachbegriffen)
- Bestehende Seiten aktualisieren wenn sich Verhalten ändert
- Wird automatisch ins GitHub Wiki synchronisiert (`.github/workflows/wiki.yml`)

### Public Website (`src/ReadyStackGo.PublicWeb/`)
- **Neue Features** in der Feature-Übersicht auflisten
- **User-Dokumentation** erweitern (Astro/Starlight, bilingual DE/EN)
- Content-Pfad: `src/ReadyStackGo.PublicWeb/src/content/docs/`
  - Deutsche Docs: `de/`
  - Englische Docs: `en/`

### Roadmap aktualisieren
- Feature von "Planned" nach "Released" verschieben in `docs/Reference/Roadmap.md`
- Release-Datum hinzufügen

## Schritt 10: Phase abschließen

Wenn alle Features, Docs und Website-Updates fertig sind:

1. **Alle Tests nochmal ausführen** (Unit, Integration, E2E) – alles muss grün sein
2. **PR vom Integration Branch gegen main erstellen:**
   ```bash
   gh pr create --base main --title "v0.XX – <Phase-Titel>" --body "..."
   ```
3. CI-Checks abwarten
4. PR mergen und Integration Branch löschen
