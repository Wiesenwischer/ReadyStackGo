---
name: vorhaben-umsetzen
description: >
  Setzt ein freigegebenes Vorhaben nach seinem Plan um (Modus `plan`) oder arbeitet die Befunde der Prüfung und die
  Kommentare von Marcus am Umsetzungs-PR ab (Modus `nacharbeit`). Baut, testet und öffnet den PR mit Patchnotes. Wird
  vom Workflow "Vorhaben" gestartet oder, im Hybrid-Weg, von Marcus in einer Sitzung am PC, dort mit der Nummer des
  Issues, zum Beispiel "/vorhaben-umsetzen 257". Verwenden, wenn Marcus "setz Vorhaben 257 um" oder "setz #257 um"
  sagt oder "/vorhaben-umsetzen 257" als Text schickt.
argument-hint: "<Issue-Nummer> | plan <Plan-PR> | nacharbeit <Umsetzungs-PR>"
---

# Vorhaben umsetzen

Du setzt **ein** Vorhaben um: von Plan und Spezifikation bis zum grünen PR. Im Workflow läufst du ohne Menschen, was
Marcus wissen muss, steht im PR. In einer Sitzung sagst du es ihm im Chat.

Argument: `$ARGUMENTS`. Umgebung: `GH_TOKEN` für `gh`, `VORHABEN_OUT` für Ergebnisdateien. Der Workflow hat das Repo
auf dem Stand von `origin/main` ausgecheckt.

**Hybrid-Weg** (Sitzung am PC, `VORHABEN_OUT` nicht gesetzt):

- Arbeite in einem eigenen Worktree von `origin/main`, nicht im Hauptverzeichnis. Den Worktree legst du erst an, wenn
  Abschnitt 1 einen Auftrag ergibt.
- Auftrag bestimmen, Umsetzen, Bauen und Testen und das Abgeben laufen genauso. Nur `pr.txt` entfällt.
- Nach dem PR sagst du Marcus die Nummer und hörst auf. Die Prüfung startet der Workflow.

## 1. Auftrag bestimmen

Der Workflow startet dich mit `plan <pr>` oder `nacharbeit <pr>`. Marcus startet eine Sitzung meist nur mit der Nummer
des Issues, das er auf dem Brett sieht (`257` oder `#257`), denn Plan-PRs stehen dort nicht. „Sag es Marcus“ heißt in
einer Sitzung im Chat, im Workflow als Kommentar am PR. Ist `$ARGUMENTS` leer, nimm die Nummer aus Marcus' Nachricht
(`257`, `#257`, „Vorhaben 257“, Link auf Issue oder PR). Steht keine darin, frag ihn nach der Nummer des Issues.

