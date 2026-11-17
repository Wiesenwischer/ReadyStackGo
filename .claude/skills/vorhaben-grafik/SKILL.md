---
name: vorhaben-grafik
description: >
  Erzeugt die Raster-Icons eines Vorhabens (Skills, Status-Effekte, Gegenstände) über fal.ai: erst eine Probetafel mit
  3 bis 5 Icons, nach deren Freigabe den ganzen Satz, jeweils als Pull Request "docs(grafik): ..." mit Mastern,
  Metadaten, Exporten und Probebildern. Freigegeben ist ein PR, wenn Marcus ihn mergt. Läuft nur in einer
  Claude-Code-Sitzung auf Marcus' PC mit dem Schlüssel FAL_KEY, nicht auf GitHub. Kein Code, kein Plan.
argument-hint: "probe <Issue-Nummer> | satz <Issue-Nummer> | nacharbeit <Nummer des Grafik-PRs>"
---

# Grafik für ein Vorhaben

Du erzeugst die Raster-Icons **eines** Vorhabens über fal.ai: Skill-Icons, Status-Effekte, Gegenstände. Du entwirfst
keine Oberfläche, planst nicht und setzt nichts um. Das Ergebnis sind Master, Metadaten und Exporte im Produkt-Repo und
ein PR, der sie mit Probebildern vorlegt. Mit dem Merge gibt Marcus sie frei.

