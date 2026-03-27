---
name: fix-bug
description: Fix a bug from a GitHub Issue using the Red-Green test approach
disable-model-invocation: false
argument-hint: "<issue number e.g. #123>"
---

# Bug fixen

Fixe einen bestehenden Bug aus einem GitHub Issue.

**Issue**: $ARGUMENTS

---

## Übersicht

Dieser Skill nutzt den Red-Green-Ansatz: Erst einen Test schreiben der den Bug reproduziert (Red), dann fixen bis der Test grün ist (Green).

---

## Schritt 1: Issue lesen

1. Falls `$ARGUMENTS` eine Issue-Nummer enthält (z.B. `#114` oder `114`): Issue lesen.
   ```bash
   gh issue view <nummer> --json title,body,labels
   ```
2. Falls kein Argument: Offene Bugs auflisten und User wählen lassen.
   ```bash
   gh issue list --state open --label bug --json number,title
   ```

## Schritt 2: Board-Status auf In Progress setzen (PFLICHT)

**SOFORT setzen, BEVOR irgendetwas anderes passiert.**

```bash
PROJECT="PVT_kwHOAKdwzc4BR2Bg"
STATUS_FIELD="PVTSSF_lAHOAKdwzc4BR2Bgzg_jRfE"
IN_PROGRESS_ID="9e4cff0c"

ITEM_ID=$(gh project item-list 6 --owner Wiesenwischer --format json --limit 200 --jq ".items[] | select(.content.number == <ISSUE_NUMBER>) | .id")
gh project item-edit --project-id $PROJECT --id "$ITEM_ID" --field-id $STATUS_FIELD --single-select-option-id $IN_PROGRESS_ID
```

## Schritt 3: Root Cause analysieren

1. **Bug-Beschreibung** aus dem Issue verstehen
2. **Relevanten Code lesen** — betroffene Dateien finden
3. **Reproduzieren** (lokal oder via Logs):
   - Docker Logs: `docker compose logs readystackgo --tail 50 | grep -i error`
   - Frontend: Komponente lesen, State-Flow nachvollziehen
4. **Root Cause identifizieren** und als Kommentar im Issue dokumentieren:
   ```bash
   gh issue comment <nummer> --body "Root cause: ..."
   ```

## Schritt 4: Branch erstellen

```bash
git checkout main && git pull
git checkout -b bugfix/<bug-name>
```

## Schritt 5: Test schreiben — Bug reproduzieren (Red)

**BEVOR der Fix implementiert wird**, einen Test schreiben der den Bug reproduziert.

### Unit Test (bevorzugt für Domain/Application Layer):
- Pfad: `tests/ReadyStackGo.UnitTests/`
- Der Test muss genau das Szenario abdecken, das den Bug auslöst
- Der Test soll das erwartete korrekte Verhalten prüfen (assertions)

### E2E Test (für UI-Bugs):
- Pfad: `src/ReadyStackGo.WebUi/e2e/`

**Test ausführen — er MUSS FEHLSCHLAGEN** (beweist: Bug existiert):
```bash
dotnet test tests/ReadyStackGo.UnitTests/ --filter "<test-name>"
```
Wenn der Test bereits besteht, ist der Bug nicht korrekt reproduziert — Test anpassen.

**Commit** (fehlschlagender Test):
```bash
git add tests/
git commit -m "test: reproduce bug #<nummer> — <kurzbeschreibung>"
```

## Schritt 6: Fix implementieren (Green)

Jetzt den Bug fixen — minimal und fokussiert.

### Regeln:
- **Minimal-Fix**: Nur das Problem beheben, kein Refactoring nebenher
- **Bestehende Patterns** beibehalten
- Code und Kommentare auf Englisch

### Commit:
```bash
git add <betroffene-dateien>
git commit -m "fix: <was wurde gefixt>"
```

## Schritt 7: Test erneut ausführen — muss BESTEHEN (Green)

1. **Gleichen Test nochmal laufen lassen**:
   ```bash
   dotnet test tests/ReadyStackGo.UnitTests/ --filter "<test-name>"
   ```
   Der Test MUSS jetzt bestehen. Falls nicht: Fix ist unvollständig -> zurück zu Schritt 6.

2. **Alle Tests laufen lassen** um Regressionen auszuschließen:
   ```bash
   dotnet test tests/ReadyStackGo.UnitTests/
   ```

## Schritt 8: Build prüfen

```bash
dotnet build
```

Keine Fehler, keine Warnungen (siehe CLAUDE.md).

## Schritt 9: PR erstellen

```bash
git push -u origin bugfix/<bug-name>
gh pr create \
  --base main \
  --title "fix: <Bug-Titel>" \
  --body "$(cat <<'EOF'
## Bug
<Kurzbeschreibung>

## Root Cause
<Was war die Ursache?>

## Fix
<Was wurde geändert?>

## Verification
- [x] Test reproduziert Bug (Red)
- [x] Fix implementiert
- [x] Test bestätigt Fix (Green)
- [x] Build passes (0 errors, 0 warnings)

Fixes #NNN
EOF
)"
```

Nach dem Merge wird das Issue automatisch geschlossen durch `Fixes #NNN`.

## Schritt 10: Board-Status auf Review setzen (PFLICHT)

Nach PR-Erstellung das Issue auf **Review** setzen — **NICHT auf Done**.
Der User reviewed und testet den Fix. Erst nach seiner Bestätigung wird auf Done gesetzt.

```bash
PROJECT="PVT_kwHOAKdwzc4BR2Bg"
STATUS_FIELD="PVTSSF_lAHOAKdwzc4BR2Bgzg_jRfE"
REVIEW_ID="f25a5d7c"

ITEM_ID=$(gh project item-list 6 --owner Wiesenwischer --format json --limit 200 --jq ".items[] | select(.content.number == <ISSUE_NUMBER>) | .id")
gh project item-edit --project-id $PROJECT --id "$ITEM_ID" --field-id $STATUS_FIELD --single-select-option-id $REVIEW_ID
```

**WICHTIG: Setze Bug-Issues NIEMALS selbst auf "Done".** Done wird nur gesetzt wenn der User den Fix bestätigt hat.

### Board Status IDs (ReadyStackGo Roadmap):
| Status | ID |
|---|---|
| Backlog | `56c4cbb9` |
| Todo | `af3283ef` |
| In Progress | `9e4cff0c` |
| Review | `f25a5d7c` |
| Done | `c631b3e2` |

---

## Checkliste

- [ ] Issue gelesen und Bug verstanden
- [ ] **Board auf In Progress gesetzt** (PFLICHT — sofort)
- [ ] Root Cause identifiziert und als Issue-Kommentar dokumentiert
- [ ] **Test geschrieben -> schlägt FEHL (Red)** (PFLICHT)
- [ ] Fix implementiert (minimal, fokussiert)
- [ ] **Test BESTEHT nach Fix (Green)** (PFLICHT)
- [ ] Build bestanden (0 Fehler, 0 Warnungen)
- [ ] PR erstellt mit `Fixes #NNN`
- [ ] **Board-Status auf Review gesetzt** (PFLICHT — NICHT Done)
- [ ] **Done wird NUR nach User-Bestätigung gesetzt**