- **Nur eine Nummer:** Bestimme den Modus selbst und nenn Marcus zu Beginn, was du gefunden hast: Issue, Plan-PR bzw.
  Umsetzungs-PR und Modus. Issues und PRs teilen sich die Nummern eines Repos,
  `gh api "repos/{owner}/{repo}/issues/<n>" --jq '.pull_request != null'` sagt, was sie ist. Die Anführungszeichen
  braucht PowerShell, ohne sie liest es `{owner}` als Skriptblock und bricht ab.
  - **Ein PR:** `gh pr view <n> --json headRefName,state,isDraft,mergedAt` (den Branch-Namen liefert das auch, wenn
    der Branch gelöscht ist). Branch `vorhaben/*` und offen, dann Modus `nacharbeit <n>`. Branch `plan/<issue>-…`,
    dann weiter wie mit dem Issue. Sonst sag Marcus, was die Nummer ist (Branch, Stand), und hör auf. Bei
    `entwurf/<issue>-…` und `grafik/<issue>-…` nennst du dazu die Nummer des Issues aus dem Branch („gemeint ist wohl
    `/vorhaben-umsetzen <issue>`“).
  - **Ein Issue:** Ein PR gehört nur über den Anfang seines Branch-Namens dazu: `vorhaben/<issue>-` (Umsetzung),
    `plan/<issue>-` (Plan), `entwurf/<issue>-` (Entwurf). „Vorhaben #<issue>“ im Text macht keinen PR zum Plan, das
    schreiben auch Entwurfs-, Grafik- und Folge-PRs. Je Präfix dieser Befehl, auf **einer** Zeile (umbrochen endet er
    in Bash und PowerShell am Zeilenende):

    ```
    gh pr list --state all --search "head:<präfix>" --limit 100 --json number,state,isDraft,headRefName,mergedAt,closedAt --jq '[.[] | select(.headRefName | startswith("<präfix>"))]'
    ```

    `head:` sucht nach dem Anfang, der Bindestrich hält `25` von `257` fern, ohne `--limit` kämen nur 30 PRs. Dazu
    `gh issue view <issue> --json state,labels,title`. Es gilt das Erste, was passt:
    1. Genau ein Umsetzungs-PR offen: Modus `nacharbeit` mit ihm. Mehrere offen: nenn sie Marcus und hör auf.
    2. Issue geschlossen, oder ein Umsetzungs-PR gemergt oder ungemergt geschlossen: nenn Marcus PR, Stand und Datum
       und hör auf. Eine zweite Umsetzung legst du nicht an, Nachträge sind ein neues Issue oder seine Entscheidung.
    3. Plan-PR gemergt (bei mehreren der zuletzt gemergte): Modus `plan` mit ihm.
    4. Plan-PR offen: `gh pr view <plan> --json url,latestReviews,autoMergeRequest,statusCheckRollup`. Ohne Approve
       von Marcus in `latestReviews` wartet er auf sein Approve. Mit Approve ist er freigegeben, aber nicht gemergt:
       Nenn die Checks, die rot sind oder ausstehen. Mit Auto-Merge (`autoMergeRequest`) mergt GitHub, sobald die
       Pflicht-Checks grün sind; der Workflow wartet darauf nur 30 Minuten, ein neues Approve startet ihn wieder.
       Sag es Marcus mit Link und hör auf.
    5. Kein Plan-PR offen oder gemergt (einen ungemergt geschlossenen nennst du mit): Sag Marcus in einem Satz, was
       als Erstes passt, und hör auf.
       - Titel beginnt nicht mit „Vorhaben:“: kein Vorhaben, etwa ein Folge-Issue „Icons einbinden“.
       - Ohne `spezifiziert`: Die Spezifikation fehlt.
       - Mit `planen`: Die Planung läuft.
       - Mit `entwurf-fehlt`: Entwurfs-PR offen → „Entwurf #<n> wartet auf deinen Merge, danach `planen`“. Entwurfs-PR
         gemergt → „Entwurf #<n> ist gemergt, nächster Schritt `planen`“. Keiner → „erst
         `/vorhaben-entwerfen <issue>`, nach dessen Merge `planen`“. Als gemergt zählt nur ein Entwurfs-PR, dessen
         `mergedAt` nach dem letzten Setzen von `entwurf-fehlt` liegt (letzte Zeile dieses Befehls, auf einer Zeile):

         ```
         gh api "repos/{owner}/{repo}/issues/<issue>/events" --paginate --jq '.[] | select(.event == "labeled" and .label.name == "entwurf-fehlt") | .created_at'
         ```
       - Sonst: „spezifiziert, nächster Schritt `planen`“.
- **Modus `plan <pr>`:** `gh pr view <pr> --json state,headRefName,title,body,files` liefert die Plan-Datei
  (`docs/plans/<name>.md`) und das Issue (aus dem Branch `plan/<issue>-…`).
  - Ist der PR nicht gemergt, beginnt sein Branch nicht mit `plan/` oder enthalten die `files` kein `docs/plans/*.md`,
    ist er kein freigegebener Plan. Sag Marcus, welchen PR du bekommen hast, und hör auf.
  - Auch mit `plan <pr>` prüfst du zuerst das Issue des Plans wie unter „Ein Issue“, Regeln 1 und 2: Ein gemergter
    Plan besteht diese Prüfung auch dann noch, wenn sein Vorhaben längst umgesetzt ist. Greift Regel 2, nennst du PR,
    Stand und Datum und hörst auf. Greift Regel 1, arbeitest du in einer Sitzung im Modus `nacharbeit` mit dem offenen
    Umsetzungs-PR weiter, im Workflow nennst du ihn und hörst auf.
  - **Läuft schon eine Sitzung?** Bevor du den Branch anlegst, suchst du ihn lokal und auf `origin`:
    `git branch --list "vorhaben/<issue>-*"` (ein `+` davor heißt: in einem anderen Worktree ausgecheckt),
    `git worktree list` und `git ls-remote --heads origin "vorhaben/<issue>-*"`. Ist er in deinem eigenen Worktree
    ausgecheckt, arbeitest du darauf weiter. Gibt es ihn sonst irgendwo, arbeitet vermutlich schon eine Sitzung daran,
    auch wenn es noch keinen Commit und keinen PR gibt. Nenn Marcus Branch, Worktree, eigene Commits
    (`git log --oneline origin/main..<branch>`) und ob `git -C <worktree> status --short` Änderungen zeigt. Nenn den
    Weg weiter: Läuft die Sitzung dort noch, arbeitet sie weiter. Ist sie beendet, startet Marcus
    `/vorhaben-umsetzen <issue>` in jenem Worktree, dann gilt „in deinem eigenen Worktree“. Dann hör auf. Zwei
    Sitzungen an einem Vorhaben bauen doppelt, und `.uasset`s lassen sich nicht mergen.
  - Lies Plan, Spezifikation (`docs/specs/<name>.md`) und das Issue.
  - Branch `vorhaben/<issue>-<name>` von `origin/main`.
