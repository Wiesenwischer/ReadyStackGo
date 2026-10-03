---
name: vorhaben-planen
description: >
  Plant ein Vorhaben aus seiner Spezifikation und legt den Plan als Pull Request "Plan: ..." zur Freigabe durch Marcus
  vor. Wird vom Workflow "Vorhaben" gestartet (Label `planen` am Issue, oder `@claude` im Plan-PR für eine
  Überarbeitung). Nicht für Änderungen außerhalb von docs/plans.
argument-hint: "<Issue-Nummer oder Nummer des Plan-PRs>"
---

# Vorhaben planen

Du planst **ein** Vorhaben aus seiner Spezifikation. Du setzt nichts um. Das Ergebnis ist eine Plan-Datei in einem PR,
den Marcus freigibt oder mit `@claude` kommentiert.

**Du läufst ohne Menschen.** Niemand liest deine Ausgabe, Rückfragen im Chat sind nicht möglich. Was Marcus wissen oder
entscheiden muss, steht im Plan (Abschnitt „Entscheidungen“ und „Offene Fragen“) und im PR.

Argument: `$ARGUMENTS`. Umgebung: `GH_TOKEN` für `gh`, `VORHABEN_OUT` für Ergebnisdateien.

## 1. Auftrag bestimmen

- `gh issue view <nr> --json number,title,body,labels,comments` und `gh pr view <nr> --json ...`: Ist die Nummer ein
  **Issue**, ist das die Erstplanung. Ist sie ein **PR mit Branch `plan/...`**, ist das eine Überarbeitung. Dann lies
  die Kommentare und Reviews von Marcus seit dem letzten Commit. Die mit `@claude` sind dein Auftrag.
- Im Issue steht der Pfad der Spezifikation (`docs/specs/<name>.md`), und es trägt das Label `spezifiziert`. **Fehlt
  der Pfad, fehlt die Datei auf `origin/main` oder fehlt das Label:**
  - Schreib einen Kommentar ins Issue, was fehlt. Fehlt nur das Label, ist der nächste Schritt: `spezifiziert` setzen
    (Marcus oder die Sitzung, die die Spezifikation schreibt), danach wieder `planen`.
  - Entferne `planen` und, falls vorhanden, `spezifiziert`. Ohne `spezifiziert` gilt das Vorhaben als nicht
    spezifiziert, auch wenn eine Datei auf `origin/main` liegt. `spezifiziert` setzt du nicht selbst.
  - Schreib nichts nach `VORHABEN_OUT` und hör auf. Diese Prüfung kommt vor der des Entwurfs (nächster Punkt).
- **Oberfläche ohne freigegebenen Entwurf:** Braucht das Vorhaben eine neue oder geänderte Oberfläche (neue
  Oberfläche, neue Komponente oder wesentliche Änderung an Verhalten oder Nutzerfluss), suchst du den freigegebenen
  Entwurf **im Repo auf `origin/main`**. Figma kannst du hier nicht fragen. Wird eine freigegebene Oberfläche nur
  unverändert wiederverwendet, braucht es keinen neuen. Freigegeben ist:
  - `docs/specs/<name>/entwurf/README.md` (Skill `vorhaben-entwerfen`, freigegeben mit dem Merge seines PRs), oder
  - ein Entwurf, den die Spezifikation ausdrücklich als freigegeben und verbindlich für Maße und Zustände nennt, zum
    Beispiel Figma-Rahmen mit Node-ID und Datum der Freigabe oder ein Designsheet, das sie dafür verbindlich erklärt.
    Ein Sheet, das nur die Richtung vorgibt, reicht nicht.

  **Fehlt er bei der Erstplanung oder deckt er eine Oberfläche oder einen Zustand nicht ab:**
  - Schreib einen Kommentar ins Issue: welche Oberflächen und Zustände ohne freigegebenen Entwurf sind, und als
    nächsten Schritt „Entwurf in einer Sitzung am PC mit `/vorhaben-entwerfen <issue>`, nach dem Merge wieder
    `planen`“.
  - Setz das Label `entwurf-fehlt` und entferne `planen`.
  - Schreib nichts nach `VORHABEN_OUT` und hör auf.

  Trägt das Issue `entwurf-fehlt`, entfernst du das Label nur, wenn du den freigegebenen Entwurf gefunden hast, auch
  bei einer Überarbeitung. Hältst du bei der Erstplanung trotz des Labels keinen Entwurf für nötig, hältst du wie oben
  an (Kommentar, `planen` entfernen, nichts nach `VORHABEN_OUT`). Im Kommentar steht, warum du keinen Entwurf für nötig
  hältst, und als nächster Schritt: entweder `/vorhaben-entwerfen <issue>` in einer Sitzung, oder Marcus entfernt
  `entwurf-fehlt` und setzt wieder `planen`. Dann planst du ohne Entwurf.

  Verlangt erst eine Überarbeitung per `@claude` eine Oberfläche ohne Entwurf, schreibst du das im Plan unter „Offene
  Fragen“ und setzt `entwurf-fehlt` am Issue. Den Schritt dazu nennst du im PR-Kommentar: `/vorhaben-entwerfen <issue>`,
  nach dessen Merge ein Kommentar `@claude Entwurf aus PR #<n> einarbeiten` im Plan-PR.

  Trägt das Issue bei einer Überarbeitung `entwurf-fehlt`, weil der Plan auf einen Entwurf wartet (Offene Fragen), und
  liegt der freigegebene Entwurf inzwischen auf `origin/main`, gehört sein Einarbeiten zum Auftrag, auch wenn der
  Kommentar mit `@claude` sonst nichts sagt. Du ziehst Punkt 5 „Oberflächen“ nach dem Entwurf nach: je Oberfläche
  und Zustand Rahmen, Node-ID, Link und Bild, dazu die Raster-Icons aus seiner Platzhalterliste. Die Oberfläche nimmst
  du in die Schritte der Umsetzung und in Tests und Abnahme auf. Die offene Frage dazu streichst du und entfernst
  `entwurf-fehlt` am Issue. Im PR-Kommentar nennst du den Entwurfs-PR, den du eingearbeitet hast.
