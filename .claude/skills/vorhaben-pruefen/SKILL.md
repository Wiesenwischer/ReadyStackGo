---
name: vorhaben-pruefen
description: >
  Prüft den Umsetzungs-PR eines Vorhabens unabhängig gegen Plan, Spezifikation und Regeln des Repos, mit drei Prüfern
  als Unteragenten und bis zu 7 Gegenprüfern für die Befunde "muss", schreibt einen Kommentar "Prüfung (Claude)" mit
  Befunden "muss" und "sollte" und gibt als Urteil `bestanden`, `nacharbeit` oder `freigabe` zurück. Ändert keinen
  Code. Wird vom Workflow "Vorhaben" gestartet.
argument-hint: "<PR-Nummer> [letzte-runde]"
---

# Vorhaben prüfen

Du bist der zweite, frische Blick auf einen Umsetzungs-PR. Du hast ihn nicht geschrieben und änderst nichts daran.
Argument: `$ARGUMENTS`. Umgebung: `GH_TOKEN`, `VORHABEN_OUT`.

Du prüfst nicht allein (Marcus, 27.09.2026: unabhängige Prüfer sind wichtig). Drei Prüfer als Unteragenten sehen den
PR mit je eigener Sicht, und bis zu 7 Gegenprüfer versuchen die Befunde „muss“ am Code zu widerlegen (Abschnitt 4).
Kommentar, Labels und Urteil schreibst nur du.

## 1. Lesen

- `gh pr view <pr> --json title,body,headRefName,headRefOid,files,comments,reviews` und `gh pr diff <pr>`
- den Plan (`docs/plans/<name>.md`, im PR verlinkt), die Spezifikation, den freigegebenen Entwurf, falls der Plan
  einen nennt (`docs/specs/<name>/entwurf/README.md` oder die Stelle der Spezifikation), und `CLAUDE.md` mit seinen
  verbindlichen Dokumenten
- die betroffenen Dateien im Ganzen, nicht nur die geänderten Zeilen. Checke dafür den Branch aus. Der geprüfte Commit
  ist `headRefOid`. Der Branch bleibt ausgecheckt, bis alle Unteragenten fertig sind: Sie lesen in deinem
  Arbeitsverzeichnis.
- den Stand der Checks: `gh pr checks <pr>`. Laufende Checks sind kein Befund, rote schon.

Notier dir die Pfade, die die Prüfer brauchen: Plan, Spezifikation, Entwurf, `CLAUDE.md` und seine verbindlichen
Dokumente, die geänderten Dateien. Was dir beim Lesen schon auffällt, sind deine eigenen Befunde. Sie gehen in die
Gegenprüfung wie die der Prüfer.

## 2. Maßstab

Befund **„muss“** heißt: So darf es nicht gemergt werden. Dazu gehört:

- Der PR tut nicht, was Plan und Spezifikation verlangen, oder tut mehr, als der Plan erlaubt.
- Fehler in der Logik: Randfälle, Fehlerpfade, Nebenläufigkeit, Sicherheit (Berechtigungen, Eingaben, Geheimnisse).
- Tests fehlen, prüfen nichts oder wurden gelockert. Der „vorher rote“ Test ist nicht belegt.
- Ein Check ist rot (`gh pr checks`). Laufende oder wartende Checks sind kein Befund.
- Behauptungen im PR stimmen nicht (Tests, Bilder, „nicht geprüft“ fehlt). Stichproben lokal nachmessen, soweit der
  Runner es kann: Tests laufen lassen, Build ausführen (Abschnitt 4, „Nachmessen“).
- Verstoß gegen verbindliche Regeln des Repos (Architektur, Sprache im Code, `## Patchnotes` fehlt).
- Eine neue oder geänderte Oberfläche ohne freigegebenen Entwurf im Repo (`docs/specs/<name>/entwurf/` oder die
  Stelle der Spezifikation, die der Plan nennt), oder eine sichtbare Abweichung von dessen Bildern ohne Eintrag unter
  „Abweichungen vom Plan“. Kannst du die Bilder nicht ansehen, ist der Vergleich kein Befund, sondern steht unter
  „Nicht geprüft“. Das gilt für Madieval: Dort liegen PNGs in Git LFS, und der Runner hat nur die Zeigerdateien (Text,
  beginnt mit `version https://git-lfs`).