Zwei Stufen, jede mit eigenem PR (Entscheidung Marcus, 27.09.2026, works#118):

- **`probe <issue>`:** eine Probetafel mit 3 bis 5 Icons. Sie legt die Stilfamilie fest, nicht die einzelnen Icons.
- **`satz <issue>`:** der ganze Satz, erst wenn die Probetafel gemergt ist, mit ihren Icons als Stilvorlage.
- **`nacharbeit <pr>`:** die Kommentare von Marcus an einem offenen Grafik-PR.

Freigegeben wird je **Familie**: `skills` (Skill), `status` (Status-Effekt), `items` (Gegenstand). Eine Familie kann
mehreren Vorhaben dienen, etwa die Status-Effekte der Zielanzeige und der Gruppenrahmen.

Planung und Entwurf warten nicht auf die Icons. Der Entwurf führt sie als Platzhalter (`vorhaben-entwerfen`), bis ihr
Satz auf `main` liegt. Eingebunden werden sie danach von der Umsetzung oder über ein Folge-Issue (Abschnitt 7).

**Du arbeitest mit Marcus.** Du läufst in einer Claude-Code-Sitzung auf seinem PC, der fal.ai-Zugang liegt dort.
Bestimmt etwas die Gestalt der Icons, das weder Spezifikation noch Entwurf noch Probetafel vorgeben, fragst du ihn im
Chat, bevor du Geld ausgibst: das Problem, zwei oder drei Möglichkeiten, deine Empfehlung. Was du selbst klären kannst,
fragst du nicht.

Argument: `$ARGUMENTS`. Umgebung: `gh` mit Marcus' Anmeldung, Python 3 mit Pillow und numpy, das Hilfsskript
`fal_run.py` neben dieser Datei. Im Produkt-Repo rufst du es aus dem Worktree mit
`python .claude/skills/vorhaben-grafik/fal_run.py <befehl>` auf, im Folgenden kurz `fal_run.py`. Ohne Menschen läuft
dieser Skill nicht: Ist `VORHABEN_OUT` gesetzt, kommentierst du im Issue (`--body-file`, siehe „Grenzen“), dass die
Grafik eine Sitzung mit Marcus am PC braucht, und hörst auf.

## 1. Auftrag bestimmen

- **Modus `probe <issue>` und `satz <issue>`:** `gh issue view <nr> --json number,title,body,labels,comments`.
  - Im Issue steht der Pfad der Spezifikation (`docs/specs/<name>.md`), und es trägt das Label `spezifiziert`. Fehlt
    der Pfad, fehlt die Datei auf `origin/main` oder fehlt das Label, sagst du es Marcus und hörst auf, bevor eine
    Anfrage an fal.ai geht. Ohne `spezifiziert` gilt das Vorhaben als nicht spezifiziert, auch wenn eine Datei auf
    `origin/main` liegt. `spezifiziert` setzt du nicht selbst. Eine Icon-Liste aus Sheets, alten Tickets (`WIE-…`)
    oder einer Fassung auf einem anderen Branch ersetzt die Spezifikation nicht.
  - Liegt auf `origin/main` ein freigegebener Entwurf (`docs/specs/<name>/entwurf/README.md`), gehört er dazu.
  - Gibt es schon einen offenen PR auf einem Branch `grafik/<issue>-…` für dieselbe Stufe, arbeitest du dort weiter
    wie im Modus `nacharbeit`.
  - **Familien bestimmen:** Die Familie eines Icons folgt aus seiner Art in der Icon-Liste (Skill, Status,
    Gegenstand; Abschnitt 2). Ist die Art eines Icons nicht eindeutig (etwa Cast-Icons von Bossfähigkeiten), fragst
    du Marcus, statt es selbst zuzuordnen.
  - **Freigaben lesen:** Was je Familie freigegeben ist, nennt die Icon-README des Produkts auf `origin/main`
    (Madieval `Design/icons/README.md`, Abschnitt „Raster-Icons“, Zeilen wie in Abschnitt 6). Jede Zeile, auf die du
    dich stützt, prüfst du mit `gh pr view <n> --json state,mergedAt`: Der PR ist gemergt.
  - **`probe` nur für eine Familie ohne Freigabe:** Nennt die README für eine Familie, die das Vorhaben braucht,
    schon „Probetafel `<familie>` (Vorhaben #<nr>) freigegeben mit dem Merge von PR #<n>“, gibt es für sie keine
    zweite Probetafel, auch wenn die Spezifikation „zuerst eine Probetafel“ sagt. Du sagst Marcus, dass die Familie
    mit PR #<n> freigegeben ist, und arbeitest für sie im Modus `satz` weiter. Eine neue Probetafel für eine
    freigegebene Familie gibt es nur, wenn Marcus die Familie ausdrücklich neu festlegen will. Ist für die Familie ein
    Probe-PR eines anderen Vorhabens offen (`gh pr list --state open --json number,title,headRefName --jq '.[] |
    select(.headRefName | startswith("grafik/"))'`), sagst du Marcus, dass erst dessen Merge kommt, und hörst auf.
  - **`satz` nur mit freigegebener Probetafel, je Familie:** Für **jede** Familie im Satz nennt die README die Zeile
    „Probetafel `<familie>` (Vorhaben #<nr>) freigegeben mit dem Merge von PR #<n>“, und `gh pr view <n> --json
    state,mergedAt` zeigt ihn gemergt. Aus welchem Vorhaben die Probetafel stammt, spielt keine Rolle, es zählt nur
    diese Zeile. Fehlt sie für eine Familie, sagst du Marcus, dass zuerst `probe` läuft, und hörst auf.
  - Eigener Worktree von `origin/main`, nicht im Hauptverzeichnis. Branch `grafik/<issue>-<name>`, `<name>` nennt
    Familie und Stufe, etwa `grafik/257-skills-probe` und `grafik/257-skills`.
- **Modus `nacharbeit <pr>`:** `gh pr view <pr> --json title,body,headRefName,comments,reviews,commits` und dazu die
  Kommentare an Dateien und Bildern mit `gh api --paginate repos/{owner}/{repo}/pulls/<pr>/comments`. Die liefert
  `gh pr view` nicht, und an Probebildern kommentiert Marcus oft direkt. Dein Auftrag sind die Kommentare von Marcus
  aus allen drei Quellen (Unterhaltung, Reviews, Dateien) seit deinem letzten Commit und was er dir im Chat sagt,
  nichts darüber hinaus. Checke den Branch des PRs in einem eigenen Worktree aus.
- **Madieval:** PNGs liegen in Git LFS. Im frischen Worktree zuerst `git lfs pull`, sonst liest du Zeiger statt
  Sheets und Master.

## 2. Verstehen, bevor du generierst

- **Icon-Liste:** je Icon Name, Bedeutung, Art (Skill, Status, Gegenstand), Größen und Verwendung. Quellen sind die
  Spezifikation und die Liste der Platzhalter in der README des freigegebenen Entwurfs. Namen übernimmst du aus dem
  Code, wo es sie gibt (`BasicAttack`, keine Übersetzung von „Schwertschlag“). Ist die Liste unvollständig oder
  widersprüchlich, fragst du Marcus. Icons, die für die Familie schon auf `origin/main` liegen
  (`Design/icons/<familie>/master/`, etwa aus dem Satz eines anderen Vorhabens), erzeugst du nicht neu und
  überschreibst du nicht. Sie stehen in der Liste mit dem PR, der sie freigegeben hat. Ändern nur mit Marcus' Ja.
- **Probe:** Du wählst 3 bis 5 Icons, die die Familie tragen: verschiedene Formen, und jede Art, die der Satz hat. Die
  Wahl begründest du im PR. Soll die Probetafel mehr abdecken als den Satz dieses Vorhabens (etwa Skills und
  Status-Effekte zusammen), fragst du Marcus vorher. Eine Probetafel gibt nur die Familien frei, von denen sie Icons
  zeigt. Der PR nennt diese Familien.
- **Regeln des Produkts:** `products/<produkt>.md` im Repo `works`, frisch gelesen (auf Marcus' PC `C:\proj\works` nach
  `git pull`), und die dort genannten Dokumente. Bei Madieval dazu:
  - `Design/README.md`, „Stilrichtung“ und „Qualitaetsmassstab“ mit der Abgabeschwelle. Sie gilt für jede Vorlage.
  - `Design/icons/README.md`, die Icon-Konvention. Fehlt dort ein Abschnitt für Raster-Icons, schreibst du ihn im
    Probe-PR (Abschnitt 6) und legst ihn als Vorschlag zur Freigabe mit vor.
  - das Handoff mit den Sheets unter `Design/references/ui-handoff/`: eine Icon-Familie, starke Silhouetten, wenig
    Innendetail in kleinen Größen, Bedeutung über die Form und nie allein über die Farbe, kein Text und keine Zahlen
    im Bild. Rahmen, Abklingzeit, Taste, Ladungen, Seltenheit und der Zustand „deaktiviert“ entstehen zur Laufzeit.
- **Grundform, bevor Geld fließt:** randlos deckend (quadratisch, Motiv auf gemaltem Grund) oder freigestellt (Objekt
  mit Alpha, etwa ein Gegenstand auf dem Inventarplatz). Und ob das Motiv inneres Leuchten tragen darf, wenn ein Sheet
  es zeigt, eingebackenes Leuchten aber ausgeschlossen ist. Sagen Spezifikation, Entwurf oder Probetafel es nicht,
  fragst du Marcus.
- **Stilvorlage:** im Modus `probe` Ausschnitte aus den Sheets, im Modus `satz` die freigegebenen Master der
  Probetafel, bei Bedarf dazu Sheet-Ausschnitte. Ausschnitte schneidest du lokal mit Pillow, vergrößert, und legst
  sie im Scratchpad der Sitzung ab, nicht im Repo. Sie sind nur Vorlage: **Nie ein Asset aus einem Design-Sheet oder
  einem anderen Vorlagebild ausschneiden** (Referenz, Moodboard, Master eines anderen Icons). Ein Asset entsteht nur
  aus der eigenen fal.ai-Ausgabe für genau dieses Icon, und nur die wird als Asset zugeschnitten und freigestellt
  (Abschnitt 5). Datei und Bereich (x, y, Breite, Höhe) jedes Ausschnitts notierst du für die Metadaten.
- Farben misst du per Pixel am Sheet, statt sie nach Eindruck zu benennen.
- Material aus der Paperclip-Zeit (Rolle Nerdanel im Tag `paperclip-archiv`, Tickets `WIE-…` unter `archiv/paperclip/`)
  ist Geschichte, keine Vorgabe.

## 3. Zugang, Lizenz und Kosten

- **Zugang:** `fal_run.py check` meldet nur, ob und wo `FAL_KEY` steht (zuerst die Benutzervariable in
  `HKCU\Environment`, sonst der Prozess) und ob er sich senden lässt, nie den Wert. Endet ein Aufruf mit HTTP 401,
  lässt du `check` laufen und sagst Marcus, dass fal.ai den Schlüssel abgelehnt hat. Du wiederholst nicht. Endet
  `check` mit 1, weil der Schlüssel fehlt, sagst du Marcus: „Leg auf
  https://fal.ai/dashboard/keys einen Schlüssel an, trag ihn als Windows-Benutzervariable `FAL_KEY` ein
  (‚Umgebungsvariablen für dieses Konto bearbeiten‘) und starte die App neu.“ Meldet es den Schlüssel als unbrauchbar
  (Leerzeichen, Zeilenumbruch), gibst du Marcus diese Meldung weiter: den Schlüssel neu als eine Zeile eintragen und
  die App neu starten. Dann hörst du auf. Den Schlüssel lässt du dir nie in den Chat geben.
- **Der Schlüssel bleibt geheim.** Nur `fal_run.py` liest ihn und schickt ihn nur an fal.ai. Du gibst ihn nicht aus,
  schreibst ihn in keine Datei, keinen Commit, keine `.meta.yaml` und keinen Kommentar, und du rufst fal.ai weder mit
  `curl` noch mit eigenem Code auf.
- **Modell:** `fal-ai/nano-banana-pro/edit`, Bild zu Bild mit Stilvorlage. So entstand die Resource Bar von Madieval
  (`Design/hud/resource-bar/`). Ein anderes Modell, auch zum Freistellen oder Hochskalieren, nimmst du nur mit Marcus'
  Ja und nach derselben Lizenz- und Kostenprüfung.
- **Lizenz vor der ersten Anfrage**, nicht danach: Lies die Modellseite `https://fal.ai/models/<endpoint>`. Erlaubt
  ist nur ein Modell mit kommerzieller Nutzung (Abzeichen „Commercial use“), nicht „Research only“ oder
  „Non-commercial“. Ist die Angabe nicht eindeutig, generierst du nicht und fragst Marcus. Wortlaut, Prüfdatum und URL
  kommen in `modelLicense`.
- **Kosten vor jeder Anfrage:** Den Stückpreis von `fal-ai/nano-banana-pro/edit` kennt `fal_run.py` (0,15 USD je Bild
  bei `1K` und `2K`, 0,30 USD bei `4K`). Deshalb stehen `num_images` und `resolution` immer in der Eingabe. Für jedes
  andere Modell holst du den Preis mit `fal_run.py price <endpoint>` und gibst ihn mit `--unit-price` mit, nie für
  `fal-ai/nano-banana-pro/edit`: `price` nennt dort nur den Grundpreis ohne 4K-Aufschlag, und `run` lehnt einen
  Stückpreis unter dem bekannten Listenpreis ab. Eine Anfrage kostet `num_images` mal Stückpreis.
  `enable_web_search` bleibt aus der Eingabe: Die Websuche kostet je Anfrage 0,015 USD extra, die der Zähler nicht
  kennt, und `run` lehnt sie ab. `run` schreibt Bilder, Stückpreis und geschätzte Kosten in die `request.json`,
  bevor es abschickt. Die Summe führst du nicht im Kopf: `fal_run.py cost --run-dir <lauf>` zählt alle Anfragen des
  Laufs, auch nach einer Kontextverdichtung oder einem `resume` der Sitzung.
- **Gezählt wird je Grafik-PR**, also je Stufe (Probetafel oder Satz) mit aller Nacharbeit, nicht je Sitzung. Der
  erste Lauf einer Stufe beginnt bei 0. Jeder weitere Lauf am selben PR (Modus `nacharbeit`, oder `probe`/`satz` mit
  offenem PR) beginnt bei „Kosten gesamt“ aus der PR-Beschreibung. Diesen Wert gibst du jedem `run` mit
  `--spent-before <usd>` mit.
- **Budget:** `run` schickt nichts ab, was die Summe des Grafik-PRs über `--budget` hebt (Standard 3 USD), und endet
  dann mit 5. Dann fragst du Marcus im Chat, mit der Summe bisher, der geplanten Anfrage und dem Grund. Nur mit seinem
  Ja rufst du erneut auf, mit `--budget <seine Grenze>`. Die Grenze von 3 USD ist ein Vorschlag vom 27.09.2026 und
  gilt, bis Marcus eine andere nennt.
- **Konto gesperrt:** Endet `fal_run.py` mit 3 (HTTP 403, „User is locked“), wiederholst du nicht, weichst auf kein
  anderes Modell aus und hörst auf. Marcus nennst du den Grund aus der Meldung: „Exhausted balance“ heißt, das Konto
  muss aufgeladen werden. „Admin lock“ oder eine Sperre, die nach dem Aufladen bleibt, löst nur fal.ai
  (support@fal.ai; bekannter Fehler, fal-ai/fal#922 und #1168).

## 4. Generieren

- **Prompt** je Icon, in dieser Reihenfolge:
  1. Bezug auf die Stilvorlage: dieselbe Malweise, Licht- und Materialbehandlung wie das Referenzbild, ein neues
     Motiv.
  2. Motiv und Bedeutung: was das Icon zeigt und wofür es steht.
  3. Silhouette konkret: Form, Richtung, Anteile, lesbar in der kleinsten Größe.
  4. Material und Farbe, die Farbe gemessen am Sheet.
  5. Ausschnitt: quadratisch, Motiv mittig mit sicherem Rand. Bei Skills bleiben die Ecken ruhig (Taste oben links,
     Ladungen unten rechts).
  6. Grund: randlos wie die Vorlage, oder für freigestellte Icons einfarbig flach in einer Farbe, die im Motiv nicht
     vorkommt.
  7. Feste Ausschlüsse: „Kein Glow, kein Schatten, kein Text.“, dazu keine Zahlen und kein Rahmen.

  Keine benannten lebenden Künstler, keine Marken, keine fremden Figuren, keine realen Personen. Produkttrennung:
  nichts aus WYSCH oder RACR, kein Stil eines anderen Produkts.
- **Eingabe** als JSON-Datei im Scratchpad, etwa
  `{"prompt": "…", "num_images": 3, "aspect_ratio": "1:1", "resolution": "1K", "output_format": "png"}`. `4K` nur mit
  Marcus' Ja.
- **Laufordner** `<lauf>` = `<scratchpad>/grafik/<modus>-<nummer>` (etwa `probe-257`, `nacharbeit-301`), einer je
  Aufruf des Skills. Je Anfrage darin ein frischer Ordner `<lauf>/<icon>/<nr>`.
- **Aufruf** je Anfrage:

  ```
  fal_run.py run fal-ai/nano-banana-pro/edit --json <eingabe.json> --image <vorlage.png> --run-dir <lauf> --out <lauf>/<icon>/<nr> --spent-before <usd>
  ```

  `--spent-before` nur, wenn am PR schon Kosten stehen (Abschnitt 3). Die Vorlagen gehen als base64-Data-URI mit,
  nicht als Upload auf das CDN von fal.ai. Das Skript prüft das Budget, reiht die Anfrage in die Warteschlange ein,
  lädt die Bilder sofort herunter (ihre Links bei fal.ai gelten nur einen Tag) und schreibt `result.json` mit
  Request-ID, Seed, Kosten, Dateien und Links.
- **Nie doppelt bezahlen:** Endet es mit 4 oder bricht der Aufruf nach dem Einreihen ab (Zeitlimit des Werkzeugs,
  Abbruch, Meldung mit `resume --out`), liegt also `request.json` ohne `result.json` im Ordner, ist die Anfrage schon
  bezahlt. Dann `fal_run.py resume --out <ordner>`, keine neue Anfrage und kein neuer Ordner. Bevor du für ein Icon
  neu anfragst, prüfst du den letzten Ordner darauf. Neu anfragen, mit geänderter Eingabe und in einem frischen
  Ordner, nur wenn fal.ai die Anfrage selbst abgelehnt hat oder sie gescheitert ist (Meldung ohne `resume`). Sie
  bleibt in der Summe. Meldet `run` „Whether fal.ai queued the request is unknown“ (keine Antwort oder nur ein
  Serverfehler 5xx), fragst du nur in einem frischen Ordner neu an: Die alte Anfrage bleibt in der Summe und kann
  doppelt kosten.
- Je Icon mehrere Varianten (`num_images` 2 bis 4). Du wählst gegen die Vorlage, die verworfenen zeigst du im PR.
- **Methode vor Stellschraube:** Kommt ein Icon nach zwei Runden nicht näher an die Vorlage, änderst du den Weg
  (Vorlage, Aufbau des Prompts, Grundform), nicht einen Parameter. Gemalte Motive baust du nie per Skript, SVG, Masken
  oder Pixelretusche nach.

## 5. Aufbereiten und prüfen

- **Freistellen** (nur freigestellte Icons): Das Alpha kommt aus der Form, nicht aus der Helligkeit. Hintergrund ist,
  was vom Bildrand aus über Pixel in der Grundfarbe erreichbar ist (Flutfüllung mit Toleranz). Eingeschlossene Flächen
  in der Grundfarbe ordnest du nach Größe: kleine gehören zum Motiv, große sind gewollte Öffnungen. Weich ist nur der
  Saum an der Silhouette, dort nimmst du die Grundfarbe aus den Randpixeln. Gelingt das mit Pillow und numpy nicht
  sauber, gilt für ein Modell zur Hintergrundentfernung Abschnitt 3.
- **Putzen:** zuschneiden, mittig auf eine quadratische Fläche mit sicherem Rand, mit Lanczos auf die Mastergröße
  (Abschnitt 6). Retuschiert wird nur gezielt (Reste der Grundfarbe, Artefakte an Kanten), nicht neu aufgebaut.
- **Exporte** rechnest du aus dem Master mit Lanczos, nur in den Spielgrößen, die Spezifikation und Entwurf nennen
  (Madieval, Skill-Icons: etwa 40, 52 und 64 px laut UI-1 und Sheet 01). Nennen beide für eine Art keine Größe (etwa
  Status-Effekte, Gegenstände), fragst du Marcus, bevor du Master oder Exporte festlegst. PNG, neu gespeichert ohne
  Metadaten.
- **Sieh dir jedes Bild selbst an**, aus der Datei:
  - jede Exportgröße 1:1, auf hellem, normalem und dunklem Grund, denselben wie auf der Probetafel
  - in Schwarz-Weiß: trägt die Silhouette die Bedeutung ohne Farbe?
  - bei Skills im Zustand „deaktiviert“ (Sättigung −80 %, Helligkeit −50 %)
  - freigestellte Icons zusätzlich über Magenta und über dem Grund der Rohgenerierung: kein Saum, kein Halo, keine
    Löcher
  - alle Icons des PRs nebeneinander: Hält die Familie zusammen?
- **Probebilder** (Ort und Namen in Abschnitt 6):
  - `<issue>-probetafel.png`, im Modus `satz` `<issue>-satz-probetafel.png`: je Icon eine Zeile mit dem Ausschnitt der
    Stilvorlage, deutlich als Referenz abgesetzt, dem Master, den Exportgrößen 1:1 auf hellem, normalem und dunklem
    Grund, Schwarz-Weiß und bei Skills „deaktiviert“. Bei Madieval sind das dieselben Untergründe wie in
    `Design/hud/resource-bar/proof/probe-drei-untergruende.png`, mit einer mittleren Helligkeit von etwa 203, 87 und
    11 (gemessen in WIE-271; Auflage 2 aus WIE-276 in `Design/hud/resource-bar/README.md`). Ein Grund allein
    verbirgt, was auf einem anderen auffällt (PR #238). Im Modus `satz` steht in der ersten Zeile ein freigegebenes
    Probe-Icon.
  - `<issue>-varianten.png`, im Modus `satz` `<issue>-satz-varianten.png`: alle Varianten je Icon, die gewählte
    markiert.
- **Abgabeschwelle:** je Icon und Kriterium ein Urteil „ja“ oder „nein“: Silhouette in der kleinsten Größe lesbar,
  Bedeutung ohne Farbe erkennbar, gehört zur Familie, hält neben der Vorlage in Spielgröße stand, sauberer Rand. Steht
  irgendwo „nein“, legst du nicht vor, sondern arbeitest weiter oder sagst Marcus, welches Mittel fehlt. Nach jeder
  Änderung urteilst du alle Zeilen neu.

## 6. Ablegen

Im Worktree, nach der Icon-Konvention des Produkts. **Madieval** unter `Design/icons/<familie>/`, je Familie ein
Ordner: `skills` (Skill), `status` (Status-Effekt), `items` (Gegenstand). Aufgebaut wie `Design/hud/resource-bar/` und
wie die Symbol-Master aus `vorhaben-entwerfen`: `master/` für Unreal, `raw/`, `export/` und `proof/` zur Ansicht.
Raster-Master kommen nie nach `source/`: `Design/icons/source/` und `Design/icons/export/` gehören dem SVG-Probelauf
aus WIE-11 (von Hand bearbeitete SVG-Master) und bleiben unverändert.

- `<familie>/master/<name>.png`: der Master, quadratisches RGBA-PNG in vierfacher Größe der größten Spielgröße, die
  Spezifikation oder Entwurf für die Familie nennen. Das ist die Madieval-Regel für Master wie in
  `Design/hud/resource-bar/master/`, bei Skill-Icons also 4 × 64 = 256 × 256 px. Dateiname englisch, klein, mit
  Bindestrichen und dem Präfix der Art, etwa `icon-skill-basic-attack.png`, `icon-status-slowed.png`.
- `<familie>/master/<name>.meta.yaml` mit den Feldern wie in `Design/hud/resource-bar/master/`:
  - `asset`, `project`, `ticket: "#<issue>"`, `createdBy: vorhaben-grafik`, `assetType: icon`, `generator: fal.ai`
  - `model`: der Endpunkt
  - `prompt`: wörtlich wie gesendet, dazu die Nacharbeit in einem Satz
  - `seed`: aus `result.json`, sonst `null`
  - `modelLicense`: Wortlaut, Prüfdatum, URL der Modellseite
  - `costUsd`: `cost.unit_price_usd` aus der `request.json` der Anfrage, aus der der Master stammt
  - `sources`: Stilvorlage mit Datei und Bereich, Request-ID, Rohbild im Repo, Link bei fal.ai mit dem Hinweis, dass
    er nach einem Tag abläuft
  - `exports`, `licenseStatus: own`
  - `approvedBy` und `approvedAt` auf `null`, denn freigegeben wird mit dem Merge

  Unbekanntes bleibt `null`, nichts erfinden.
- `<familie>/raw/<name>.png`: das Rohbild der gewählten Variante, unverändert, neben `master/` und nicht darin, damit
  `master/` nur enthält, was nach Unreal geht. Verworfene Varianten stehen nur im Variantenbild.
- `<familie>/export/<name>-<größe>.png`: die Exporte, nur zur Ansicht, nie von Hand ändern.
- `<familie>/proof/<issue>-probetafel.png` und `<familie>/proof/<issue>-varianten.png`, im Modus `satz`
  `<issue>-satz-probetafel.png` und `<issue>-satz-varianten.png`. Die Issue-Nummer hält die Belege mehrerer Vorhaben
  derselben Familie auseinander, etwa die Status-Effekte aus UI-2 und UI-5. Umfasst eine Probe mehrere Familien, legst
  du je Familie eine eigene Probetafel und ein eigenes Variantenbild ab. Probebilder anderer PRs änderst du nicht.
- `Design/icons/README.md`: Im ersten Probe-PR kommt ein Abschnitt „Raster-Icons“ dazu, mit dieser Konvention (Ordner
  `<familie>/master`, `raw`, `export`, `proof`, Namen, Größen, Metadaten, Grundform, Prüfung, Einbinden als UI-Textur
  aus dem Master) als Vorschlag. Dazu kommt ein offener Punkt für die Umsetzung: Unreal bekommt den Master als
  UI-Textur ohne Mips (so importiert in PR #238) und verkleinert ihn bei 40 px um das 6,4-Fache. Ob das flimmert oder
  ausfranst, ist nicht gemessen. Die Umsetzung prüft es beim Import im PIE in 1080p. Denselben Punkt führst du im PR
  unter „offene Fragen“. Je Familie steht dort, was freigegeben ist, eine Zeile je Grafik-PR:
  - für jede Familie, von der die Probetafel Icons zeigt: „Probetafel `<familie>` (Vorhaben #<issue>) freigegeben mit
    dem Merge von PR #<n>“
  - je Satz: „Satz `<familie>` (Vorhaben #<issue>) freigegeben mit dem Merge von PR #<m>“, mit den Icons dieses Satzes
    und seinen Probebildern

  Zeilen anderer PRs bleiben stehen. `source/` und `export/` aus dem SVG-Probelauf WIE-11 bleiben als Geschichte
  stehen.
- Nichts unter `Content/`, kein Import in Unreal. Eingebunden wird von der Umsetzung oder über das Folge-Issue aus
  Abschnitt 7, der Master aus `master/` als UI-Textur, nicht `export/` oder `raw/`.
- Nach dem Commit prüfst du mit `git lfs ls-files`, dass alle PNGs in LFS stehen.

Nennt ein anderes Produkt keinen Ort für Raster-Icons (WYSCH und RACR haben keinen), fragst du Marcus, bevor du einen
anlegst.

## 7. Vorlegen

- Commit in der Sprache, die die `CLAUDE.md` des Produkts für Commits und PRs vorgibt (ohne Vorgabe Deutsch; ReadyStackGo: Englisch, weil Open Source), etwa `docs(grafik): Probetafel der Skill-Icons`, dann `git push -u origin grafik/<issue>-<name>`.
- `gh pr create --base main --head grafik/<issue>-<name> --title "docs(grafik): <Titel>" --body-file <datei>`. Die
  Beschreibung schreibst du vorher mit dem Write-Werkzeug als UTF-8-Datei in den Scratchpad (Abschnitt „Grenzen“,
  „Texte nur aus einer Datei“). Sie wird beim Merge die Commit-Nachricht und beschreibt also den fertigen Stand:
  - als erster Punkt das Urteil der Abgabeschwelle als Tabelle, je Icon und Kriterium „ja“ oder „nein“
  - „Vorhaben #<issue>“, ohne „Closes“: Die Grafik schließt das Issue nicht
  - die Familien, die der PR freigibt
  - die Icons: Name, Bedeutung, Master, Exporte, im Modus `probe` die Begründung der Auswahl
  - die Probebilder, eingebunden über den Commit, nicht über den Branch, denn der Branch wird mit dem Merge gelöscht.
    `<sha>` kommt aus `git rev-parse HEAD` nach dem Push:
    `![<Bild>](https://github.com/<owner>/<repo>/blob/<sha>/<pfad>?raw=true)`. Daneben steht der Pfad im Repo, unter
    dem das Bild nach dem Merge auf `main` liegt.
  - Modell, Lizenz mit Prüfdatum, Anzahl der Anfragen und Bilder, Kosten gesamt über alle Läufe an diesem PR
    (Grundlage des Budgets, Abschnitt 3): aus `fal_run.py cost --run-dir <lauf>`, als Schätzung nach Stückpreis, bei
    einem weiteren Lauf plus der bisherige Wert aus der Beschreibung
  - im Probe-PR die Konvention aus `Design/icons/README.md` als Vorschlag, den Marcus mit dem Merge freigibt
  - offene Fragen
  - `## Patchnotes` mit „Keine Auswirkung für Nutzer.“
  - der Hinweis: **„Freigeben: Merge, danach <nächster Schritt>. Ändern: Kommentar im PR, danach
    `/vorhaben-grafik nacharbeit <PR>` in einer Sitzung am PC.“**

  Labels `vorhaben` und `freigabe`. Danach trägst du die PR-Nummer in die Icon-README ein, als zweiter Commit.
- **Einbinden (nur Modus `satz`):** Plan und Umsetzung arbeiten mit Platzhaltern. Liegt der Satz bei der Umsetzung
  schon freigegeben auf `main`, bindet sie ihn ein (`vorhaben-umsetzen`, Abschnitt 2), sonst ein Folge-Issue.
  Unmittelbar vor PR-Beschreibung und Kommentar liest du den Stand des Vorhabens frisch:
  `gh issue view <issue> --json state`,
  `gh pr list --state all --limit 200 --json number,state,isDraft,headRefName --jq '.[] | select(.headRefName |
  startswith("vorhaben/<issue>-"))'` und
  `gh issue list --state open --search 'in:title "Icons einbinden"' --json number,title,body`.
  - Noch kein Umsetzungs-PR und das Issue offen: Die Umsetzung bindet die Icons ein. Kein Folge-Issue.
  - Umsetzungs-PR offen oder gemergt, oder das Issue geschlossen: das Folge-Issue „Icons einbinden: <Familie>,
    <Oberfläche>“. Gibt es für dieses Vorhaben und diese Familie schon ein offenes, ziehst du dessen Text nach
    (`gh issue edit <m> --body-file <datei>`), statt ein zweites anzulegen. Sonst legst du es an
    (`gh issue create --title "Icons einbinden: <Familie>, <Oberfläche>" --body-file <datei>`), ohne Label und ohne
    das Wort „Vorhaben“ im Titel, sonst gilt es als Vorhaben ohne Spezifikation. Der Text reicht für eine Sitzung ohne
    diesen Chat:
    - „Folge von Vorhaben #<issue>“ und die Voraussetzung: Merge von PR #<pr>. Wird er ohne Merge geschlossen, ist
      das Issue erledigt.
    - je Icon: der Master (`Design/icons/<familie>/master/<name>.png`), der Platzhalter, den es ersetzt, und die
      Stelle im Produkt (bei Madieval etwa `T_IconSword` in `DA_BasicAttack`), aus dem Umsetzungs-PR („Platzhalter“).
      Ist der Skill oder Effekt noch nicht gebaut, steht dabei, dass das Vorhaben, das ihn baut, das Icon einbindet.
    - Eingebunden wird nach `Design/icons/README.md`: der Master als UI-Textur, nicht die Exporte. Gebaut und geprüft
      wird nach „Vorhaben: bauen und testen“ in der `CLAUDE.md` des Produkts (Madieval am PC mit Editor), mit eigenem
      PR, `Closes #<m>` und `## Patchnotes`.
    - Nimmt ein Umsetzungs-PR die Icons mit, schließt er dieses Issue zusätzlich („Closes #<m>“).
- **Nächster Schritt**, im PR (Beschreibung per `gh pr edit <pr> --body-file <datei>` nachziehen) und im
  Issue-Kommentar:
  - nach der Probetafel: `/vorhaben-grafik satz <issue>` in einer Sitzung am PC
  - nach dem Satz, je nach Stand oben: die Umsetzung bindet die Icons ein (`/vorhaben-umsetzen <Issue>`,
    sobald der Plan gemergt ist), oder das Folge-Issue #<m>. Soll der Figma-Entwurf die Icons statt der Platzhalter
    zeigen, geht das in einer Sitzung mit `/vorhaben-entwerfen <issue>` als Erweiterung des Entwurfs. Das machst du
    hier nicht.
- Im Issue des Vorhabens kommentierst du mit Link auf den PR und dem nächsten Schritt
  (`gh issue comment <nr> --body-file <datei>`). Gibt es ein Folge-Issue „Icons einbinden“ zu diesem Vorhaben und
  dieser Familie, kommentierst du auch dort, nach der Probetafel ebenso.
- Modus `nacharbeit`: push, dann die PR-Beschreibung nachziehen (`gh pr edit <pr> --body-file <datei>`, Aufbau wie
  oben): Bilder mit dem SHA des neuen Commits, Urteil und offene Fragen auf dem neuen Stand, Kosten gesamt um die
  Anfragen dieses Laufs erhöht. Ändern sich im Modus `satz` Icons, Namen oder Platzhalter, ziehst du das Folge-Issue
  nach (`gh issue edit <m> --body-file <datei>`). Danach ein PR-Kommentar mit jedem Punkt von Marcus und dem, was du
  geändert hast (`gh pr comment <pr> --body-file <datei>`).
- Danach sagst du Marcus die PR-Nummer und hörst auf.

## Grenzen

- **Kein Merge und kein Auto-Merge**, auch wenn eine Regel der Sitzung sagt, PRs gleich zu mergen oder den Auto-Merge
  einzuschalten. Der Merge ist Marcus' Freigabe. Approven kann er den PR nicht, er stammt von seinem Konto.
- Im Repo änderst du nur die Icons des Satzes mit Metadaten, Rohbildern, Exporten und Probebildern und die Icon-README
  (Madieval `Design/icons/`). Kein Code, nichts unter `Content/`, nichts in Figma, kein Plan.
- Kein Label `planen`, keine Planung, keine Umsetzung. Das Folge-Issue zum Einbinden legst du an, eingebunden wird in
  einer anderen Sitzung.
- **Keine Geheimnisse:** `FAL_KEY` nie ausgeben, loggen oder in Dateien, Commits oder Kommentare schreiben. fal.ai nur
  über `fal_run.py`.
- **Texte nur aus einer Datei:** PR-Beschreibungen, Issue- und PR-Kommentare und Issues (auch der Hinweis bei
  gesetztem `VORHABEN_OUT`) schreibst du mit dem Write-Werkzeug als UTF-8-Datei in den Scratchpad und übergibst sie
  mit `--body-file <datei>`. Nie inline mit `--body "…"`, auch keinen kurzen Text: Die Texte enthalten Backticks, `$`
  und Umlaute. Bash führt Backticks als Befehl aus, PowerShell liest sie als Escape-Zeichen, und keiner der Aufrufe
  meldet einen Fehler. Nach dem Senden liest du den Text zurück (`gh pr view <pr> --json body`,
  `gh issue view <nr> --json body,comments`) und vergleichst ihn mit der Datei. In der Datei steht nie `FAL_KEY`.
- **Nie ein Asset aus einem Design-Sheet oder einem anderen Vorlagebild ausschneiden** (Referenz, Moodboard, Master
  eines anderen Icons). Als Asset zugeschnitten und freigestellt wird nur die eigene fal.ai-Ausgabe für genau dieses
  Icon. Ausschnitte sind nur Stilvorlage und gehen als Data-URI an fal.ai, nicht als Upload.
- **Budget:** prüft `fal_run.py run --budget`: höchstens 3 USD je Grafik-PR (Stufe mit aller Nacharbeit) ohne
  Rückfrage, Vorschlag vom 27.09.2026, bis Marcus eine andere Grenze nennt. Höher nur mit Marcus' Ja. Kosten nur aus
  `fal_run.py cost`.
- Bei erschöpftem Guthaben oder gesperrtem Konto nicht wiederholen, nicht ausweichen, Marcus sagen.
- Nichts erfinden: keine Lizenz, keine Kosten, keine Seeds, keine Quellen ohne Beleg.
- Sprache: README und Kommentare auf Deutsch, Commits und PR in der Sprache, die die `CLAUDE.md` des Produkts für Commits und PRs vorgibt (ohne Vorgabe Deutsch; ReadyStackGo: Englisch, weil Open Source).
- Keine Signatur und keine Zeile „Generated with Claude Code“ in Commits, PRs und Kommentaren.
