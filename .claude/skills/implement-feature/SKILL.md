---
name: implement-feature
description: Implement the next feature from the roadmap or a specific feature by name
disable-model-invocation: false
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

1. **GitHub Project Board** lesen um das nächste Feature zu identifizieren:
   ```bash
   gh project item-list 6 --owner Wiesenwischer --format json --limit 50
   ```
   - Falls `$ARGUMENTS` angegeben wurde, suche dieses spezifische Feature in den Issues.
   - Falls `$ARGUMENTS` leer ist, nimm das Feature mit der **höchsten Priorität** (niedrigste Priority-Nummer) im Status "Planned".
   - Alternativ: `gh issue list --label epic --state open --json number,title,milestone`
2. Lies die **Projektrichtlinien** (`CLAUDE.md`) für Branch-Konventionen, Commit-Regeln und Test-Anforderungen.
3. **Prüfe ob bereits eine Specification/Plan-Datei existiert** in `docs/Plans/`:
   - Das Epic Issue enthält einen Link zur PLAN-Datei (z.B. "See [PLAN-xyz.md](docs/Plans/PLAN-xyz.md)")
   - **Falls vorhanden: Lies die Spec VOLLSTÄNDIG** – sie enthält Architektur-Entscheidungen, Feature-Aufteilung, betroffene Dateien, Abhängigkeiten und Test-Anforderungen
   - Die Spec ist die **primäre Quelle** für den Implementierungsplan. Erstelle keinen neuen Plan wenn eine Spec existiert – nutze sie als Basis
   - Falls keine Spec existiert: Erstelle eine neue Planungsdatei in Schritt 2
4. Lies relevante **bestehende Implementierungen** um Patterns und Architektur zu verstehen (die Spec referenziert Pattern-Vorbilder mit Dateipfaden).

## Schritt 2: Phasen-Planung & Implementierungsplan erstellen

Bevor irgendwelcher Code geschrieben wird, muss eine **Planungsdatei** für die Phase erstellt werden.

### Planungsdatei anlegen: `docs/Plans/PLAN-<phase-name>.md`

Beispiel: `docs/Plans/PLAN-init-container-ux.md`

Die Planungsdatei enthält:

```markdown
# Phase: <Phasen-Titel> (v0.XX)

## Ziel
<Kurze Beschreibung was diese Phase erreichen soll>

## Analyse
<Zusammenfassung der Codebase-Analyse: bestehende Patterns, betroffene Dateien, Abhängigkeiten>

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten und logischem Aufbau:

- [ ] **Feature 1: <Name>** – <Kurzbeschreibung>
  - Betroffene Dateien: ...
  - Abhängig von: -
- [ ] **Feature 2: <Name>** – <Kurzbeschreibung>
  - Betroffene Dateien: ...
  - Abhängig von: Feature 1
- [ ] **Feature 3: <Name>** – <Kurzbeschreibung>
  - Betroffene Dateien: ...
  - Abhängig von: -
- [ ] **Dokumentation & Website** – Wiki, Public Website, Roadmap
- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## Offene Punkte
- [ ] <Frage oder Unklarheit>
- [ ] <Technische Entscheidung die geklärt werden muss>

## Entscheidungen
| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| <Thema> | A, B, C | B | <Warum B gewählt wurde> |
```

### Fortschritts-Tracking

Status-Markierungen für Features/Schritte:
- `[ ]` – Offen (noch nicht begonnen)
- `[x]` – Erledigt (erfolgreich implementiert)
- `[-]` – Übersprungen (bewusst nicht implementiert, mit Begründung)

**Aktualisiere die Planungsdatei nach jedem abgeschlossenen Feature!**

## Schritt 3: Offene Punkte klären

Bevor du mit der Implementierung beginnst:
- Identifiziere **Unklarheiten** und **Entscheidungen** die getroffen werden müssen
- Dokumentiere diese in der Planungsdatei unter "Offene Punkte"
- Frage den User explizit nach offenen Punkten
- Kläre technische Ansätze wenn es mehrere Möglichkeiten gibt
- Dokumentiere getroffene Entscheidungen in der Tabelle "Entscheidungen"
- Stelle sicher, dass der **Scope** klar definiert ist

**Implementiere NICHTS bevor alle Fragen geklärt sind!**

## Schritt 4: Branches erstellen

**WICHTIG: Jedes Epic/Phase bekommt IMMER einen eigenen Integration Branch!**
- Ein Epic darf NIEMALS auf dem Integration Branch eines anderen Epics aufbauen
- Auch wenn mehrere Epics in der gleichen Release-Version geplant sind, hat jedes seinen **eigenen** Integration Branch
- Der Integration Branch wird IMMER von `main` abgeleitet

### Prüfe ob ein Integration Branch für **dieses** Epic existiert:
```bash
git checkout main && git pull
git branch -a | grep integration/<epic-name>
```

### Falls kein Integration Branch für dieses Epic existiert:
```bash
git checkout main
git checkout -b integration/<epic-name>
git push -u origin integration/<epic-name>
```

### Feature Branch vom Integration Branch ableiten:
```bash
git checkout integration/<epic-name>
git checkout -b feature/<feature-name>
```

