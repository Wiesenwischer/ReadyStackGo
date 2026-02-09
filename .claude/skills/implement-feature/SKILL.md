---
name: implement-feature
description: Implement the next feature from the roadmap or a specific feature by name
disable-model-invocation: true
argument-hint: "[feature-name]"
---

# Feature implementieren

Implementiere ein neues Feature für ReadyStackGo.

**Feature**: $ARGUMENTS

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

## Schritt 3: Feature-Branch erstellen

```bash
git checkout main
git pull
git checkout -b feature/<feature-name>
```

Branch-Name gemäß CLAUDE.md: `feature/<name>` ohne Versionsnummern.

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

## Schritt 8: PR erstellen

1. Alle Änderungen committen (kurze, prägnante Commit-Messages, KEIN Footer)
2. Branch pushen
3. PR erstellen mit:
   - Aussagekräftiger Beschreibung
   - Auflistung der Änderungen
   - **KEIN Footer** (kein "Generated with Claude Code" o.ä.)
4. CI-Checks abwarten
5. PR mergen und Branch löschen
