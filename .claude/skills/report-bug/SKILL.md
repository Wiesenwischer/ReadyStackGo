---
name: report-bug
description: Report a bug as a GitHub Issue with description, reproduction steps, and initial analysis
disable-model-invocation: false
argument-hint: "<bug description>"
---

# Bug erfassen

Erfasse einen Bug als GitHub Issue mit Beschreibung, Reproduktionsschritten und erster Analyse.

**Bug**: $ARGUMENTS

---

## Übersicht

Dieser Skill erstellt ein GitHub Issue für einen Bug — **ohne ihn zu fixen**. Zum Fixen nutze `/fix-bug <issue-nummer>`.

---

## Schritt 1: Bug verstehen

1. Falls `$ARGUMENTS` eine Beschreibung enthält: Bug analysieren.
2. Falls leer: User nach dem Bug fragen — was passiert, was sollte passieren, wo tritt es auf.
3. **Kontext sammeln:**
   - Welche Seite / welches Feature ist betroffen?
   - Reproduktionsschritte (wenn bekannt)
   - Fehlermeldung (Console, Server-Logs, UI)

## Schritt 2: Schnelle Analyse

1. **Relevanten Code lesen** — den betroffenen Bereich finden
2. **Docker Logs prüfen** (falls Server-Bug):
   ```bash
   docker compose logs readystackgo --tail 50 2>/dev/null | grep -i error
   ```
3. **Root Cause Vermutung** formulieren (muss nicht 100% sicher sein)

## Schritt 3: GitHub Issue erstellen (PFLICHT)

**Ein Bug MUSS als GitHub Issue erfasst werden. Kein Bug ohne Issue.**

```bash
gh issue create \
  --title "bug: <Kurzbeschreibung>" \
  --label "bug" \
  --body "$(cat <<'EOF'
## Bug Description
<Was passiert?>

## Expected Behavior
<Was sollte passieren?>

## Steps to Reproduce
1. ...
2. ...

## Environment
- [ ] Production
- [ ] Local Dev (Docker)
- [ ] Both

## Analysis
<Erste Analyse: welcher Code/welche Komponente ist wahrscheinlich betroffen?>
<Vermutete Ursache?>
EOF
)"
```

## Schritt 4: Zum Project Board hinzufügen (PFLICHT)

**Jedes Bug-Issue MUSS auf dem Project Board sichtbar sein.**

```bash
# Issue zum Board hinzufügen
gh project item-add 6 --owner Wiesenwischer --url <ISSUE_URL>

# Status auf Backlog setzen
PROJECT="PVT_kwHOAKdwzc4BR2Bg"
STATUS_FIELD="PVTSSF_lAHOAKdwzc4BR2Bgzg_jRfE"
BACKLOG_ID="56c4cbb9"

ITEM_ID=$(gh project item-list 6 --owner Wiesenwischer --format json --limit 200 --jq ".items[] | select(.content.number == <ISSUE_NUMBER>) | .id")
gh project item-edit --project-id $PROJECT --id "$ITEM_ID" --field-id $STATUS_FIELD --single-select-option-id $BACKLOG_ID
```

### Board Status IDs (ReadyStackGo Roadmap):
| Status | ID |
|---|---|
| Backlog | `56c4cbb9` |
| Todo | `af3283ef` |
| In Progress | `9e4cff0c` |
| Review | `f25a5d7c` |
| Done | `c631b3e2` |

## Schritt 5: Zusammenfassung

Zeige dem User:
- Issue-Nummer und Link
- Kurze Zusammenfassung des Bugs
- Vermutete Ursache
- Hinweis: `/fix-bug #NNN` zum Fixen

---

## Checkliste

**Alle Punkte müssen abgehakt sein. Bei Fehler -> nochmal versuchen, nicht weitermachen.**

- [ ] Bug verstanden
- [ ] Erste Analyse durchgeführt
- [ ] **GitHub Issue erstellt** mit Label `bug` (PFLICHT — verifiziere dass Issue-URL zurückkommt)
- [ ] **Issue zum Project Board hinzugefügt** (PFLICHT — `gh project item-add`)
- [ ] **Board-Status gesetzt** (Backlog)
- [ ] User über Issue-Nummer informiert