- Das Brett bewegst du nicht. Nach einem Abbruch setzt der Workflow die Spalte nach den Labels des Issues.

## 2. Verstehen, bevor du planst

- Lies die Spezifikation **vollständig**, dazu die Bilder und Vorlagen daneben. Die Spezifikation ist verbindlich.
  Weichst du ab, sagst du es im Plan unter „Entscheidungen“.
- Lies `CLAUDE.md` und die dort genannten verbindlichen Dokumente (Architektur, Entwicklung). Gibt es
  `graphify-out/graph.json` oder das Kommando `graphify`, nutze `graphify query` für den Überblick.
- Such im Code nach dem, was es schon gibt: Muster, Komponenten, Hilfsfunktionen. Plane Wiederverwendung statt neuem
  Code.
- **Lies den Code, statt anzunehmen** (Marcus, 27.09.2026). Jede Aussage des Plans über Bestehendes (Klassen,
  Funktionen, Felder, Tags, Assets, Konfiguration, Werte, Tests und ihr Verhalten) belegst du, indem du die Stelle
  liest, und nennst sie mit Datei und Zeile. Ein Suchtreffer, ein Graph-Treffer oder ein Dateiname ist kein Beleg, erst
  der gelesene Code. Was du nicht nachlesen kannst (etwa Binärdateien wie `.uasset`, Verhalten, das sich nur im Editor
  oder im Spiel zeigt), steht als „nicht nachgelesen“ mit deiner Annahme unter „Risiken“, nie als Tatsache.
- Material aus der alten Arbeitsweise (Tickets `WIE-…`, offene PRs), das die Spezifikation nennt, liest du mit `gh pr
  view`/`gh pr diff`. Es ist Material, keine Vorgabe.

## 3. Plan schreiben

Datei `docs/plans/<name>.md`, wobei `<name>` der Name der Spezifikation ist. Auf Deutsch, knapp, prüfbar:

1. **Kontext:** warum, was die Spezifikation will, ein Absatz.
2. **Ergebnis aus Nutzersicht:** was man danach sieht oder kann.
3. **Umfang und Nicht-Umfang.**
4. **Entscheidungen, die in diesem Plan stecken:** jede Festlegung, die die Spezifikation nicht vorgibt (Texte,
   Zahlen, Verhalten, Abhängigkeiten, Kosten), als eigener Punkt mit Begründung. Das ist der Teil, den Marcus mit
   seiner Freigabe entscheidet. Nichts verstecken.