## Schritt 5: Feature-Implementierung planen

Nutze den Plan Mode um für das aktuelle Feature einen detaillierten Implementierungsplan zu erstellen:
- Identifiziere alle betroffenen Dateien
- Plane die Reihenfolge der Änderungen
- Berücksichtige bestehende Patterns im Codebase
- Plane die Test-Strategie

## Schritt 6: Tests schreiben

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

## Schritt 7: Feature implementieren

- Implementiere das Feature gemäß dem Plan aus Schritt 4
- Halte dich an die bestehende Architektur (DDD, Clean Architecture, MediatR)
- Code und Kommentare auf Englisch
- Dokumentation auf Deutsch mit englischen Fachbegriffen
- Baue den Docker Container und teste lokal:
  ```bash
  docker compose build
  docker compose up -d
  ```

## Schritt 8: AMS UI Impact-Check

**WICHTIG: Jedes Feature das `@rsgo/core` Types, Hooks oder API-Endpunkte ändert, muss auf AMS UI Auswirkungen geprüft werden!**

Das AMS UI (`C:\proj\ReadyStackGo.Ams`) ist ein separates privates Repo das `@rsgo/core` via File-Link konsumiert und eigene UI-Komponenten (React + AMS Component Library) hat.

### Prüfschritte:

1. **Betroffene `@rsgo/core` Exports identifizieren:**
   - Welche Types/Interfaces wurden geändert oder erweitert? (z.B. `HealthTransitionDto`)
   - Welche Hooks wurden geändert? (z.B. `useHealthTransitionsStore`)
   - Welche API-Funktionen wurden geändert? (z.B. `getHealthTransitions`)

2. **AMS UI durchsuchen nach Nutzung dieser Exports:**
   ```bash
   cd C:\proj\ReadyStackGo.Ams
   grep -r "HealthTransitionDto\|useHealthTransitionsStore\|getHealthTransitions" packages/
   ```

3. **Falls AMS UI betroffen ist:**
   - Entsprechende AMS-Komponenten identifizieren (z.B. `packages/ui-ams/src/components/health/`)
   - AMS-Anpassungen als **separaten Task** dokumentieren
   - Den User fragen ob die AMS-Anpassung jetzt oder als Follow-up erfolgen soll

4. **Falls AMS UI NICHT betroffen ist:**
   - Im PR dokumentieren: "AMS UI: nicht betroffen (keine Nutzung von [geänderte Exports])"

### Typische Bereiche mit AMS UI Overlap:
- Health-Komponenten (`packages/ui-ams/src/components/health/`)
- Dashboard-Widgets (`packages/ui-ams/src/components/dashboard/`)
- Deployment-Detail-Seiten (`packages/ui-ams/src/pages/`)
- Hooks (`packages/ui-ams/src/hooks/`)

**Diesen Schritt NIEMALS überspringen!** Vergessene AMS-Anpassungen führen zu veralteten oder inkompatiblen UI-Varianten.

## Schritt 9: Verifizierung

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

## Schritt 10: Feature-PR erstellen

1. Alle Änderungen committen (kurze, prägnante Commit-Messages, KEIN Footer)
2. Branch pushen
3. PR erstellen **gegen den Integration Branch** (nicht gegen main!):
   ```bash
   gh pr create --base integration/<phase-name> --title "..." --body "..." --milestone "<Milestone>"
   ```
   - Falls das Feature ein eigenes GitHub Issue hat: `Closes #NNN` im PR Body verwenden
   - Das verknüpft den PR mit dem Issue und schließt es automatisch beim Merge
4. **KEIN Footer** in PR-Beschreibungen
5. CI-Checks abwarten
6. PR mergen und Feature Branch löschen

## Schritt 11: Planungsdatei aktualisieren

Nach jedem abgeschlossenen Feature die Planungsdatei aktualisieren:
- Feature als `[x]` markieren
- Eventuelle Erkenntnisse oder Abweichungen dokumentieren
- Übersprungene Features als `[-]` markieren mit Begründung

**Wiederhole Schritte 5-11 für jedes Feature der Phase.**

## Schritt 13: Dokumentation & Website (pro Phase)

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

### Release History aktualisieren
- Feature in `docs/Reference/Release-History.md` unter der entsprechenden Version eintragen
- Release-Datum hinzufügen

## Schritt 14: Phase abschließen

Wenn alle Features, Docs und Website-Updates fertig sind:

1. **Alle Tests nochmal ausführen** (Unit, Integration, E2E) – alles muss grün sein
2. **PR vom Integration Branch gegen main erstellen:**
   ```bash
   gh pr create --base main --title "<Phase-Titel>" --body "..." --milestone "<Milestone>"
   ```
   - `Closes #NNN` im Body für das Epic Issue → schließt das Epic automatisch
3. CI-Checks abwarten
4. PR mergen und Integration Branch löschen
5. **Milestone schließen** wenn alle zugeordneten Issues erledigt sind:
   ```bash
   gh api repos/Wiesenwischer/ReadyStackGo/milestones/<NUMBER> --method PATCH -f state=closed
   ```
   → Löst den `milestone-release` Workflow aus → Release wird automatisch veröffentlicht
