---
name: vorhaben-entwerfen
description: >
  Entwirft die Oberflächen eines Vorhabens in Figma, bevor es geplant wird, und legt den Entwurf als Pull Request
  "docs(entwurf): ..." mit Bildern, Figma-Links und README unter docs/specs/<name>/entwurf/ vor. Freigegeben ist er,
  wenn Marcus den PR mergt. Läuft in einer Claude-Code-Sitzung auf Marcus' PC mit dem Figma-Connector, nicht auf
  GitHub. Kein Code, kein Plan.
argument-hint: "<Issue-Nummer> | nacharbeit <Nummer des Entwurfs-PRs>"
---

# Vorhaben entwerfen

Du entwirfst die Oberflächen **eines** Vorhabens in Figma, bevor es geplant wird. Du planst nicht und setzt nichts um.
Das Ergebnis ist ein baubarer Entwurf in der Figma-Datei des Produkts und ein PR, der ihn mit Bildern und Figma-Links
im Repo festhält. Mit dem Merge dieses PRs gibt Marcus den Entwurf frei. Den nächsten Schritt nennt der PR
(Abschnitt 5).

**Du arbeitest mit Marcus.** Du läufst in einer Claude-Code-Sitzung auf seinem PC. Lässt die Spezifikation etwas offen,
das die Gestalt bestimmt (Anordnung, Größen, Zustände, Texte, Farben, Bedienung), fragst du ihn im Chat, bevor du es
festlegst: das Problem, zwei oder drei Möglichkeiten, deine Empfehlung. Was du selbst klären kannst, fragst du nicht.

Argument: `$ARGUMENTS`. Umgebung: `gh` mit Marcus' Anmeldung, der Figma-Connector der Sitzung. `VORHABEN_OUT` ist in
der Sitzung nicht gesetzt.

**Ohne Menschen** (später headless auf einem Runner am PC, `VORHABEN_OUT` gesetzt):

- Rückfragen im Chat sind nicht möglich. Was du sonst Marcus fragen würdest und was die Gestalt bestimmt (Anordnung,
  Bezugsauflösung, Token-Wert ohne Vorgabe, Seite in Figma, Ablageort der Symbole), legst du nicht fest. Es steht mit
  deinem Vorschlag unter „Offene Fragen“ in README und PR, und du entwirfst nur, was feststeht.
- Wo dieser Skill sagt „sagst du Marcus … und hörst auf“ oder bei fehlender Figma-Datei „fragst du Marcus“,
  kommentierst du stattdessen im Issue (im Modus `nacharbeit` im PR), was fehlt und was du vorschlägst, schreibst
  nichts nach `VORHABEN_OUT` und hörst auf.
- Die PR-Nummer schreibst du nach `$VORHABEN_OUT/pr.txt`, statt sie Marcus zu sagen.

## 1. Auftrag bestimmen

- **Figma zuerst:** Ruf `whoami` des Figma-Connectors auf. Die Figma-Werkzeuge können zurückgestellt sein, dann lädst
  du sie zuerst mit `ToolSearch` (Suche „figma“), bevor du sie für fehlend hältst. Fehlen sie danach oder schlägt
  `whoami` fehl, sagst du Marcus, was fehlt (Connector nicht verbunden, Anmeldung abgelaufen), und hörst auf. Ohne
  Figma kein Entwurf und kein Ersatz im Repo.
- **Modus `<issue>`:** `gh issue view <nr> --json number,title,body,labels,comments`.
  - Im Issue steht der Pfad der Spezifikation (`docs/specs/<name>.md`), und es trägt das Label `spezifiziert`. Fehlt
    der Pfad, fehlt die Datei auf `origin/main` oder fehlt das Label, sagst du es Marcus und hörst auf. Ohne
    `spezifiziert` gilt das Vorhaben als nicht spezifiziert, auch wenn eine Datei auf `origin/main` liegt: Erst die
    Spezifikation, dann der Entwurf. `spezifiziert` setzt du nicht selbst.
  - Gibt es schon einen offenen PR auf einem Branch `entwurf/<issue>-…`, arbeitest du dort weiter wie im Modus
    `nacharbeit`.
  - Liegt der freigegebene Entwurf schon auf `origin/main`, und nennt die Icon-README des Produkts (Madieval
    `Design/icons/README.md`) für Icons, die er als Platzhalter führt, „Satz `<familie>` (Vorhaben #<n>) freigegeben
    mit dem Merge von PR #<m>“? Dann erweiterst du den Entwurf um diese Icons (Abschnitt 3, „Raster-Icons“, und
    Abschnitt 4, „Erweiterung“). Mehr änderst du nur, wenn die Spezifikation oder Marcus im Chat es verlangt.
  - Sonst: eigener Worktree von `origin/main`, nicht im Hauptverzeichnis, Branch `entwurf/<issue>-<name>`.
