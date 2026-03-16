---
name: plan-feature
description: Plan a new feature, add it to the roadmap, and create a specification document
disable-model-invocation: false
argument-hint: "<feature description or idea>"
---

# Feature planen und in die Roadmap einordnen

Plane ein neues Feature für ReadyStackGo, ordne es in die Roadmap ein und erstelle eine Planungsdatei.

**Feature-Idee**: $ARGUMENTS

---

## Übersicht

Dieser Skill führt durch den gesamten Planungsprozess:
1. GitHub Project Board und bestehende Issues analysieren
2. Feature als GitHub Issue erstellen und einem Milestone zuordnen
3. Planungsdatei (Specification) erstellen
4. Dokumentation committen

---

## Schritt 1: Kontext erfassen

1. **GitHub Project Board** lesen um den aktuellen Planungsstand zu verstehen:
   ```bash
   gh project item-list 6 --owner Wiesenwischer --format json --limit 50
   ```
2. **Milestones** prüfen — welche Versionen gibt es, wie weit sind sie?
   ```bash
   gh api repos/Wiesenwischer/ReadyStackGo/milestones --jq '.[] | "\(.title) — \(.open_issues) open, \(.closed_issues) closed"'
   ```
3. **Bestehende Epic Issues** prüfen:
   ```bash
   gh issue list --label epic --state open --json number,title,milestone
   ```
4. Lies die **Projektrichtlinien** (`CLAUDE.md`) für Konventionen.
5. Prüfe ob bereits eine **Planungsdatei** für eine verwandte Phase existiert (`docs/Plans/PLAN-*.md`).
6. Prüfe die **Release History** (`docs/Reference/Release-History.md`) für bereits implementierte Features.
7. Falls `$ARGUMENTS` leer ist, frage den User welches Feature geplant werden soll.

## Schritt 2: Feature-Scope definieren

Analysiere das Feature und kläre mit dem User:

### 2a: Bestehende Architektur verstehen
- Lies relevanten bestehenden Code um zu verstehen, welche Bereiche betroffen sind
- Identifiziere bestehende Patterns die wiederverwendet werden können
- Prüfe Abhängigkeiten zu anderen Features/Modulen

### 2b: Scope klären mit AskUserQuestion
Frage den User nach:
- **Milestone**: In welchen Milestone soll das Feature? (z.B. v0.50, v1.0)
- **Scope**: Was genau soll das Feature umfassen? Was explizit nicht?
- **Abhängigkeiten**: Gibt es Voraussetzungen die zuerst erledigt werden müssen?

### 2c: Technische Analyse
- Welche Bounded Contexts sind betroffen? (Domain, Application, Infrastructure, API, WebUI)
- Braucht es neue Entities, Value Objects, Aggregates?
- Welche bestehenden Endpoints/Services werden erweitert?
- Braucht es neue UI-Seiten oder nur Erweiterungen?
- Welche Tests sind nötig? (Unit, Integration, E2E)

## Schritt 3: GitHub Issue erstellen

### Epic Issue erstellen:
```bash
gh issue create \
  --title "<Feature-Titel>" \
  --label "epic,feature" \
  --milestone "<Milestone z.B. v0.50>" \
  --body "$(cat <<'EOF'
## Goal
<Kurzbeschreibung>

## Specification
See [PLAN-<name>.md](docs/Plans/PLAN-<name>.md)

## Tasks
- [ ] Feature 1: ...
- [ ] Feature 2: ...
- [ ] Documentation & Website
EOF
)"
```

### Zum Project Board hinzufügen:
```bash
gh project item-add 6 --owner Wiesenwischer --url <ISSUE_URL>
```

## Schritt 4: Planungsdatei erstellen

Erstelle eine Specification unter `docs/Plans/PLAN-<feature-name>.md`.

**WICHTIG:** Füge am Anfang der Datei einen Kommentar mit der Issue-Nummer ein:
```markdown
<!-- GitHub Epic: #NNN -->
# Phase: <Phasen-Titel>
```

### Format:

```markdown
<!-- GitHub Epic: #NNN -->
# Phase: <Phasen-Titel>

## Ziel
<Was soll diese Phase erreichen? 2-3 Sätze.>

## Analyse

### Bestehende Architektur
<Welche bestehenden Patterns/Services sind relevant?>
<Welche Dateien werden erweitert vs. neu erstellt?>

### Betroffene Bounded Contexts
- **Domain**: <Entities, Value Objects, Events>
- **Application**: <Commands, Queries, Services>
- **Infrastructure**: <Repositories, External Services>
- **API**: <Endpoints>
- **WebUI (rsgo-generic)**: <Pages, Components>

## AMS UI Counterpart

> RSGO has two UI distributions with different design systems:
> - **rsgo-generic**: React + Tailwind CSS (reference implementation, `packages/ui-generic`)
> - **AMS UI**: ConsistentUI/Lit web components (separate repo `ReadyStackGo.Ams`)
>
> Shared logic lives in `@rsgo/core` (hooks, API calls, state). Pages/layouts must be reimplemented per distribution.
>
> **Sync-Mechanismus**: RSGO PLAN files sind die Source of Truth. Jedes Feature mit AMS-Counterpart bekommt ein entsprechendes PLAN file im AMS Repo (`C:\proj\ReadyStackGo.Ams\docs\Plans\`).

**Benötigt AMS UI eine Entsprechung?**

- [ ] **Ja** — AMS-Counterpart wird als eigenes PLAN file im AMS Repo angelegt
- [ ] **Ja (deferred)** — AMS-Counterpart wird später geplant
- [ ] **Nein** — nur `@rsgo/core` betroffen (Logik/Hooks, kein UI) → keine AMS-Arbeit nötig
- [ ] **Teilweise** — bestehende AMS-Seite muss erweitert werden

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: <Name>** – <Kurzbeschreibung>
  - Betroffene Dateien: ...
  - Pattern-Vorlage: <Referenz auf bestehende ähnliche Implementierung>
  - Abhängig von: -
- [ ] **Feature 2: <Name>** – <Kurzbeschreibung>
  - Betroffene Dateien: ...
  - Abhängig von: Feature 1
- [ ] **Dokumentation & Website** – Wiki, Public Website (DE/EN), Roadmap
- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## Test-Strategie
- **Unit Tests**: <Was wird getestet?>
- **Integration Tests**: <Was wird getestet?>
- **E2E Tests**: <Welche User-Flows werden getestet?>

## Offene Punkte
- [ ] <Frage oder Unklarheit>

## Entscheidungen
| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| <Thema> | A, B, C | - | <Noch offen / Begründung> |
```

## Schritt 5: Zusammenfassung und Bestätigung

Zeige dem User eine Zusammenfassung:

```
## Zusammenfassung

**Feature**: <Feature-Beschreibung>
**Milestone**: <z.B. v0.50>
**GitHub Issue**: #NNN
**Planungsdatei**: docs/Plans/PLAN-<name>.md
**Geschätzter Umfang**: <Anzahl Features/Schritte>

### Geänderte/Erstellte Dateien:
- docs/Plans/PLAN-<name>.md (Planungsdatei erstellt)
- GitHub Issue #NNN (Epic erstellt)
- GitHub Project Board (Issue hinzugefügt)
```

## Schritt 6: Spec-Datei aufräumen

Falls das Feature auf einer bestehenden Spec-Datei in `docs/specs/` basiert:

```bash
git rm docs/specs/<ordner>/<datei>.md
```

## Schritt 7: Committen

```bash
git add docs/Plans/PLAN-<name>.md
git rm docs/specs/...  # falls Spec gelöscht
git commit -m "Plan <Feature-Titel>"
```

**Commit-Regeln** (siehe CLAUDE.md):
- Englische Commit-Messages
- Kein Footer

---

## Checkliste

- [ ] GitHub Project Board und Milestones gelesen
- [ ] Feature-Scope mit User geklärt
- [ ] Technische Analyse durchgeführt
- [ ] GitHub Epic Issue erstellt mit Milestone
- [ ] Issue zum Project Board hinzugefügt
- [ ] Planungsdatei erstellt (`docs/Plans/PLAN-*.md`) mit `<!-- GitHub Epic: #NNN -->`
- [ ] **AMS UI Counterpart entschieden** — bei Ja: AMS PLAN file in `C:\proj\ReadyStackGo.Ams\docs\Plans\` erstellt
- [ ] Offene Punkte dokumentiert
- [ ] Änderungen committed