- Ein Raster-Icon bleibt Platzhalter, obwohl sein Satz laut Icon-README auf `main` freigegeben ist, oder der PR nennt
  verbleibende Platzhalter und ihr Folge-Issue nicht.

Befund **„sollte“** heißt: besser machen, hält den Merge aber nicht auf.

Keine Geschmacksfragen als „muss“. Jeder Befund nennt Datei und Zeile, die Stelle wörtlich, was falsch ist und warum,
und was erfüllt sein müsste. Geheimnisse (Schlüssel, Tokens, Passwörter) zitierst du nie, auch nicht im Kommentar oder
im Auftrag an einen Gegenprüfer: nur Datei, Zeile und Art.

## 3. Drei Prüfer

Starte mit dem Werkzeug `Agent` drei Unteragenten vom Typ `general-purpose`, **alle in einer Nachricht**, damit sie
parallel laufen. Hat das Werkzeug den Parameter `run_in_background`, setz ihn auf `false`: Sonst läuft ein Unteragent im
Hintergrund, und `claude -p` bricht Hintergrund-Agenten nach zehn Minuten ohne Rückmeldung ab. Im Workflow fehlt der
Parameter (`CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1`), dann laufen sie ohnehin im Vordergrund. Gib kein `model` an, dann
erben sie deins: Ein schwächeres Modell prüft nicht. Warte auf alle drei Ergebnisse, bevor du weitermachst, und beende
deinen Lauf nie, solange ein Unteragent noch läuft.

Ein Unteragent sieht nichts von deinem Verlauf. Sein Auftrag muss alles enthalten, was er braucht: Nimm die Vorlage
unten, setz Repo, PR, Commit, Pfade und seine Sicht ein. Die drei Sichten:

- **A, Plan und Spezifikation:** Tut der PR, was Plan und Spezifikation verlangen, und nicht mehr? Stimmen Oberflächen
  mit dem freigegebenen Entwurf überein (README und Bilder unter `docs/specs/<name>/entwurf/` oder die Stelle der
  Spezifikation), und steht jede Abweichung begründet unter „Abweichungen vom Plan“? Gibt es eine neue Oberfläche ohne
  freigegebenen Entwurf? Raster-Icons: Platzhalter, obwohl der Satz laut Icon-README auf `main` freigegeben ist, oder
  verbleibende Platzhalter ohne Folge-Issue im PR? Bilder, die nur als LFS-Zeiger vorliegen, sind „Nicht geprüft“,
  kein Befund.
- **B, Regeln und Tests:** `CLAUDE.md` mit dem Abschnitt „Vorhaben: bauen und testen“ und den verbindlichen
  Dokumenten. Nennt der PR für jeden Befehl, den dieser Abschnitt verlangt, das Ergebnis? Sind die Checks grün
  (`gh pr checks`)? Laufende oder wartende Checks sind kein Befund, rote schon. Gibt es die Tests aus dem Plan,
  prüfen sie etwas, und ist einer davon ohne die Änderung rot und im PR benannt? Wurden Tests gelockert,
  übersprungen oder Zeitgrenzen erhöht? Stimmen die Behauptungen des PRs (Testzahlen, Commit, Bilder, „Nicht
  geprüft“) mit dem Diff? `## Patchnotes` da, Quellcode englisch, keine Geheimnisse im Diff?
- **C, Fehler im Diff:** Logik, Randfälle, Fehlerpfade, Nebenläufigkeit, Sicherheit (Berechtigungen, Eingaben),
  Ressourcen und was das Repo besonders verlangt (in Madieval etwa Replikation und Server-Autorität). Die geänderten
  Dateien ganz lesen und ihre Aufrufer, nicht nur die geänderten Zeilen.

Vorlage für einen Prüfer:

```text
Du prüfst als unabhängiger Prüfer den Pull Request #<pr> im Repo <owner/repo>, Commit <sha>. Das Repo liegt auf
diesem Commit ausgecheckt in <arbeitsverzeichnis>.

Deine Sicht: <Text der Sicht A, B oder C>

Lies:
- den PR: gh pr view <pr> --json title,body,files,comments,reviews und gh pr diff <pr>
- Plan: <pfad>. Spezifikation: <pfad>. Entwurf: <pfad oder „keiner“>.
- CLAUDE.md und <die verbindlichen Dokumente, die es nennt>
- die geänderten Dateien im Ganzen: <liste>

Frühere Runden: Gibt es im PR schon einen Kommentar „Prüfung (Claude)“, lies den jüngsten, die Antwort der
Nacharbeit danach, die Kommentare von Marcus mit @claude und die Reviews mit „Changes requested“. Ein früherer
Befund „muss“, der im Code noch besteht, ist wieder ein Befund. Was Marcus dort entschieden hat, meldest du nicht als
„muss“. Hat die Nacharbeit einen Befund begründet nicht umgesetzt, meldest du ihn nur, wenn die Begründung dem Code
nicht standhält, und zitierst sie unter „Warum“.

Maßstab: „muss“ heißt, so darf es nicht gemergt werden: <die Punkte aus „Maßstab“, die deine Sicht betreffen>.
„sollte“ heißt, besser machen, hält den Merge aber nicht auf. Keine Geschmacksfragen als „muss“.

<Block „Nur lesen“>

Antworte auf Deutsch und nur mit diesen drei Teilen:
Befunde (sonst „Keine Befunde.“), je Befund:
- Schwere: muss | sollte
  Datei: <pfad>, Zeile: <n>
  Zitat: <die Stelle wörtlich aus der Datei oder dem Diff>
  Warum: <was falsch ist, mit Bezug auf Plan, Spezifikation, Regel oder Code>
  Behebung: <was erfüllt sein müsste>
Nachmessen: lokale Befehle (Tests, Build), die ein Befund bräuchte und die du nicht ausführen darfst, je mit Zweck
(sonst „keine“)
Nicht geprüft: was du nicht lesen oder beurteilen konntest, mit Grund (sonst „nichts“)
```

**Block „Nur lesen“**, wörtlich in jeden Auftrag an einen Unteragenten:

```text
Du liest nur. Du legst im Repo und unter $VORHABEN_OUT keine Datei an und änderst keine. Du änderst den Stand des
Arbeitsverzeichnisses nicht (kein git checkout, switch, reset, stash, commit, push, kein gh pr checkout) und startest
keine Builds und Tests, denn andere Prüfer lesen gleichzeitig darin. Andere Stände liest du mit git show <ref>:<pfad>
oder git diff. Du schreibst nichts nach GitHub: kein Kommentar, kein Label, kein Review, gh api nur lesend (GET).
Geheimnisse (Schlüssel, Tokens, Passwörter) zitierst du nie, du nennst nur Datei, Zeile und Art. Jede Aussage belegst
du mit der Stelle, die du selbst gelesen hast. Ein Suchtreffer ist kein Beleg. Was du nicht nachlesen kannst, nennst
du „nicht geprüft“, statt es anzunehmen. Du startest keine eigenen Unteragenten (kein Werkzeug Agent oder Task),
du arbeitest allein.
```

## 4. Gegenprüfung

- **Zusammenführen:** die Befunde der drei Prüfer und deine eigenen. Derselbe Fehler an derselben Stelle ist ein
  Befund, mit der höchsten Schwere, die ihm jemand gab. Ein Befund, der nur laufende oder wartende Checks meldet,
  entfällt (Abschnitt 1).
- **Gegen den PR halten:** Jeden Befund hältst du gegen die Kommentare und Reviews aus Abschnitt 1. Hat Marcus die
  Stelle mit `@claude` oder einem Review entschieden, zählt der Befund nicht als „muss“: Er steht unter „Verworfen“,
  mit Link auf Marcus' Kommentar, und braucht keinen Gegenprüfer. Hat die Nacharbeit ihn begründet nicht umgesetzt,
  geht die Begründung wörtlich an den Gegenprüfer („Was der PR dazu sagt“). Gibt es einen vorigen Kommentar
  „Prüfung (Claude)“, siehst du selbst nach, ob jeder seiner Befunde „muss“ behoben, begründet oder wieder gemeldet
  ist. Einen, der nichts davon ist, nimmst du als eigenen Befund auf.
- **Gegenprüfer:** Für jeden Befund „muss“ startest du einen Unteragenten (`Agent`, `general-purpose`,
  im Vordergrund wie in Abschnitt 3, ohne `model`), alle in einer Nachricht, und wartest auf alle. Sein Auftrag ist, den
  Befund zu **widerlegen**.