- **Modus `nacharbeit <pr>`:** `gh pr view <pr> --json state,headRefName,headRefOid` liefert Stand und Branch.
  - Ist der PR nicht offen oder beginnt sein Branch nicht mit `vorhaben/`, sag Marcus, welchen PR du bekommen hast,
    und hör auf.
  - **Läuft schon eine Sitzung?** `git worktree list` zeigt, ob der Branch des PRs (`headRefName`) in einem anderen
    Worktree ausgecheckt ist. Ist der sauber (`git -C <worktree> status --short` leer) und steht er auf dem Kopf des
    PRs (`git -C <worktree> rev-parse HEAD` gleich `gh pr view <pr> --json headRefOid --jq .headRefOid`), ist die
    frühere Sitzung fertig: Arbeite in diesem Worktree weiter und sag es Marcus. Sonst nenn Marcus Branch, Worktree,
    Änderungen und Commits, die nicht auf `origin` liegen, und hör auf.
  - Checke den Branch des PRs aus. Offen ist nur, was neuer ist als der letzte Commit des Branches, alles Ältere hat
    eine frühere Runde schon abgearbeitet. Lies:
    - den jüngsten Kommentar „Prüfung (Claude)“ und seine Befunde „muss“
    - die Reviews mit „Changes requested“
    - die Kommentare von Marcus mit `@claude`. Im Hybrid-Weg startet ein `@claude` am Umsetzungs-PR die nächste
      Prüfung: Ist das jüngste `@claude` neuer als der jüngste Kommentar „Prüfung (Claude)“, läuft sie noch. Sag
      Marcus das und hör auf. Als Auftrag zählt dort nur, was über den Anstoß hinaus im Kommentar steht.
  - Ist davon nichts offen und wartet der PR auch nicht als Draft auf einen Entwurf (nächster Punkt), sag Marcus,
    worauf er wartet (die Prüfung läuft, `@claude` für die nächste Prüfung, `freigabe`, Auto-Merge auf die Checks),
    und hör auf.
  - Trägt das Issue `entwurf-fehlt` und ist der PR ein Draft (`gh pr view <pr> --json isDraft,createdAt`), wartet er
    auf einen Entwurf („Wenn es nicht geht“). Der ist erst da, wenn ein PR auf `entwurf/<issue>-` nach dem `createdAt`
    des Drafts gemergt wurde (Suche wie oben). Ein Entwurf, der schon vorher auf `main` lag, zeigt das Fehlende nicht.
    Fehlt dieser Merge, baust du nichts: Sag Marcus „Entwurf #<n> wartet auf deinen Merge“ (offener Entwurfs-PR, mit
    Link) oder „erst `/vorhaben-entwerfen <issue>`“ und hör auf. Gibt es ihn, gehört die fehlende Oberfläche zum
    Auftrag. Du holst `origin/main` in den Branch, baust sie nach dem Entwurf (Abschnitt 2) und ergänzt „Bilder“ in
    der Beschreibung. Dann entfernst du `entwurf-fehlt` am Issue und `freigabe` am PR und setzt den PR mit
    `gh pr ready` auf bereit. In einer Sitzung startet das die Prüfung, im Workflow folgt sie ohnehin.

  Das ist dein Auftrag, nichts darüber hinaus.

## 2. Umsetzen

- `CLAUDE.md` und die dort genannten verbindlichen Dokumente gelten. Der Plan legt fest, **was** du baust. Musst du
  davon abweichen, sag es im PR unter „Abweichungen vom Plan“, mit Grund.