- **Modus `nacharbeit <pr>`:** `gh pr view <pr> --json title,body,headRefName,comments,reviews,commits` und dazu die
  Kommentare an Dateien und Bildern mit `gh api --paginate repos/{owner}/{repo}/pulls/<pr>/comments`. Die liefert
  `gh pr view` nicht, und am Entwurf kommentiert Marcus oft direkt an einem Bild oder an einer Zeile der README. Dein
  Auftrag sind die Kommentare von Marcus aus allen drei Quellen (Unterhaltung, Reviews, Dateien) seit deinem letzten
  Commit und was er dir im Chat sagt, nichts darüber hinaus. Checke den Branch des PRs in einem eigenen Worktree aus.
  Ein `@claude` in diesem PR startet auf GitHub nichts, nachgearbeitet wird nur in einer Sitzung.
- **Produkt:** `products/<produkt>.md` im Repo `Wiesenwischer/works` (`wysch`, `racr` oder `madieval`; der Kopf der
  Datei nennt ihr Repo). Die Figma-Datei steht dort im Abschnitt „Design“, Zeile „Figma-Datei“: Der Name ist der
  Linktext, der Key der Pfadteil nach `/design/` (Madieval: „Madieval Design System“, `Y64BV0HMpB6YDXquzCczy2`). Lies
  die Datei frisch: im Klon nach `git pull` (auf Marcus' PC `C:\proj\works`) oder mit
  `gh api -H "Accept: application/vnd.github.raw+json" repos/Wiesenwischer/works/contents/products/<produkt>.md`.
  Fehlt die Zeile, fragst du Marcus. Eine Datei aus dem Gedächtnis, aus einem alten Link oder aus einer anderen Quelle
  nimmst du nicht.

## 2. Verstehen, bevor du entwirfst

- Lies die Spezifikation **vollständig**, besonders „Vorgaben“ und „Oberfläche und Bilder“, dazu Bilder und Vorlagen
  daneben (`docs/specs/<name>/`). Die Spezifikation ist verbindlich.
- Lies `products/<produkt>.md` im Repo `works` (Abschnitt „Design“, bei WYSCH auch „Figma-Fallen“) und die dort
  genannten Dokumente im Produkt-Repo, bei Madieval `Design/README.md`, `Design/tokens/README.md` und
  `docs/UI_ARCHITECTURE.md`.
- **Was einen Entwurf braucht:** jede neue Oberfläche, jede neue Komponente und jede wesentliche Änderung an Verhalten
  oder Nutzerfluss. Nicht nötig ist er, wo eine freigegebene Komponente genau nach ihrer Spezifikation
  wiederverwendet wird. Schreib die Liste der Oberflächen und ihrer Zustände auf, bevor du baust. Nur Zustände, die es
  wirklich gibt: Standard, Hover, Fokus, gedrückt, gewählt, deaktiviert, lädt, leer, Fehler und was die Spezifikation
  nennt.
- **Ein Designsheet von Marcus ist die freigegebene Richtung, nicht der baubare Entwurf.** Es trägt keine
  verlässlichen Maße und zeigt nicht jeden Zustand. Nur wenn die Spezifikation ausdrücklich sagt, dass es für Maße und
  Zustände verbindlich ist, braucht es keinen Entwurf. Dann sagst du das Marcus und hörst auf.
- **Das Aussehen kommt aus dem Sheet.** Marcus erstellt es eigens dafür. Form, Proportionen, Farben, Material, Licht
  und Schein nimmst du aus dem Sheet, der Entwurf legt dazu Maße, Zustände und Tokens fest. Was die Spezifikation
  ausdrücklich anders festlegt als das Sheet, gilt. Jede andere Abweichung braucht Marcus' Ja, auch eine, die dir
  besser scheint. Vorschläge gegen das Sheet machst du nicht: Lässt sich etwas nicht wie im Sheet bauen, fragst du
  Marcus, mit Möglichkeiten, die so nah wie möglich am Sheet bleiben. Zeigt das Sheet etwas nicht, folgen deine
  Möglichkeiten seiner Formsprache.
- **Eine Entscheidung von Marcus ist kein freigegebener Entwurf.** Sie sagt, was gelten soll, nicht, wie es aussieht.
  Sie gehört in den Entwurf.
- **Figma-Anleitungen:** Vor dem ersten `use_figma` lädst du die Anleitung `figma-use` und hältst dich an sie. Ist kein
  Skill des Figma-Plugins installiert, liest du sie mit `get_figma_skill` (`skill://figma/figma-use/SKILL.md`) und
  danach die Referenzen, die sie verlangt, als `skill://figma/figma-use/references/<datei>`, mindestens
  `plugin-api-standalone.index.md` und `gotchas.md`. Zum Zusammensetzen der Rahmen kommt `figma-generate-design` dazu,
  für Komponenten und Variablen `figma-generate-library`. In jedem `use_figma` nennst du die befolgten Anleitungen in
  `skillNames`, über den Connector geladene mit `resource:` davor, etwa
  `resource:figma-use,resource:figma-generate-library`.
- **Sieh dir die Datei an, bevor du etwas änderst:** Seiten, Komponenten, Variablen und bestehende Entwürfe zum Thema.
  `get_metadata` ohne Node listet nicht jede Seite, und die Suche findet keine lokalen Variablen. Beides liest du mit
  einem lesenden `use_figma`-Skript.
- Material aus der alten Arbeitsweise (Tickets `WIE-…`, frühere Rahmen), das die Spezifikation nennt, ist Material,
  keine Vorgabe.

## 3. Entwerfen

- **Nur in der Figma-Datei des Produkts** aus `products/<produkt>.md` (Abschnitt „Design“). Keine neue Datei, keine
  Kopie der Datei.
- **Bestehendes ändern statt daneben legen:** Gibt es für eine Oberfläche oder Komponente schon einen Entwurf, änderst
  du ihn. Was Spezifikation oder Handoff als abgelöst nennen, nimmst du nicht als Grundlage.
- **Tokens und Variablen** änderst du nur so weit, wie die Spezifikation oder ein freigegebener Handoff es vorgibt.
  Braucht der Entwurf einen Wert, den niemand vorgibt, fragst du Marcus. Jede Änderung kommt mit altem Wert, neuem Wert
  und Quelle in die README.
- **Aus vorhandenen Variablen und Komponenten bauen.** Keine fest eingetragenen Farben oder Maße, wo es eine Variable
  gibt. Neue Komponenten und Varianten legst du an, wo die Spezifikation eine neue Oberfläche verlangt.
- **Komponenten** mit echten Maßen in der Bezugsauflösung, die Spezifikation oder Produktkontext nennen (fehlt sie,
  fragst du), und **jeder Zustand als Variante**. Komponenten gehören auf ihre Seite im Designsystem.
- **Rahmen je Zustand:** aus den Komponenten zusammengesetzt, in der Bildschirmgröße aus der Spezifikation, auf der
  Seite, auf der die Oberfläche in der Datei schon liegt (WYSCH zum Beispiel „Wartung“, Madieval die Seite der
  Komponente oder „HUD Screen“), nicht auf „Foundations“ oder „Cover“. Gibt es keine, legst du eine neue Seite an,
  benannt nach der Oberfläche wie die bestehenden Seiten, nicht nach Vorhaben oder Issue-Nummer. Im Zweifel fragst du
  Marcus. Die Rahmen benennst du nach der Regel des Produkts (WYSCH: `Bereich / Seite`).
  Gibt es ein Designsheet, liegt neben jedem Rahmen ein Ausschnitt davon als markierte Referenz, nicht im Rahmen.
  Der Ausschnitt dient nur zum Vergleich und ist kein Asset. Du schneidest ihn lokal aus dem Sheet im Repo (Python mit
  Pillow) und legst die Datei außerhalb des Repos ab, im Scratchpad der Sitzung. Nach Figma bringst du ihn so: Mit
  `use_figma` legst du neben dem Rahmen ein Rechteck in der Größe des Ausschnitts an, benannt `[REFERENZ] <Sheet>`, und
  gibst seine ID zurück. Dann rufst du `upload_assets` mit `nodeIds` auf dieses Rechteck und `scaleMode: FIT` auf und
  schickst die Bytes an die zurückgegebene URL:
  `curl -X POST -H "Content-Type: image/png" --data-binary @<datei> "<url>"`. Die URL gilt nur einmal und kommt
  nirgends hin. `createImageAsync` ist in `use_figma` nicht erlaubt, und Bildbytes passen nicht in ein Skript mit
  höchstens 50 000 Zeichen.
- **Echte Texte** aus der Spezifikation, keine Platzhalter.
- **Bedienung:** Maus und Tastatur, Touch oder Gamepad, je nach Produkt. Bei Madieval gehören Fokusreihenfolge und
  Gamepad-Belegung in jeden Entwurf.
- **Einfache Symbole** (Marker, Rauten, Fadenkreuze, Rollen- und Rangzeichen) zeichnest du als Vektor-Komponenten: Das
  Motiv ist weiß, der Rahmen der Komponente hat keine Füllung. Eine neue Komponente trägt sonst eine weiße Füllung, und
  der Export wird eine weiße Fläche. Exportiert werden sie in Weiß auf transparentem Grund, als PNG in vierfacher Größe
  und als SVG. Quelle bleibt die Komponente in Figma, PNG und SVG sind Exporte davon. Eingefärbt wird im Produkt. Wie
  exportiert wird, steht in Abschnitt 4.
  - **Madieval:** PNG und SVG liegen in `Design/<Bereich>/<Baustein>/master/` wie die PNG-Master in
    `Design/hud/resource-bar/master/` und die Raster-Icons aus `vorhaben-grafik` (`Design/icons/<familie>/master/`),
    nicht in `source/` (dort liegen von Hand bearbeitete SVG-Master). Daneben je eine
    `<name>.meta.yaml` mit den Feldern wie dort, aber `ticket: "#<issue>"`, `createdBy: vorhaben-entwerfen`,
    `assetType: icon` und `generator: figma`; `model`, `prompt`, `seed`, `modelLicense` und `costUsd` auf `null`;
    unter `sources` der Link auf die Figma-Komponente mit Node-ID, unter `exports` PNG und SVG; `licenseStatus: own`;
    `approvedBy` und `approvedAt` auf `null`, denn freigegeben wird mit dem Merge.
  - Nennt das Produkt keinen Ort für Symbole (bei WYSCH gibt es keinen), fragst du Marcus, bevor du einen anlegst.
- **Raster-Icons** (Skills, Status-Effekte, Gegenstände) zeichnest du nicht. Sie entstehen eigens (Skill
  `vorhaben-grafik`) und liegen danach unter `Design/icons/<familie>/master/`. Ob es sie schon gibt, sagt die
  Icon-README des Produkts auf `origin/main` (Madieval `Design/icons/README.md`):
  - **Satz nicht freigegeben:** Der Entwurf führt sie als Platzhalter in der richtigen Größe, und die README listet
    sie. Eine freigegebene Probetafel allein reicht nicht, denn sie legt die Familie fest, nicht die Icons, außer die
    Spezifikation lässt Icons der Probetafel ausdrücklich zu.
  - **Satz freigegeben** („Satz `<familie>` (Vorhaben #<n>) freigegeben mit dem Merge von PR #<m>“ mit diesem Icon):
    Du setzt jedes Icon des Satzes als Bildfüllung in seinen Platzhalter ein, Größe und Name des Platzhalters bleiben.
    Der Weg ist derselbe wie beim Ausschnitt oben: `upload_assets` mit `nodeIds` auf den Platzhalter und
    `scaleMode: FIT`, dann `curl -X POST -H "Content-Type: image/png" --data-binary @<master> "<url>"`. Quelle ist der
    Master (Madieval `Design/icons/<familie>/master/<name>.png`), nie ein Export, ein Probebild oder ein Sheet, und nie
    nachgezeichnet. In Madieval vorher `git lfs pull` im Worktree, sonst lädst du einen LFS-Zeiger hoch. Sieh dir den
    Rahmen danach mit `get_screenshot` an. Was der Satz nicht enthält, bleibt Platzhalter.
- **Nie ein Asset aus einem generierten Sheet oder Bild ausschneiden**, weder Rahmen noch Symbol noch Icon.
- Sieh dir jeden Rahmen mit `get_screenshot` an und halte ihn gegen Spezifikation und Sheet, bevor du ihn ablegst.
  Weicht er sichtbar vom Sheet ab, ohne dass Spezifikation oder Marcus es so wollen, gleichst du ihn dem Sheet an.
  Nennt das Produkt eine Abgabeschwelle (Madieval: `Design/README.md`, „Qualitaetsmassstab“), gilt sie.

## 4. Ablegen

Im Worktree, unter `docs/specs/<name>/entwurf/`:

- **Je Rahmen ein PNG** in Originalgröße: `get_screenshot` mit `maxDimension` gleich der langen Kante des Rahmens,
  oder `download_assets` als PNG. Die Antwort nennt eine kurzlebige URL. Speichere die Datei sofort mit
  `curl -L -o <datei>.png "<url>"`. Diese URLs gelten ohne Anmeldung, also nie in Commit, PR, Issue oder README. Die
  Dateinamen nennen Oberfläche und Zustand, zum Beispiel `skill-slot-abklingzeit.png`.
- **Sieh dir jedes abgelegte Bild an**, aus der Datei, nicht nur in Figma. Ist es leer, abgeschnitten oder falsch
  skaliert, exportierst du neu.
- **Symbole** legst du nicht unter `entwurf/` ab, sondern nach der Asset-Konvention aus Abschnitt 3. Je
  Symbol-Komponente zwei Aufrufe von `download_assets`: einmal mit `defaultFormat: "png"` und `defaultScale: 4`, einmal
  mit `defaultFormat: "svg"`. Beide URLs speicherst du sofort mit `curl -L -o <datei> "<url>"`. `get_screenshot` taugt
  dafür nicht, denn `maxDimension` begrenzt nur und vergrößert nie. An jeder PNG prüfst du per Python (Pillow): Die
  Maße sind das Vierfache der Komponente, es gibt Pixel mit Alpha 0, und jedes Pixel mit Alpha über 0 ist weiß. Danach
  siehst du sie dir auf dunklem Grund an. Auf Weiß oder Schachbrett ist ein weißes Motiv kaum zu sehen.
- **`README.md`** ist das Zeichen, das die Planung prüft. Die Planung kann weder Figma lesen noch, bei Madieval, die
  Bilder (Git LFS). Deshalb steht hier alles in Text:
  - Vorhaben (#<issue>), Spezifikation, Figma-Datei mit Name und Key aus `products/<produkt>.md`, Seite(n) mit
    Node-ID
  - Freigabe: „freigegeben mit dem Merge von PR #<n>“
  - je Rahmen eine Zeile: Oberfläche, Zustand, Node-ID, Link (`https://www.figma.com/design/<key>?node-id=<a>-<b>`),
    Bilddatei
  - Komponenten und Varianten mit Node-ID
  - geänderte Tokens und Variablen: alt, neu, Quelle
  - Symbole: Komponente und exportierte Dateien
  - Raster-Icons: je Icon welches, wo, welche Größe und ob Platzhalter oder eingesetzt. Bei eingesetzten Icons der
    Pfad des Masters und die Zeile „Raster-Icons: Satz aus PR #<m>“
  - Interaktion und Navigation, Eingabegeräte (bei Madieval mit Gamepad), Bildschirmgrößen
  - Festlegungen, die die Spezifikation nicht vorgibt, und Abweichungen vom Sheet, jede mit ihrer Quelle („Marcus im
    Chat, <Datum>“)
  - Hinweise für die Umsetzung. Hast du Variablen oder Komponenten des Designsystems geändert oder angelegt, steht
    hier, was im Repo danach nachzuziehen ist: bei WYSCH der Export nach `design/tokens.json` (`products/wysch.md`),
    bei Madieval `Design/tokens/README.md`, die Komponententabelle in `Design/README.md` und `UMadievalStyleLibrary`.
    Das übernehmen Plan und Umsetzung, nicht dieser PR.
  - offene Fragen
- **Node-IDs** trägst du erst ein, nachdem du sie mit `get_metadata` in Figma nachgelesen hast. Nie aus dem Gedächtnis.
- **Spezifikation:** Unter „Oberfläche und Bilder“ kommt eine Zeile dazu: „Verbindlich: Entwurf in
  `docs/specs/<name>/entwurf/` (freigegeben mit dem Merge von PR #<n>).“ Steht dort ein Hinweis, dass der Entwurf
  fehlt, passt du ihn an. Sonst änderst du an der Spezifikation nichts.
- **Erweiterung:** Liegt dort schon ein freigegebener Entwurf, erweiterst du ihn: neue Rahmen und Zeilen dazu,
  geänderte Bilder neu. Die Zeile in der Spezifikation nennt dann beide PRs („… PR #<n>, erweitert mit PR #<m>“).
- **Madieval:** PNGs laufen über Git LFS. Prüf nach dem Commit mit `git lfs ls-files`, dass die Bilder dort stehen,
  auch die Master der Symbole.
- Im Modus `nacharbeit` exportierst du jedes geänderte Bild neu und ziehst die README nach.

## 5. Vorlegen

- Commit auf Deutsch, zum Beispiel `docs(entwurf): <Titel>`, dann `git push -u origin entwurf/<issue>-<name>`.
- Neuer Entwurf: `gh pr create --base main --head entwurf/<issue>-<name> --title "docs(entwurf): <Titel>"
  --body-file <datei>`. Die Beschreibung schreibst du vorher mit dem Write-Werkzeug als UTF-8-Datei in den Scratchpad
  (Abschnitt „Grenzen“, „Texte nur aus einer Datei“):
  - nennt das Produkt eine Abgabeschwelle (Madieval: `Design/README.md`, „Qualitaetsmassstab“), als erster Punkt ihr
    Urteil: je Bauteil, ob es neben dem Referenzbild in Spielgröße standhält, ja oder nein, bei nein mit dem, was fehlt
  - „Vorhaben #<issue>“, ohne „Closes“: Der Entwurf schließt das Issue nicht
  - je Rahmen das Bild, eingebunden über den Commit statt über den Branch, und der Link auf den Figma-Knoten. Der
    Branch wird mit dem Merge gelöscht, der Commit bleibt über den PR erreichbar. `<sha>` kommt aus
    `git rev-parse HEAD` nach dem Push:
    `![<Oberfläche, Zustand>](https://github.com/<owner>/<repo>/blob/<sha>/docs/specs/<name>/entwurf/<datei>.png?raw=true)`
  - die Festlegungen und die geänderten Tokens als Liste
  - offene Fragen
  - Listet die README Raster-Icons als Platzhalter, nennst du den Schritt, der daneben läuft:
    `/vorhaben-grafik probe <issue>` in einer Sitzung am PC. Nennt die Icon-README für diese Familie schon
    „Probetafel `<familie>` … freigegeben …“, stattdessen `/vorhaben-grafik satz <issue>`. Ist ein PR auf
    `grafik/<issue>-…` offen, nennst du diesen PR. Die Planung wartet nicht darauf.
  - `## Patchnotes` mit „Keine Auswirkung für Nutzer.“
  - der Hinweis: **„Freigeben: Merge, danach <nächster Schritt>. Ändern: Kommentar im PR, danach
    `/vorhaben-entwerfen nacharbeit <PR>` in einer Sitzung am PC.“**

  Labels `vorhaben` und `freigabe`. Danach trägst du die PR-Nummer in README und Spezifikation ein, als zweiter Commit.
- Im Issue kommentierst du mit Link auf den PR (`gh issue comment <nr> --body-file <datei>`) und nennst dort und im
  Hinweis des PRs den Schritt nach dem Merge, bei Raster-Platzhaltern auch den Grafik-Schritt daneben. Er hängt am
  Stand des Vorhabens:
  - noch kein Plan: am Issue #<issue> das Label `planen` setzen
  - offener Plan-PR: im Plan-PR #<plan-pr> der Kommentar `@claude Entwurf aus PR #<dieser PR> einarbeiten`
  - Plan schon auf `main` (`docs/plans/<name>.md`): Wartet ein Umsetzungs-PR als Draft auf den Entwurf, dessen
    Nacharbeit, in einer Sitzung mit `/vorhaben-umsetzen <Issue>`, bei einem PR des Workflows mit `@claude` im
    PR. Gibt es noch keinen Umsetzungs-PR, die Umsetzung in einer Sitzung mit `/vorhaben-umsetzen <Issue>`.
    **Kein** `planen`, das startete eine zweite Planung.
  - Umsetzungs-PR offen, ohne als Draft auf den Entwurf zu warten, oder schon gemergt, oder Issue geschlossen: kein
    Schritt an diesem Vorhaben, kein `planen`, kein `@claude`. Setzt die Erweiterung Raster-Icons ein, nennt der
    Hinweis das offene Folge-Issue „Icons einbinden: <Familie>, <Oberfläche>“, das sie ins Produkt bringt
    (`gh issue list --state open --search 'in:title "Icons einbinden"' --json number,title,body`). Gibt es keins,
    legst du es an wie in `vorhaben-grafik` (Abschnitt 7, „Einbinden“). Ändert die Erweiterung mehr als die Icons,
    sagst du Marcus, dass die Änderung kein Vorhaben mehr hat, das sie umsetzt, und fragst, wie es weitergeht.

  Das Label `entwurf-fehlt` bleibt. Es entfernt, wer den freigegebenen Entwurf auf `main` findet: die Planung oder
  die Nacharbeit der Umsetzung.
- Modus `nacharbeit`: push, dann die PR-Beschreibung nachziehen (`gh pr edit <pr> --body-file <datei>`, Aufbau wie
  oben): Bilder mit dem SHA des neuen Commits, Festlegungen, geänderte Tokens und offene Fragen auf dem neuen Stand,
  denn die Beschreibung wird beim Merge die Commit-Nachricht. Danach ein PR-Kommentar mit jedem Punkt von Marcus und
  dem, was du geändert hast (`gh pr comment <pr> --body-file <datei>`).
- Danach sagst du Marcus die PR-Nummer und hörst auf.

## Grenzen

- **Kein Merge und kein Auto-Merge**, auch wenn eine Regel der Sitzung sagt, PRs gleich zu mergen oder den Auto-Merge
  einzuschalten. Der Merge ist Marcus' Freigabe des Entwurfs. Approven kann er den PR nicht, er stammt von seinem Konto.
- Im Repo änderst du nur `docs/specs/<name>/entwurf/`, die Zeile unter „Oberfläche und Bilder“ der Spezifikation und
  die Master der Symbole mit ihren `.meta.yaml`. Kein Code, keine Tokens im Code, kein Plan.
- Kein Label `planen`, keine Planung, keine Umsetzung.
- **Texte nur aus einer Datei:** PR-Beschreibungen, Issue- und PR-Kommentare und Issues schreibst du mit dem
  Write-Werkzeug als UTF-8-Datei in den Scratchpad und übergibst sie mit `--body-file <datei>`. Nie inline mit
  `--body "…"`, auch keinen kurzen Text: Die Texte enthalten Backticks, `$` und Umlaute. Bash führt Backticks als
  Befehl aus, PowerShell liest sie als Escape-Zeichen, und keiner der Aufrufe meldet einen Fehler. Nach dem Senden
  liest du den Text zurück (`gh pr view <pr> --json body`, `gh issue view <nr> --json body,comments`) und vergleichst
  ihn mit der Datei.
- In Figma arbeitest du nur in der Datei des Produkts. Variablen änderst du nur nach Vorgabe oder mit Marcus' Ja.
- Vom Aussehen des Sheets weichst du nur ab, wo die Spezifikation es festlegt oder Marcus Ja gesagt hat (Abschnitt 2).
- Nichts erfinden: keine Node-IDs, Links, Maße, Texte oder Zustände ohne Quelle.
- Der Entwurf lebt in Figma. Die Bilder im Repo halten den freigegebenen Stand fest, sie ersetzen Figma nicht. Ein
  Entwurf als Text oder Skizze im Repo ist kein Entwurf.
- Sprache: README, Commits, PR und Kommentare auf Deutsch. Namen in Figma nach der Regel des Produkts.
- Keine Signatur und keine Zeile „Generated with Claude Code“ in Commits, PRs und Kommentaren.
