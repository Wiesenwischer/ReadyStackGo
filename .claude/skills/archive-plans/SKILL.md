---
name: archive-plans
description: Check which feature plans are fully implemented and archive completed ones
disable-model-invocation: true
argument-hint: ""
---

# Erledigte Plans archivieren

Prüfe welche Feature-Plans vollständig umgesetzt sind und verschiebe sie ins Archiv.

---

## Schritt 1: Alle Plan-Dateien lesen

Lies alle `PLAN-*.md` Dateien in `docs/Plans/` (NICHT in `docs/Plans/completed/`).

Für jede Datei:
1. Zähle alle Checkboxen: `- [ ]` (offen) und `- [x]` (erledigt)
2. Bestimme den Status:
   - **Vollständig**: Alle Items `[x]` — bereit zum Archivieren
   - **In Arbeit**: Mindestens ein `[x]`, aber auch `[ ]` vorhanden
   - **Offen**: Kein `[x]` vorhanden — noch nicht begonnen

## Schritt 2: Status-Übersicht anzeigen

Zeige dem User eine Tabelle:

```
| Plan | Status | Erledigt / Gesamt | Aktion |
|------|--------|-------------------|--------|
| PLAN-xyz.md | Vollständig | 8/8 | → Archivieren |
| PLAN-abc.md | In Arbeit | 3/7 | Bleibt |
| PLAN-def.md | Offen | 0/5 | Bleibt |
```

## Schritt 3: Abgleich mit Roadmap

Für jeden "Vollständig"-Plan:
1. Lies `docs/Reference/Roadmap.md`
2. Prüfe ob die zugehörige Version unter "Released" steht (mit Datum)
3. Falls die Version noch unter "Planned" steht → **Warnung**: Plan ist als erledigt markiert, aber Version ist noch nicht released

## Schritt 4: Archivieren

Für alle Plans die vollständig UND in der Roadmap als released markiert sind:

```bash
git mv docs/Plans/PLAN-<name>.md docs/Plans/completed/PLAN-<name>.md
```

**Nicht archivieren wenn:**
- Noch offene Checkboxen vorhanden
- Version steht nicht unter "Released" in der Roadmap
- User lehnt die Archivierung ab

## Schritt 5: Commit

Falls Plans verschoben wurden:

```bash
git add docs/Plans/ docs/Plans/completed/
git commit -m "Archive completed plans: <liste der verschobenen Plans>"
```

**Commit-Regeln** (siehe CLAUDE.md):
- Englische Commit-Messages
- Kein Footer

## Schritt 6: Zusammenfassung

Zeige dem User:
- Wie viele Plans archiviert wurden
- Wie viele Plans noch offen/in Arbeit sind
- Ob es Diskrepanzen gibt (erledigt aber nicht released, oder released aber Plan nicht erledigt)
