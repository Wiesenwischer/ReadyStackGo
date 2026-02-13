---
name: plan-feature
description: Plan a new feature, add it to the roadmap, and create a specification document
disable-model-invocation: true
argument-hint: "<feature description or idea>"
---

# Feature planen und in die Roadmap einordnen

Plane ein neues Feature für ReadyStackGo, ordne es in die Roadmap ein und erstelle eine Planungsdatei.

**Feature-Idee**: $ARGUMENTS

---

## Übersicht

Dieser Skill führt durch den gesamten Planungsprozess:
1. Bestehende Roadmap und Architektur analysieren
2. Feature in die Roadmap einordnen (neue oder bestehende Version)
3. Planungsdatei (Specification) erstellen
4. Dokumentation committen

---

## Schritt 1: Kontext erfassen

1. Lies die **Roadmap** (`docs/Reference/Roadmap.md`):
   - Welche Version ist die letzte released?
   - Welche Versionen sind geplant und was steht in ihnen?
   - Wo passt das neue Feature logisch hin?
2. Lies die **Projektrichtlinien** (`CLAUDE.md`) für Konventionen.
3. Prüfe ob bereits eine **Planungsdatei** für eine verwandte Phase existiert (`docs/Plans/PLAN-*.md`).
4. Prüfe ob das Feature oder etwas Ähnliches bereits in der Roadmap steht (planned oder released).
5. Falls `$ARGUMENTS` leer ist, frage den User welches Feature geplant werden soll.

## Schritt 2: Feature-Scope definieren

Analysiere das Feature und kläre mit dem User:

### 2a: Bestehende Architektur verstehen
- Lies relevanten bestehenden Code um zu verstehen, welche Bereiche betroffen sind
- Identifiziere bestehende Patterns die wiederverwendet werden können
- Prüfe Abhängigkeiten zu anderen Features/Modulen

### 2b: Scope klären mit AskUserQuestion
Frage den User nach:
- **Priorität**: Soll das Feature in die nächste geplante Version oder als eigenständige Version?
- **Scope**: Was genau soll das Feature umfassen? Was explizit nicht?
- **Abhängigkeiten**: Gibt es Voraussetzungen die zuerst erledigt werden müssen?

### 2c: Technische Analyse
- Welche Bounded Contexts sind betroffen? (Domain, Application, Infrastructure, API, WebUI)
- Braucht es neue Entities, Value Objects, Aggregates?
- Welche bestehenden Endpoints/Services werden erweitert?
- Braucht es neue UI-Seiten oder nur Erweiterungen?
- Welche Tests sind nötig? (Unit, Integration, E2E)

## Schritt 3: In die Roadmap einordnen

### Option A: Neues Feature passt in eine existierende geplante Version
- Feature als Bullet Point in die bestehende Version einfügen
- Ggf. Versionstitel anpassen wenn sich der Scope ändert

### Option B: Neues Feature wird eine eigene Version
- Nächste freie Versionsnummer verwenden (nach der letzten geplanten Version)
- Titel für die Version wählen (kurz und aussagekräftig)
- Feature-Bullet-Points hinzufügen

### Option C: Feature wird in eine existierende Version eingeschoben
- Wenn das Feature Priorität hat und vor anderen geplanten Versionen kommen soll
- Bestehende geplante Versionen entsprechend umnummerieren

### Roadmap-Format (Planned Section):
```markdown
### v0.XX – <Versions-Titel>
- <Feature/Bullet 1>
- <Feature/Bullet 2>
- <Sub-Feature oder Detail>
```

**Frage den User** mit `AskUserQuestion` welche Option gewählt werden soll, falls nicht eindeutig.

## Schritt 4: Planungsdatei erstellen

Erstelle eine Specification unter `docs/Plans/PLAN-<feature-name>.md`.

### Format:

```markdown
# Phase: <Phasen-Titel> (v0.XX)

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
- **WebUI**: <Pages, Components>

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: <Name>** – <Kurzbeschreibung>
  - Betroffene Dateien: ...
  - Pattern-Vorlage: <Referenz auf bestehende ähnliche Implementierung>
  - Abhängig von: -
- [ ] **Feature 2: <Name>** – <Kurzbeschreibung>
  - Betroffene Dateien: ...
  - Pattern-Vorlage: ...
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

### Namenskonventionen:
- Dateiname: `PLAN-<kebab-case-name>.md` (z.B. `PLAN-docker-volumes.md`)
- Phase-Titel: Kurz und beschreibend (z.B. "Docker Volumes Management")
- Feature-Namen: Imperativ (z.B. "Docker Volumes View implementieren")

## Schritt 5: Zusammenfassung und Bestätigung

Zeige dem User eine Zusammenfassung:

```
## Zusammenfassung

**Feature**: <Feature-Beschreibung>
**Version**: v0.XX – <Versions-Titel>
**Einordnung**: <Neu / In bestehende Version eingefügt / Eingeschoben>
**Planungsdatei**: docs/Plans/PLAN-<name>.md
**Geschätzter Umfang**: <Anzahl Features/Schritte>

### Geänderte Dateien:
- docs/Reference/Roadmap.md (Roadmap aktualisiert)
- docs/Plans/PLAN-<name>.md (Planungsdatei erstellt)
```

## Schritt 6: Committen

```bash
git add docs/Reference/Roadmap.md docs/Plans/PLAN-<name>.md
git commit -m "Plan v0.XX <Versions-Titel>"
```

**Commit-Regeln** (siehe CLAUDE.md):
- Englische Commit-Messages
- Kein Footer

---

## Checkliste

- [ ] Roadmap gelesen und verstanden
- [ ] Feature-Scope mit User geklärt
- [ ] Technische Analyse durchgeführt
- [ ] Roadmap aktualisiert (neue oder bestehende Version)
- [ ] Planungsdatei erstellt (`docs/Plans/PLAN-*.md`)
- [ ] Offene Punkte dokumentiert
- [ ] Änderungen committed