- **Lies den Code, statt anzunehmen** (Marcus, 27.09.2026). Bevor du baust, prüfst du jede Aussage des Plans über
  Bestehendes (Klassen, Funktionen, Felder, Tags, Assets, Konfiguration, Werte, Tests) an der Stelle, die er nennt,
  und liest den Code selbst. Stimmt eine Aussage nicht oder nur ungenau, baust du nach dem, was der Code zeigt, und
  nennst es mit Datei und Zeile unter „Abweichungen vom Plan“. Was sich nur im Editor oder im Spiel zeigt, prüfst du
  dort, bevor du dich darauf verlässt.
- Oberflächen baust du nach dem freigegebenen Entwurf, den der Plan unter „Oberflächen“ nennt (Rahmen und Bilder in
  `docs/specs/<name>/entwurf/` oder die Stelle der Spezifikation). Maßgeblich ist sein Stand auf `origin/main`: README
  und Bilder. Figma lebt weiter. Eine offene Erweiterung (`entwurf/<issue>-…`) oder der Entwurf eines anderen
  Vorhabens kann dieselben Rahmen und Komponenten schon geändert haben. Stehen dir Figma-Werkzeuge zur Verfügung, liest
  du Maße und Zustände aus den verlinkten Rahmen nur, wenn ihr Screenshot (`get_screenshot`) mit dem Bild im Repo
  übereinstimmt. Weicht ein Rahmen ab, baust du nach dem Bild und nennst die Abweichung im PR unter „Gefunden, nicht
  angefasst“. Lässt sich ein Maß oder Zustand nur aus dem abweichenden Rahmen ablesen, gilt er als nicht entworfen
  (nächster Punkt). Nennt die Spezifikation statt Bildern Figma-Rahmen mit Datum der Freigabe, liest du sie und
  schreibst unter „Nicht geprüft“, dass sich der Stand dieses Datums nicht nachprüfen lässt. In Figma änderst du nichts.
- Braucht die Umsetzung eine Oberfläche oder einen Zustand, den kein freigegebener Entwurf zeigt, baust du davon
  nichts. Kommentiere im Issue, was fehlt, setz das Label `entwurf-fehlt` und verfahre wie unter „Wenn es nicht geht“.
  Als nächsten Schritt nennst du in PR und Issue: `/vorhaben-entwerfen <issue>`, nach dessen Merge die Nacharbeit
  dieses PRs (in einer Sitzung `/vorhaben-umsetzen <Issue>`, bei einem PR des Workflows ein Kommentar mit
  `@claude`). Nicht `planen`: Der Plan liegt schon auf `main`.