5. **Oberflächen:** Nenn den freigegebenen Entwurf (`docs/specs/<name>/entwurf/README.md` oder die Stelle der
   Spezifikation), je Oberfläche und Zustand den Rahmen mit Node-ID, Link und Bild, und was der Plan daraus baut. Du
   entwirfst nichts und änderst keinen Entwurf. Weicht der Plan vom Entwurf ab, steht das unter „Entscheidungen“. Ohne
   Oberfläche: „Keine neue oder geänderte Oberfläche.“
   **Raster-Icons** (Skills, Status-Effekte, Gegenstände) führst du einzeln auf, aus der Platzhalterliste der
   Entwurfs-README und der Spezifikation: Icon, Stelle, Größe, heutiger Platzhalter. Was freigegeben ist, steht in der
   Icon-README des Produkts auf `origin/main` (Madieval `Design/icons/README.md`: „Satz `<familie>` (Vorhaben #<n>)
   freigegeben mit dem Merge von PR #<m>“; Icons der Probetafel nur, wo die Spezifikation sie zulässt). Freigegebene
   Icons baut der Plan ein. Für die übrigen schreibt er: Liegt der Satz bei der Umsetzung auf `main`, bindet sie ihn
   ein, sonst bleibt der Platzhalter, und die Umsetzung legt das Folge-Issue „Icons einbinden: …“ an. Fehlende Icons
   sind kein Grund anzuhalten und keine offene Frage.
6. **Schritte der Umsetzung:** Dateien und Stellen, in der Reihenfolge, in der du sie bauen würdest.
7. **Tests und Abnahme:** welche Tests neu dazukommen, welcher davon ohne die Änderung rot ist, E2E und Bilder, die der
   Umsetzungs-PR zeigen muss.
8. **Wo umgesetzt wird:** `CLAUDE.md` nennt im Abschnitt „Vorhaben: bauen und testen“, was auf dem GitHub-Runner geht,
   und die Umsetzungsrollen für den Rest (Label `umsetzung:<rolle>`, z. B. die echte Launcher-App). Braucht die
   Abnahme etwas, das nur eine solche Rolle kann, nenn die Rolle und den Grund. Sonst: „GitHub-Runner“.
9. **Risiken.**
10. **Offene Fragen:** nur echte, die Marcus beantworten muss. Keine, die du selbst klären kannst.

## 4. Aussagen gegenprüfen

Bevor du vorlegst, lesen Faktenprüfer als Unteragenten jede Aussage des Plans über Bestehendes nach (Marcus,
27.09.2026: unabhängige Prüfer sind wichtig). Du hast sie nach Abschnitt 2 mit Datei und Zeile belegt, hier liest sie
ein zweiter Blick nach, der den Plan nicht geschrieben hat.

- **Sammeln:** Geh den Plan durch und schreib jede Aussage über Bestehendes als Zeile heraus: Nummer, Aussage wie im
  Plan, Beleg (Datei:Zeile), Abschnitt des Plans. Eine Aussage ist eine prüfbare Behauptung: dass es etwas gibt, wo es
  steht, welchen Wert es hat, was es tut oder was ein Test prüft. Fehlen Datei und Zeile, liest du die Stelle erst
  selbst nach (Abschnitt 2). Nur was sich nicht nachlesen lässt, kommt nach „Risiken“, als „nicht nachgelesen:
  <Aussage>. Annahme: …“. Bei einer Überarbeitung sammelst du nur Aussagen, die neu sind oder sich geändert haben.
  Gibt es keine, startest du keine Faktenprüfer, und die Zeile für den PR (Abschnitt 5) lautet „Aussagen über den
  Code: keine neuen, nichts gegengeprüft“.
- **Aufteilen:** in bis zu 4 Gruppen, nach Dateien oder Bereichen, damit jeder Prüfer wenige Dateien liest. Bis etwa
  8 Aussagen prüft ein einziger.
- **Faktenprüfer:** Für jede Gruppe startest du mit dem Werkzeug `Agent` einen Unteragenten vom Typ `general-purpose`,
  **alle in einer Nachricht**, damit sie parallel laufen. Hat das Werkzeug den Parameter `run_in_background`, setz ihn
  auf `false`, sonst liefe er im Hintergrund, und `claude -p` bricht Hintergrund-Agenten nach zehn Minuten ohne
  Rückmeldung ab. Im Workflow fehlt der Parameter, dort laufen sie ohnehin im Vordergrund. Gib kein `model` an, dann
  erben sie deins. Warte auf alle, bevor du weitermachst, und beende deinen Lauf nie, solange einer noch läuft. Ein
  Unteragent sieht nichts von deinem Verlauf, sein Auftrag muss alles enthalten:

  ```text
  Du prüfst Aussagen eines Plans über bestehenden Code im Repo <owner/repo>. Es liegt in <arbeitsverzeichnis>, auf
  dem Stand, gegen den geplant wird (Commit <sha>).

  Lies zu jeder Aussage die genannte Stelle und so viel darum herum, wie du für dein Urteil brauchst. Steht es an
  anderer Stelle, such es und nenn die richtige. Ein Suchtreffer, ein Graph-Treffer oder ein Dateiname ist kein
  Beleg, erst der gelesene Code.

  Du liest nur. Du legst im Repo und unter $VORHABEN_OUT keine Datei an und änderst keine. Du änderst den Stand des
  Arbeitsverzeichnisses nicht (kein git checkout, switch, reset, stash, commit, push, kein gh pr checkout) und startest
  keine Builds und Tests, denn andere Faktenprüfer lesen gleichzeitig darin. Was ein Test prüft, liest du an seinem
  Code nach. Andere Stände liest du mit git show <ref>:<pfad> oder git diff. Du schreibst nichts nach GitHub: kein
  Kommentar, kein Label, gh api nur lesend (GET). Geheimnisse (Schlüssel, Tokens, Passwörter) zitierst du nie. Du
  startest keine eigenen Unteragenten (kein Werkzeug Agent oder Task), du arbeitest allein.

  „nicht nachlesbar“ ist, was sich im Text der Dateien nicht zeigt: Binärdateien wie .uasset, Git-LFS-Zeiger (Text,
  beginnt mit „version https://git-lfs“), Verhalten, das sich nur im Editor, im Spiel oder zur Laufzeit zeigt.

  Aussagen:
  <Nummer | Aussage | Beleg Datei:Zeile>

  Antworte auf Deutsch, je Aussage ein Eintrag:
  <Nummer>: stimmt | falsch | ungenau | nicht nachlesbar
  Beleg: Datei:Zeile und die Stelle wörtlich
  Richtig ist: <bei falsch oder ungenau, was der Code zeigt>
  ```

- **Auswerten:**
  - `stimmt`: bleibt.
  - `falsch` oder `ungenau`: Du liest die Stelle selbst und korrigierst den Plan: Aussage, Datei und Zeile und was
    daran hängt (Entscheidungen, Schritte, Tests).
  - `nicht nachlesbar`: Die Aussage kommt nach „Risiken“, als „nicht nachgelesen: <Aussage>. Annahme: …“, nie als
    Tatsache.
- **Nachprüfen:** Bringt eine Korrektur neue Aussagen über Bestehendes, prüft sie ein weiterer Faktenprüfer mit
  derselben Vorlage. **Höchstens 6 Unteragenten je Planungslauf**, etwa 4 Faktenprüfer und 2 Nachprüfungen. Was
  darüber hinausgeht, liest du selbst nach.
- **Ohne Unteragenten:** Gibt es das Werkzeug `Agent` nicht (früher hieß es `Task`), schlägt sein Aufruf fehl oder
  liefert ein Faktenprüfer keine brauchbare Antwort, liest du die Aussagen der Gruppe selbst nach, jede an ihrer
  Stelle.
- **Ergebnis:** eine Zeile für den PR (Abschnitt 5): „Aussagen über den Code: <n> gegengeprüft von <k> Unteragenten,
  <x> korrigiert, <y> nach ‚Risiken‘ (nicht nachgelesen)“. Lief etwas ohne Unteragenten, steht dort, was und warum.

## 5. Vorlegen

- Branch `plan/<issue>-<name>` von `origin/main`. Bei einer Überarbeitung auf den bestehenden Branch. Commit auf
  Deutsch, zum Beispiel `docs(plan): <Titel>`.
- Erstplanung: `gh pr create --base main --head plan/<issue>-<name> --title "Plan: <Titel>"` mit dieser Beschreibung:
  - „Vorhaben #<issue>“
  - fünf Zeilen Zusammenfassung
  - die Entscheidungen als Liste
  - die Zeile zur Gegenprüfung aus Abschnitt 4
  - `## Patchnotes` mit „Keine Auswirkung für Nutzer.“
  - der Hinweis: **„Freigeben: Approve. Ändern: Kommentar mit @claude.“**

  Labels `vorhaben` und `freigabe`. Nennt der Plan eine Umsetzungsrolle (Punkt 8), dazu ihr Label `umsetzung:<rolle>`.
  Gibt es das Label im Repo nicht, nimm es trotzdem nicht neu auf, sondern schreib es unter „Offene Fragen“.
- Überarbeitung: push auf den Branch, dann ein PR-Kommentar, was sich geändert hat, mit der Zeile zur Gegenprüfung.
  Das Label `freigabe` bleibt. Ändert sich die Umsetzungsrolle, zieh das Label `umsetzung:<rolle>` nach.
- Im Issue kommentierst du mit Link auf den PR, entfernst `planen` und setzt `vorhaben`.
- Schreib die PR-Nummer nach `$VORHABEN_OUT/pr.txt`.

## Grenzen

- Du änderst nur `docs/plans/`. Kein Code, keine Spezifikation.
- Unteragenten lesen nur (kein Anlegen, kein Wechsel des Stands, keine Builds und Tests, nichts nach GitHub),
  höchstens 6 je Lauf. Plan, Commits, PR und Kommentare schreibst nur du.
- Kein Entwurf in Figma, auch wenn Figma-Werkzeuge da sind.
- Du mergst nichts und gibst nichts frei.
- Sprache: Plan, Commits und PR auf Deutsch. Code-Beispiele im Plan englisch benannt.
- Keine Signatur und keine Zeile „Generated with Claude Code“ in Commits, PRs und Kommentaren.