- **Höchstens 10 Unteragenten je Lauf**, nach den drei Prüfern also bis zu 7 Gegenprüfer. Gibt es mehr Befunde „muss“,
  prüfst du die schwersten gegen, in dieser Reihenfolge: Sicherheit und Datenverlust, falsches Verhalten, Abweichung
  von Plan, Spezifikation oder Entwurf, fehlende Tests oder solche, die nichts prüfen, falsche Behauptungen im PR,
  übrige Regeln.
  Die übrigen bleiben „muss“, mit dem Vermerk „nicht gegengeprüft“.

Vorlage für einen Gegenprüfer:

```text
Du prüfst einen Befund aus der Prüfung des Pull Requests #<pr> im Repo <owner/repo>, Commit <sha>. Das Repo liegt
auf diesem Commit ausgecheckt in <arbeitsverzeichnis>. Versuch, den Befund zu widerlegen. Er gilt nur, wenn er dem
Code standhält.

Befund:
<Schwere, Datei, Zeile, Zitat, Warum, Behebung wörtlich>

Worauf er sich stützt: <Plan, Spezifikation, Regel, Aufrufer oder Test, mit Pfad>
Was der PR dazu sagt: <Begründung der Nacharbeit oder Kommentar von Marcus mit @claude zu dieser Stelle, wörtlich
mit Link, sonst „nichts“>

Maßstab: „muss“ heißt, so darf es nicht gemergt werden: <alle Punkte aus „Maßstab“, wörtlich>. „sollte“ heißt,
besser machen, hält den Merge aber nicht auf. Keine Geschmacksfragen als „muss“.

Prüf: Steht das Zitat an dieser Stelle? Fängt anderer Code den Fall schon ab (Aufrufer, Prüfung davor, Test)?
Verlangt Plan, Spezifikation oder Regel es wirklich so? Hat Marcus es im PR entschieden, oder trägt die Begründung,
mit der die Nacharbeit es nicht umgesetzt hat? Trifft der Befund, wenn er stimmt, einen Punkt des Maßstabs, ist er
„muss“. Nur wenn er keinen trifft, ist er „sollte“. Dann nennt deine Begründung den Punkt, der am nächsten liegt, und
warum er nicht zutrifft.

<Block „Nur lesen“>

Antworte auf Deutsch und nur so:
Urteil: bestätigt | widerlegt | sollte | unklar
Beleg: Datei:Zeile und die Stelle wörtlich, die dein Urteil trägt
Begründung: zwei bis vier Sätze
```

- **Auswerten:**
  - `bestätigt`: bleibt „muss“.
  - `widerlegt`: entfällt. Im Kommentar steht er unter „Verworfen“, mit einem Satz zum Beleg.
  - `sollte`: wird „sollte“, mit dem Vermerk „nach Gegenprüfung herabgestuft“, aber nur, wenn der Befund keinen Punkt
    des Maßstabs trifft. Trifft er einen (etwa fehlende `## Patchnotes`, ein nicht belegter „vorher roter“ Test, eine
    Oberfläche ohne freigegebenen Entwurf), liest du die Stelle selbst und entscheidest, Vermerk „selbst gegengeprüft“.
  - `unklar`, eine Antwort ohne Beleg oder eine Widerlegung, deren Beleg den Befund nicht trifft: Du liest die Stelle
    selbst und entscheidest, Vermerk „selbst gegengeprüft“.
- Befunde „sollte“ prüft niemand gegen. Ihre Stelle siehst du dir trotzdem selbst an, bevor sie in den Kommentar
  kommen.
- **Nachmessen:** Erst jetzt, wenn kein Unteragent mehr läuft, führst du aus, was die Prüfer unter „Nachmessen“
  vorschlagen, soweit es ein lokaler Befehl im Arbeitsverzeichnis ist (Tests, Build, `git show`, `git diff`), dazu
  deine eigenen Stichproben: Tests, Build, den neuen Test gegen `origin/main`, wo er rot sein muss. Nie etwas, das
  nach GitHub schreibt oder dort etwas startet (`gh workflow run`, `gh run rerun`, `gh pr …` außer lesend). Braucht
  ein Befund einen Workflow-Lauf, steht das unter „Nicht geprüft“. Danach steht der Branch wieder auf `headRefOid`.
  Die Messung gilt vor dem Urteil eines Gegenprüfers: Bestätigt sie einen Befund, bleibt er „muss“. Widerlegt sie
  ihn, steht er unter „Verworfen“. Beides bekommt den Vermerk „nachgemessen“. Ein neuer Befund aus deiner Messung ist
  belegt, braucht keinen Gegenprüfer und trägt denselben Vermerk.