- **Raster-Icons** nach dem Plan („Oberflächen“). Liegt ein Icon inzwischen freigegeben auf `origin/main` (Icon-README
  des Produkts, Madieval `Design/icons/README.md`: „Satz `<familie>` (Vorhaben #<n>) freigegeben mit dem Merge von PR
  #<m>“ mit diesem Icon), bindest du es ein, auch wo der Plan noch Platzhalter nennt. Das ist keine Abweichung vom
  Plan. In Madieval kommt der Master aus `Design/icons/<familie>/master/` als UI-Textur nach `Content/`, nicht der
  Export. Sonst bleiben die Platzhalter, und du legst vor dem PR ein Folge-Issue im Produkt-Repo an:
  - Titel `Icons einbinden: <Familie>, <Oberfläche>`, ohne das Wort „Vorhaben“, ohne Label
  - Text: „Folge von Vorhaben #<issue>“, je Platzhalter Stelle (Asset, Widget, Datei), Icon-Name und heutiger
    Platzhalter, dazu der Stand der Grafik (Probetafel und Satz: PR oder „nicht begonnen“). Als Weg:
    `/vorhaben-grafik probe|satz <issue>` in einer Sitzung am PC. Nach dem Merge des Satzes arbeitet eine Sitzung
    dieses Issue ab (Madieval am PC mit Editor), mit eigenem PR, `Closes #<Folge-Issue>`, `## Patchnotes`, gebaut und
    getestet nach `CLAUDE.md`, „Vorhaben: bauen und testen“.
  - Gibt es schon ein offenes Folge-Issue zu diesem Vorhaben und dieser Familie
    (`gh issue list --state open --search 'in:title "Icons einbinden"' --json number,title,body`), ergänzt du es,
    statt ein zweites anzulegen. Bindest du Icons ein, für die es ein Folge-Issue gibt, schließt dein PR es zusätzlich
    („Closes #<m>“).
- Wiederverwenden, was es gibt. Keine Nebenbaustellen: Was du unterwegs findest und nicht zum Plan gehört, kommt als
  Liste in den PR („Gefunden, nicht angefasst“).
- Quellcode englisch (Bezeichner, Kommentare, Logs, Testnamen). Commits in der Sprache, die die `CLAUDE.md` des Produkts für Commits und PRs vorgibt (ohne Vorgabe Deutsch; ReadyStackGo: Englisch, weil Open Source).

## 3. Bauen und testen

- Führe **alle** Befehle aus, die `CLAUDE.md` im Abschnitt „Vorhaben: bauen und testen“ nennt. Sie müssen grün sein,
  ohne neue Warnungen.
- Neue Tests nach dem Plan. Mindestens einer davon muss ohne deine Änderung rot sein. Prüf das, indem du ihn gegen
  `origin/main` laufen lässt, und nenn ihn im PR.
- Nennt der Plan E2E oder Bilder und der Runner kann sie laufen lassen (siehe `CLAUDE.md`):
  - Lass sie laufen und sieh dir jedes Bild selbst an.
  - Leg die Bilder unter `docs/plans/<name>/bilder/` ab und verlinke sie im PR.
  - Geht E2E auf diesem Runner nicht, schreib das in den PR unter „Nicht geprüft“.
- Ist etwas rot, behebst du die Ursache. Du lockerst keinen Test, überspringst keinen und erhöhst keine Zeitgrenze, um
  grün zu werden.

## 4. Abgeben

- Commit(s) in der Sprache, die die `CLAUDE.md` des Produkts für Commits und PRs vorgibt (ohne Vorgabe Deutsch; ReadyStackGo: Englisch, weil Open Source), `git push -u origin vorhaben/<issue>-<name>`.
- Modus `plan`: `gh pr create --base main --title "<type>(<bereich>): <Titel>"`, ohne Issue-Nummer im Titel (beim
  Squash hängt GitHub die PR-Nummer an). Die Beschreibung hat:
  - als erste Zeile „Closes #<issue>“ und den Link auf den Plan
  - „Umgesetzt“
  - „Abweichungen vom Plan“
  - „Tests“: Befehle mit Ergebnis und Anzahl, dazu der Test, der vorher rot war
  - „Bilder“: bei Oberflächen je Zustand das Bild aus dem Spiel oder der App neben dem Bild des Entwurfs, mit Link auf
    den Figma-Rahmen
  - „Platzhalter“: je Raster-Icon, das Platzhalter bleibt, Stelle und Icon, und das Folge-Issue (#<n>). Ohne
    Platzhalter entfällt der Punkt.
  - „Nicht geprüft“
  - „Gefunden, nicht angefasst“
  - `## Patchnotes`: Nutzersicht in der Sprache des PRs, oder „Keine Auswirkung für Nutzer.“ (englisch „No impact
    for users.“)

  Label `vorhaben`. **Kein Auto-Merge und kein Merge:** Den Auto-Merge schaltet der Workflow ein, wenn die Prüfung
  `bestanden` sagt, oder Marcus mit seinem Approve.
- Modus `nacharbeit`: push, dann ein PR-Kommentar mit jedem Befund und was du getan hast. Befunde, die du bewusst nicht
  umsetzt, bekommen eine Begründung.
- Schreib die PR-Nummer nach `$VORHABEN_OUT/pr.txt`, wenn `VORHABEN_OUT` gesetzt ist.

## Wenn es nicht geht

Fehlt etwas, das nur Marcus beschaffen kann (Zugang, Entscheidung, Datei), oder ist der Plan so nicht umsetzbar:

- Leg den PR als Entwurf an (`--draft`), mit allem, was fertig ist.
- Schreib oben in die Beschreibung, was fehlt und was du vorschlägst.
- Setz das Label `freigabe`, schreib die Nummer nach `pr.txt` und hör auf.

Nie etwas vortäuschen: Was nicht gebaut, getestet oder angesehen ist, steht unter „Nicht geprüft“.

## Grenzen

- Kein Merge, kein Auto-Merge, kein Tag, kein Release, kein Deployment.
- Keine Geheimnisse ausgeben oder committen. Lokale Testwerte (`.env` aus `.env.example`) nur für den lokalen Stack.
- Keine Signatur und keine Zeile „Generated with Claude Code“ in Commits, PRs und Kommentaren.
