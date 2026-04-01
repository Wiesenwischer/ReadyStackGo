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

## Schritt 3: Abgleich mit GitHub Issues und Board-Status

Für jeden "Vollständig"-Plan:
1. Prüfe den `<!-- GitHub Epic: #NNN -->` Kommentar in der PLAN-Datei
2. Prüfe ob das referenzierte GitHub Issue geschlossen ist:
   ```bash
   gh issue view NNN --json state --jq .state
   ```
3. Prüfe den Board-Status des Issues:
   ```bash
   gh project item-list 6 --owner Wiesenwischer --format json --limit 200 --jq ".items[] | select(.content.number == NNN) | .status"
   ```
4. Falls das Issue noch offen ist → **Warnung**: Plan ist als erledigt markiert, aber Issue ist noch offen
5. Falls das Issue offen ist aber alle Plan-Items erledigt → Frage den User ob das Issue geschlossen werden soll
6. Falls kein Issue referenziert ist: Prüfe `docs/Reference/Release-History.md` ob das Feature dort gelistet ist

## Schritt 4: Archivieren und Board-Status synchronisieren

Für alle Plans die vollständig UND deren GitHub Issue geschlossen ist (oder in Release-History gelistet):

```bash
git mv docs/Plans/PLAN-<name>.md docs/Plans/completed/PLAN-<name>.md
```

### Board-Status auf Done setzen (falls noch nicht):
```bash
PROJECT="PVT_kwHOAKdwzc4BR2Bg"
STATUS_FIELD="PVTSSF_lAHOAKdwzc4BR2Bgzg_jRfE"
DONE_ID="c631b3e2"

ITEM_ID=$(gh project item-list 6 --owner Wiesenwischer --format json --limit 200 --jq ".items[] | select(.content.number == <ISSUE_NUMBER>) | .id")
gh project item-edit --project-id $PROJECT --id "$ITEM_ID" --field-id $STATUS_FIELD --single-select-option-id $DONE_ID
```

**Nicht archivieren wenn:**
- Noch offene Checkboxen vorhanden
- GitHub Issue ist noch offen
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
- Ob es Diskrepanzen gibt (Plan erledigt aber Issue offen, oder Issue geschlossen aber Plan nicht erledigt)