## 5. Schreiben

Genau **ein** PR-Kommentar mit der Überschrift `## Prüfung (Claude)`. Er enthält:

- den geprüften Commit (Kurz-SHA)
- was du gelesen und selbst ausgeführt hast
- wer geprüft hat: „Drei Prüfer (Plan und Spezifikation, Regeln und Tests, Fehler im Diff) und <n> Gegenprüfer“, oder
  was ohne Unteragenten lief und warum
- die Gegenprüfung in einer Zeile: „<n> Befunde ‚muss‘ gegengeprüft: <x> bestätigt, <y> widerlegt, <z> zu ‚sollte‘
  herabgestuft, <w> selbst gegengeprüft. <m> nachgemessen, <v> nicht gegengeprüft.“ Ein nachgemessener Befund zählt
  nur unter „nachgemessen“, nicht zusätzlich unter dem Urteil seines Gegenprüfers oder unter „nicht gegengeprüft“.
- was du und die Prüfer nicht prüfen konnten („Nicht geprüft“)
- die Befunde „muss“ und „sollte“, jeder mit Datei und Zeile und seinem Vermerk, wo einer gilt, ein Geheimnis nie
  wörtlich, nur seine Art
- „Verworfen“: die widerlegten und die von Marcus im PR entschiedenen Befunde, je ein Satz mit Beleg oder Link. Gibt
  es keine, entfällt der Punkt.
- das Urteil

Urteil, es zählen die Befunde „muss“, die nach der Gegenprüfung stehen, auch die nicht gegengeprüften:

- **`bestanden`**, wenn es keinen Befund „muss“ gibt und alle Checks grün sind oder noch laufen.
  - Label `nacharbeit` entfernen. Kein Label `freigabe`: Der Workflow mergt den PR daraufhin selbst, sobald die
    Pflicht-Checks grün sind. Das Tor ist das Release, nicht der PR (Marcus, 17.09.2026 und 26.09.2026).
- **`nacharbeit`**, wenn es mindestens einen Befund „muss“ gibt und das Argument `letzte-runde` fehlt.
  - Label `nacharbeit` setzen.
- **`freigabe`**, wenn es in der letzten Runde (`letzte-runde`) noch Befunde „muss“ gibt oder ein Check rot ist.
  - Label `nacharbeit` entfernen, Label `freigabe` setzen.
  - Schreib die offenen Befunde oben in den Kommentar: **„Offen für Marcus“**. Marcus entscheidet, ob er sie per
    `@claude` nacharbeiten lässt oder trotzdem freigibt.

Schreib das Urteil (`bestanden`, `nacharbeit` oder `freigabe`) nach `$VORHABEN_OUT/verdict.txt`, wenn `VORHABEN_OUT`
gesetzt ist.

## Ohne Unteragenten

- Gibt es das Werkzeug `Agent` nicht (früher hieß es `Task`) oder schlägt sein Aufruf fehl, prüfst du wie vor dem
  27.09.2026 allein: alle drei Sichten selbst, und jeden Befund „muss“ liest du vor dem Urteil noch einmal an seiner
  Stelle nach.
- Liefert ein Prüfer keine brauchbare Antwort (Fehler, Abbruch, keine Liste), übernimmst du seine Sicht selbst. Einen
  zweiten Versuch startest du nicht.
- Scheitert ein Gegenprüfer, prüfst du seinen Befund selbst gegen, Vermerk „selbst gegengeprüft“.
- Der Kommentar sagt, was ohne Unteragenten lief und warum.

## Grenzen

- Kein Code, kein Commit, kein Approve, kein Merge. Du schreibst nur den Kommentar und setzt Labels.
- Keine Workflow-Läufe starten oder wiederholen (`gh workflow run`, `gh run rerun`), auch nicht auf Vorschlag eines
  Prüfers.
- Unteragenten lesen nur (Block „Nur lesen“), höchstens 10 je Lauf. Kommentar, Labels und `verdict.txt` schreibst nur
  du.
- Geheimnisse (Schlüssel, Tokens, Passwörter) gibst du nie aus: nicht im Kommentar, nicht in `verdict.txt`, nicht im
  Auftrag an einen Unteragenten. Du nennst Datei, Zeile und Art.
- Der Kommentar ist auf Deutsch.
- Keine Signatur und keine Zeile „Generated with Claude Code“ in Commits, PRs und Kommentaren.
