# Plan: Erweiterter Modus für Deployment-Variablen

Vorhaben #522 · Spezifikation [`docs/specs/variablen-erweiterter-modus.md`](../specs/variablen-erweiterter-modus.md) ·
Entwurf [`docs/specs/variablen-erweiterter-modus/entwurf/README.md`](../specs/variablen-erweiterter-modus/entwurf/README.md)
(freigegeben mit dem Merge von PR #523) · Stand der Planung: `origin/main` 1f0a8af, 19.11.2026

## 1. Kontext

Deploy Stack, Deploy Product, Upgrade Stack und Upgrade Product zeigen heute jede Variable. Bei Produkten mit 40 und
mehr Variablen, die meisten mit passendem Default, gehen die wenigen unter, die der Benutzer wirklich setzen muss. Die
Spezifikation führt das Manifest-Kennzeichen `advanced` ein (in `variables:` und `sharedVariables:`). Die vier Seiten
zeigen standardmäßig nur die übrigen Variablen, ein Schalter im Seitenkopf blendet die erweiterten ein. Ausblenden
ändert nur die Anzeige: Werte, „Save value“ und die Prüfung „Missing required variables“ bleiben. Der freigegebene
Entwurf legt Schalter, Hinweise, Warnung, Tags, Texte und Verhalten fest und beantwortet die offenen Fragen der
Spezifikation zur Gestalt.

## 2. Ergebnis aus Nutzersicht

- Ein Manifest-Autor markiert Variablen mit `advanced: true`. Ohne das Kennzeichen sieht jede Seite aus wie heute,
  ohne Schalter.
- Hat eine Seite erweiterte Variablen, steht rechts im Seitenkopf „Show advanced variables“, daneben „{n} hidden“.
  Die Seite öffnet immer im Standardmodus. Gruppen ohne sichtbare Variable verschwinden samt Überschrift.
- Am Ende jedes Abschnitts mit ausgeblendeten Variablen steht eine gestrichelte Zeile „{n} advanced variables hidden“
  mit „Show“. Fehlt einer ausgeblendeten Pflichtvariable der Wert, wird daraus eine gelbe Warnung, die sie nennt.
- Im erweiterten Modus erscheinen alle Variablen an ihrer Stelle, die erweiterten mit dem Tag „Advanced“.
- Deployen und Upgraden im Standardmodus übernehmen die Werte der ausgeblendeten Variablen (Default, `.env`-Import,
  gespeicherter Wert auf Upgrade Product, vorher eingegebener Wert), genau wie bei sichtbaren. Fehlt ein Pflichtwert,
  nennt die Meldung die Variable und, wenn sie ausgeblendet ist, den Schalter.
- Auf den Produkt-Seiten tragen die Stacks im Akkordeon Tags („2 services“, „1 variable“, „Required“, „New“). Ein
  Stack, dessen eigene Variablen alle ausgeblendet sind und der keine Warnung hat, bleibt zu.
- Upgrade Stack markiert neue Variablen mit dem Tag „New“ statt mit einem Ring; die Info über neue Variablen und neue
  Stacks steht als Hinweis über den Karten und nennt, welche davon erweitert sind.
- Website (en/de) und Repo-Dokumentation beschreiben `advanced` mit Beispiel.

## 3. Umfang und Nicht-Umfang

**Umfang**

- Backend: `RsgoVariable`, `Variable`, Parser und Produktquelle, die DTOs von `GetStack`, `ListStacks`,
  `ListProducts`, `GetProduct` und das API-DTO der vier Endpunkte.
- `@rsgo/core`: Typ `StackVariable`, reine Logik zu Sichtbarkeit, Gruppen, Zählung und Pflichtprüfung, die vier Stores.
- `@rsgo/ui-generic`: Schalter, Hinweis, Tag, Label-Zeile der Variablenfelder, die vier Seiten.
- Tests (.NET, Vitest, Browsertest gegen den Container), Testmanifest, Dokumentation, `wwwroot`.

**Nicht-Umfang**

- Ansichten ohne Eingabe (Product Deployment Detail, Deployment-Detail), Deployen über API, Hooks oder andere Clients,
  Kennzeichen pro Gruppe, Bedingungen, Sicht je Rolle (Spezifikation).
- Umstellen bestehender Manifeste; eigene Deploy- und Upgrade-Seiten von Distributionen (bekommen das Feld und die
  Store-Felder über `@rsgo/core`, ziehen selbst nach).
- Was die Rahmen nebenbei anders zeigen und nicht zum Vorhaben gehört: Untertitel der Seiten, umrandete Knöpfe der
  Seitenleiste, die Checkbox (E16, E19).
- AAA-Kontrast (offene Frage des Entwurfs, eigenes Vorhaben).

## 4. Entscheidungen, die in diesem Plan stecken

- **E1 – Namen in den Schichten.** Manifest `advanced` (Vorgabe). `RsgoVariable.Advanced` (`bool?`, nach
  `DefaultTransient`, `RsgoVariable.cs:79`), `Variable.IsAdvanced` (`bool`, letzter Konstruktor-Parameter
  `isAdvanced = false` nach `defaultTransient`, `Variable.cs:90-105`), in den Records der Abfragen `IsAdvanced = false`
  am Ende (additiv), im API-DTO `IsAdvanced`, im JSON `isAdvanced`, in TypeScript `isAdvanced?: boolean`. Grund: Wie
  `required` → `IsRequired` → `isRequired` (`stacks.ts:38`). Das YAML liest der Parser ohne weiteren Code
  (`CamelCaseNamingConvention`, `IgnoreUnmatchedProperties`, `RsgoManifestParser.cs:34-38`); ältere Versionen
  ignorieren das Feld.
- **E2 – Eine Abbildung `RsgoVariable` → `Variable`.** Heute gibt es zwei: `RsgoManifestParser.ConvertToVariable`
  (`RsgoManifestParser.cs:481-502`) und `LocalDirectoryProductSourceProvider.ConvertToVariable`
  (`LocalDirectoryProductSourceProvider.cs:736-756`). Die zweite baut die Variablen der Stacks von Multi-Stack-Produkten
  (`CreateStackFromEntry`, `:413-430`) und übergibt `DefaultTransient` nicht. Git- und OCI-Quellen laden über
  denselben Provider (`GitRepositoryProductSourceProvider.cs:73`, `OciRegistryProductSourceProvider.cs:166`), ihre
  Multi-Stack-Produkte also ebenso; Single-Stack-Manifeste nehmen bei allen Quellen den Weg über den Parser
  (`LocalDirectoryProductSourceProvider.cs:469` → `RsgoManifestParser.cs:405-448`). Ohne Änderung käme `advanced` bei
  Produkt-Stacks nie an. Neu: eine statische Funktion `RsgoVariableMapper.ToVariable(name, def)`
  unter `Infrastructure/Parsing/`, beide Stellen rufen sie. Die Regel „stack-specific overrides shared“ bleibt (Parser
  `:436`, `:463`; Provider `:426`): Definiert ein Stack-Eintrag eine geteilte Variable neu, gilt seine ganze
  Definition; fehlt dort `advanced`, ist sie in diesem Stack nicht erweitert. Wirksam ist für Produkt-Stacks die Stelle
  im Provider; `RsgoManifestParser.ExtractStackVariablesAsync` ruft in `src` niemand auf (nur
  `IRsgoManifestParser.cs:52` und Tests).
- **E3 – Auch über `GetStack` und das API-DTO.** Die Spezifikation nennt `GetProduct`, `ListProducts`, `ListStacks`.
  Deploy Stack und Upgrade Stack laden aber über `GET /api/stacks/{id}` (`useDeployStackStore.ts:142`,
  `useUpgradeStackStore.ts:165`) → `GetStackHandler` (`GetStackHandler.cs:37-52`), der `DefaultTransient` heute nicht
  übergibt. Außerdem bilden alle vier Endpunkte auf `StackVariableDto` ab (`ListStacksEndpoint.cs:89-105`; Abbildungen
  `ListStacksEndpoint.cs:41`, `GetStackEndpoint.cs:57`, `ListProductsEndpoint.cs:50`, `GetProductEndpoint.cs:60`), und
  dieses DTO hat kein `DefaultTransient`. `IsAdvanced` kommt deshalb in beide Records (`StackVariableItem`,
  `ListStacksQuery.cs:23-39`; `StackVariableDetails`, `GetProductQuery.cs:159-175`), in alle vier Handler, ins
  API-DTO und in alle vier Abbildungen.
- **E4 – `defaultTransient` wird mitrepariert (über die Spezifikation hinaus).** Aus E2 und E3 folgt: Das Vorbild der
  Spezifikation erreicht den Browser heute nicht. Bei Produkt-Stacks verliert es der Provider, auf allen Wegen das
  API-DTO. Sichtbar wird das auf Deploy Product und Upgrade Product: „Save value“ startet bei `defaultTransient: true`
  angehakt, obwohl die Stores den Haken dann weglassen würden (`useDeployProductStore.ts:246-256`,
  `upgradeVariables.ts:211-216`). Weil dieselben Zeilen ohnehin geändert werden, reicht der Plan `DefaultTransient`
  mit durch (über E2, `GetStackHandler`, API-DTO). Folge für Nutzer: Solche Variablen starten künftig wie dokumentiert
  ohne Haken; das steht in den Patchnotes der Umsetzung. Soll das nicht mit, wird es ein eigenes Bug-Issue.
- **E5 – Zustand des Schalters im Store, nichts gemerkt.** Jede der vier Seiten startet im Standardmodus, der Browser
  speichert nichts (Vorschlag der Spezifikation, Entwurf „Jede Seite öffnet im Standardmodus“). Der Zustand liegt als
  `showAdvanced` und `setShowAdvanced` in den vier Stores, additiv wie schon `expandedStacks`
  (`useDeployProductStore.ts:72`). Grund: Distributionen bekommen ihn über `@rsgo/core`; Upgrade Product braucht ihn
  beim Versionswechsel für das Akkordeon (E10). Ein Versionswechsel lässt den Modus stehen; die Rückkehr aus dem
  Precheck öffnet die Seite neu, also im Standardmodus, mit den mitgegebenen Werten.
- **E6 – Reine Logik in `@rsgo/core`, Texte in `@rsgo/ui-generic`.** Neue Datei
  `packages/core/src/lib/advancedVariables.ts`, exportiert über `index.ts` (wie `lib/urls`, `index.ts:10`): die
  Gruppierung (zieht aus `VariableInput.tsx:117-133` um; der Export `groupVariables` dort bleibt und ruft die neue
  Funktion), Sichtbarkeit je Modus, Zählungen, Pflichtprüfung, Regel des Akkordeons. Die Texte als reine Funktionen in
  `packages/ui-generic/src/components/variables/advancedVariableTexts.ts`. Grund: Vitest prüft nur reine Logik ohne
  DOM (`vitest.config.ts:8-11`); so sind alle Regeln und Texte testbar.
- **E7 – Gruppen und ihre Reihenfolge bleiben stabil.** Erst über alle Variablen gruppieren, dann je Gruppe filtern
  und leere Gruppen weglassen. Würde man vor dem Gruppieren filtern, könnte sich die Reihenfolge der Gruppen zwischen
  den Modi ändern: `groupVariables` ordnet Gruppen nach dem ersten Auftreten (`VariableInput.tsx:120-125`), und die
  Variablen der Produkt-Stacks sind nicht nach Gruppe sortiert (`LocalDirectoryProductSourceProvider.cs:413-430`,
  anders als im Parser, `RsgoManifestParser.cs:445`, `:476`). Überschriften gibt es, wenn mehr als eine Gruppe
  sichtbar ist (heutige Regel `groups.size > 1`, z. B. `DeployStack.tsx:283`).
- **E8 – Geteilte Variable mit unterschiedlichem `advanced`.** Kommt eine Variable in zwei oder mehr Stacks vor,
  zeigt die Seite sie einmal unter „Shared Variables“, mit der Definition des ersten Stacks (`computeSharedVariables`,
  `upgradeVariables.ts:12-27`; Kopie in `useDeployProductStore.ts:14-30`). Für `isAdvanced` gilt: erweitert nur, wenn
  sie in jedem dieser Stacks erweitert ist. Grund: Hat ein Stack-Eintrag sie ohne `advanced` neu definiert, ist sie
  für ihn wichtig, und die Reihenfolge der Stacks soll das nicht entscheiden. Die übrigen Eigenschaften bleiben beim
  ersten Stack (Test `upgradeVariables.test.ts:73`). Deploy Product nutzt künftig `computeSharedVariables` aus
  `upgradeVariables.ts` statt seiner Kopie.
- **E9 – Texte.** Aus der README des Entwurfs („Texte (englisch)“), ergänzt, wo sie schweigt:
  - Schalter „Show advanced variables“ (Entwurf; die Spezifikation schlug „Show advanced settings“ vor), im
    Standardmodus daneben „{n} hidden“; per `aria-describedby` „1 advanced variable hidden“ / „{n} advanced variables
    hidden“; Live-Region beim Einschalten „1 advanced variable shown“ / „{n} advanced variables shown“.
  - Hinweis, Warnung und Tags wie im Entwurf, die Warnung also bei einer Variable „{Label} is required and has no
    value“, bei mehreren „{k} required advanced variables have no value“. Das Bild der Komponente
    (`komponente-hinweis.png`) zeigt einen anderen Beispieltext („1 required advanced variable is empty“); es gelten
    README und Rahmen.
  - Meldung bei fehlenden Pflichtvariablen, für alle Pflichtvariablen (auch sichtbare): Titel „Missing required
    variable“ / „Missing required variables“. Text bei einer: „{Label} (stack {stack}) has no value.“, ist sie gerade
    ausgeblendet, dazu „It is an advanced variable: turn on “Show advanced variables” to enter it.“ (Entwurf). Bei
    mehreren: „{A}, {B} (stack {s}) and {C} have no value.“, für die ausgeblendeten darunter „{X} is an advanced
    variable: turn on “Show advanced variables” to enter it.“ bzw. „{X} and {Y} are advanced variables: turn on
    “Show advanced variables” to enter them.“ „(stack …)“ nur auf den Produkt-Seiten bei stackspezifischen Variablen.
    Ob eine Variable ausgeblendet ist, gilt für den Modus beim Anzeigen: Nach dem Einschalten entfällt der Satz zum
    Schalter, die Meldung bleibt bis zum nächsten Klick auf Deploy oder Upgrade wie heute.
  - Info auf Upgrade Stack: Titel „1 new variable in {version}“ / „{k} new variables in {version}“. Text: die Labels
    („A.“, „A and B.“, „A, B and C.“), dazu „{A} is an advanced variable.“ / „{A} and {B} are advanced variables.“ /
    bei allen (k ≥ 2) „All of them are advanced variables.“; ist die einzige neue erweitert, nur „{A} is an advanced
    variable.“ Entfernte Variablen (heute in derselben Karte, `UpgradeStack.tsx:307-318`) als Satz „Removed in
    {version}: {names}.“; gibt es nur entfernte, Titel „1 variable removed in {version}“ / „{r} variables removed in
    {version}“ und Text „{names}.“
  - Info auf Upgrade Product: Titel „New stack in {version}: {name}“ (Entwurf) / „New stacks in {version}: {a} and
    {b}“. Text nur, wenn neue Stacks erweiterte eigene Variablen haben: „Its {n} variables are advanced and start with
    their defaults.“ (Entwurf) / „Its variable is advanced and starts with its default.“ / „{a} of its {n} variables
    are advanced and start with their defaults.“; bei mehreren neuen Stacks „{a} of their {n} variables are …“ /
    „All {n} of their variables are …“. Nicht mehr enthaltene Stacks (heute `UpgradeProduct.tsx:545-559`) als Satz
    „No longer in {version}: {names}. These stacks will not be automatically removed. You can remove them manually
    after the upgrade.“; gibt es nur solche, Titel „1 stack no longer in {version}“ / „{r} stacks no longer in
    {version}“.
- **E10 – Akkordeon.** Ein Stack klappt beim Öffnen auf, wenn er im aktuellen Modus sichtbare eigene Variablen oder
  eine Warnung hat (README „Interaktion“), auf Upgrade Product außerdem, wenn er neu ist (heute
  `upgradeVariables.ts:202-206`; Rahmen `640:8041` zeigt „reporting“ offen). Abweichung vom Rahmen `640:8041`: Dort
  sind identity und api zu, obwohl sie sichtbare Variablen haben; der Plan folgt der Regel der README. Deploy Product
  wendet die Regel nach dem Laden der gespeicherten Werte und der Werte aus dem Precheck an (heute davor:
  `useDeployProductStore.ts:271-288` vor `:290-331`), weil die Warnung vom Wert abhängt. Umschalten ändert das
  Akkordeon nicht (README). Ein Versionswechsel auf Upgrade Product setzt es wie heute neu
  (`useUpgradeProductStore.ts:310-325`), nach dem aktuellen Modus.
- **E11 – Kopf der Stacks nach dem Entwurf.** Neues `Tag` (Neutral, Brand, Danger) statt der runden Pillen
  (`DeployProduct.tsx:589-601`, `UpgradeProduct.tsx:668-689`): „{n} service(s)“ und „{n} variable(s)“ Brand, wobei
  „variable(s)“ die im aktuellen Modus sichtbaren eigenen Variablen zählt und bei 0 entfällt; „Required“ Danger (heute
  „required“), zählt auch ausgeblendete leere Pflichtvariablen; „New“ Brand für neue Stacks. „Existing“ und der grüne
  Rand neuer Stacks (`UpgradeProduct.tsx:645-649`, `:668-676`) entfallen wie im Entwurf. „Required“ auf Upgrade
  Product beachtet künftig gespeicherte Secrets; heute fehlt dort `storedSecretNames` (`UpgradeProduct.tsx:638-640`),
  anders als in der Prüfung des Stores (`useUpgradeProductStore.ts:416-423`).
- **E12 – Ein Hinweis je Abschnitt.** Abschnitte sind die Karte „Environment Variables“ (Stack-Seiten), die Karte
  „Shared Variables“ und jeder Stack im Akkordeon. Der Hinweis steht am Ende des Abschnitts (im Stack vor „Services“),
  nur im Standardmodus und nur, wenn dort etwas ausgeblendet ist; mit leerer ausgeblendeter Pflichtvariable als
  Warnung. Karten und Stack-Inhalt bleiben stehen, auch wenn alles ausgeblendet ist (Entwurf). „No stack-specific
  variables. All variables are shared.“ (`DeployProduct.tsx:640-644`) bleibt für Stacks ganz ohne eigene Variablen.
  „Neu“ heißt auf Upgrade Stack: in `upgradeInfo.newVariables` (wie heute `UpgradeStack.tsx:343`); auf Upgrade Product
  sind alle eigenen Variablen eines neuen Stacks neu (Rahmen `640:8041`: „2 advanced variables hidden, both new“).
  Neue Variablen in bestehenden Stacks kennt Upgrade Product nicht (`CheckProductUpgradeResponse` hat nur
  `newStacks`/`removedStacks`, `deployments.ts:608-622`).
- **E13 – Tag „New“ am Feld nur auf Upgrade Stack**, statt Ring und „New in this version“
  (`UpgradeStack.tsx:342-357`), wie im Entwurf. Auf Upgrade Product trägt der Kopf des neuen Stacks „New“, die Felder
  nicht.
- **E14 – Fehlende Pflichtwerte als Liste.** Die Stores prüfen alle Abschnitte auf einmal (heute: geteilte zuerst,
  dann der erste Stack mit Lücken, `useDeployProductStore.ts:447-466`, `useUpgradeProductStore.ts:407-428`) und setzen
  neben `error` (Text für Distributionen, z. B. „Missing required variables: Admin Email (stack identity)“) das neue
  Feld `missingRequired` (Name, Label, Stack, `isAdvanced`). `@rsgo/ui-generic` zeigt dann ein `Alert` (Ton error,
  `components/ui/Alert.tsx:15-43`) mit Titel und Text aus E9, sonst wie heute den Fehlerkasten. Auf Upgrade Stack
  entfällt dabei der Rollback-Hinweis (`UpgradeStack.tsx:244-257`), denn es wurde noch nichts geändert. Die Seite
  bringt die Meldung in den Blick (`scrollIntoView`, bei `prefers-reduced-motion` ohne Animation). Die README sagt
  „scrollt wie heute“, heute scrollt aber keine der vier Seiten und keiner ihrer Stores zur Meldung: In den Seiten
  scrollt nur `UpgradeStack.tsx:24` ans Ende des Logs, und `ScrollToTop` (`components/common/ScrollToTop.tsx:7-13`)
  wirkt nur beim Wechsel des Pfads.
- **E15 – Bedienung, Fokus, Bewegung (README „Interaktion“).** Der Schalter ist ein `button` mit `role="switch"` und
  `aria-checked`, die ganze Zeile ist Klickfläche, Leertaste und Enter schalten. Eine Live-Region (`aria-live="polite"`)
  meldet das Einschalten. „Show“ schaltet ein und setzt den Fokus auf das erste eingeblendete Feld seines Abschnitts;
  steht beim Ausschalten der Fokus in einem Feld, das verschwindet, geht er auf den Schalter. Eingeblendete Felder
  erscheinen mit höchstens 150 ms (Tailwind `starting:` und `motion-reduce:transition-none`), die Seite scrollt nicht.
- **E16 – Seitenkopf.** Titel und Untertitel bleiben wie heute, der Schalter steht ab `lg` rechts daneben, darunter
  linksbündig unter dem Untertitel (README „Bildschirmgrößen“). Die Rahmen zeigen andere Untertitel („Configure the
  stack and deploy it to Production“) und umrandete Knöpfe; das bleibt wie heute. Karten, die die Rahmen nicht zeigen
  („Target Version“, „Upgrade Configuration“), bleiben.
- **E17 – Info der Upgrade-Seiten als `Alert` (Ton info) über den Karten**, wie im Entwurf, statt der Karten
  „Configuration Changes“ (`UpgradeStack.tsx:289-320`) und „Stack Changes“ (`UpgradeProduct.tsx:527-561`). Im Titel
  steht die gewählte Zielversion. Der Server berechnet neue und entfernte Variablen bzw. Stacks gegen die neueste
  Version (`CheckUpgradeHandler.cs:126-138`, `CheckProductUpgradeHandler.cs:72-92`); die Seite filtert sie deshalb
  gegen die Zielversion: neu nur, was es dort gibt, entfernt nur, was es dort nicht gibt.
- **E18 – Testmanifest unter `stacks/examples/e2e-advanced/`** statt unter
  `src/ReadyStackGo.WebUi/e2e/test-data/stacks/` (Spezifikation). Grund: Der Container nach `CLAUDE.md`
  (`docker compose up -d`) bindet `./stacks:/app/stacks` ein (`docker-compose.yml:24`); `e2e/test-data/stacks/` hängt
  nur `docker-compose.e2e.yml:22` ein, und kein Browsertest nutzt es. Neue Dateien dort ignoriert git außerdem
  (`.gitignore:435` `**/e2e/test-data/`; die Ausnahme in `:437` greift nicht, weil das Elternverzeichnis ausgeschlossen
  ist). So macht es schon `product-deployment.spec.ts:54` mit `/app/stacks/examples/e2e-platform`. Folge: Das Beispiel
  liegt wie `e2e-platform` auch im Image (`Dockerfile:80-81`) und damit in der Quelle „RSGO Embedded Stacks“
  (`source-registry.json:2-14`).
- **E19 – Bausteine.** Neu in `@rsgo/ui-generic`: `Tag` (`components/ui/Tag.tsx`), `AdvancedSwitch` und
  `HiddenVariablesNote` (mit Textknopf „Show“) unter `components/variables/`, Tags der Label-Zeile in den sieben
  Eingaben. Wiederverwendet: `Alert`, `AlertTriangleIcon` (`components/sso/icons.tsx:58`). Nicht wiederverwendet:
  `Toggle` (`FormControls.tsx:111-164`), denn dort steht der Schalter vor dem Label und nur er ist Klickfläche; der
  Entwurf will Label, Zahl, Schalter und die ganze Zeile. Checkbox und Feld bleiben wie heute, an den Eingaben ändert
  sich nur die Label-Zeile (README „Hinweise für die Umsetzung“).
- **E20 – Dokumentation.** Website en/de: Zeile `advanced` in der Tabelle (`reference/manifest-format.md:186-200`) und
  ein Abschnitt „Advanced variables“ mit Beispiel nach „Variable Grouping“ (`:204`); ebenso
  `docs/Reference/Manifest-Schema.md` (`:170-186`). In beiden Tabellen kommt auch `defaultTransient` dazu, das dort
  fehlt (Spezifikation, „Material“). `docs/Concepts/Best-Practices.md`: kurzer Abschnitt unter „Variablen“; in der
  empfohlenen Gruppenreihenfolge (`:205-213`) verweist „7. Advanced“ künftig auf `advanced: true`.
- **E21 – Build-Ausgabe.** `src/ReadyStackGo.Api/wwwroot` ist eingecheckt; die Umsetzung baut neu und committet sie,
  wie die UI-PRs zuvor (z. B. #501).
- **E22 – Werte auf Upgrade Stack wie heute, gespeicherte Werte als eigenes Bug-Issue.** Die Spezifikation nimmt an,
  dass Upgrade Stack gespeicherte Werte mitnimmt. Das tut die Seite heute nicht: Sie legt die Werte aus den Defaults
  der Zielversion und aus `configuration` an (`useUpgradeStackStore.ts:168-185`), doch `configuration` füllt der Server
  nie (`MapDeploymentToResponse`, `DeploymentService.cs:1271-1300`; die Eigenschaft hat nur ihren Initialwert,
  `DeploymentDtos.cs:537`). Sie schickt diese Werte, und beim Server gewinnt der Request über die gespeicherten
  (`UpgradeStackHandler.cs:177-206`). Der Plan ändert daran nichts: Ausgeblendete Variablen gehen auf Upgrade Stack
  mit dem Wert mit, den das Formular hat, genau wie sichtbare (Spezifikation: Ausblenden ändert nur die Anzeige). Ein
  richtiger Fix braucht gespeicherte Werte ohne Secrets in der API und eine Regel, ob gespeicherter Wert oder neuer
  Default gewinnt (Upgrade Product: gespeicherter, `upgradeVariables.ts:116-118`); das sprengt das Vorhaben. Die
  Umsetzung legt dafür ein Bug-Issue an. „Gespeicherter Wert beim Upgrade“ aus der Abnahme belegen die Tests auf
  Upgrade Product (Abschnitt 7).

## 5. Oberflächen

Verbindlich ist der Entwurf `docs/specs/variablen-erweiterter-modus/entwurf/README.md` (Figma-Datei „ReadyStackGo
Design“, Key `RxVNdSKNs7PpJqkYgvb6a1`, Seite „App“). Bezugsgröße Desktop 1440 px, Theme Türkis, hell und dunkel.

| Oberfläche | Zustand | Node-ID | Link | Bild | Was gebaut wird |
|---|---|---|---|---|---|
| Deploy Stack | Standardmodus, 4 ausgeblendet, Gruppe „Versions“ entfällt | `637:6237` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=637-6237) | `deploy-stack-standard.png` | Schalter im Kopf, gefilterte Gruppen, Hinweis |
| Deploy Stack | erweitert, Tags „Advanced“ | `637:6545` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=637-6545) | `deploy-stack-erweitert.png` | alle Felder in alter Reihenfolge, Tags |
| Deploy Product | Standardmodus, 13 ausgeblendet, worker zu, Warnung bei monitoring | `639:6549` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=639-6549) | `deploy-produkt-standard.png` | Hinweise je Abschnitt, Warnung, Akkordeon-Regel, Tags im Kopf |
| Deploy Product | erweitert, alle 18 | `639:6982` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=639-6982) | `deploy-produkt-erweitert.png` | Tags „Advanced“, Zählung „3 variables“ |
| Deploy Product | nach „Deploy“, Pflichtwert ausgeblendet und leer | `639:7536` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=639-7536) | `deploy-produkt-pflichtwert-fehlt.png` | `Alert` mit Titel und Hinweis auf den Schalter (E14) |
| Upgrade Stack | Standardmodus, 2 neue, eine erweitert | `640:7334` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-7334) | `upgrade-stack-standard.png` | Info als `Alert` (E17), „New“ am Feld, Hinweis „…, 1 of them new“ |
| Upgrade Stack | erweitert, „Advanced“ und „New“ | `640:7646` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-7646) | `upgrade-stack-erweitert.png` | beide Tags an einer Variable |
| Upgrade Product | Standardmodus, neuer Stack „reporting“ nur mit erweiterten | `640:8041` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8041) | `upgrade-produkt-neuer-stack.png` | Info als `Alert`, „New“ am Stack, Hinweis „…, both new“ (Abweichung E10) |
| Deploy Stack, dunkel | wie `637:6237` / `637:6545` | `640:8418` / `640:8499` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8418) / [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8499) | `deploy-stack-standard-dunkel.png` / `deploy-stack-erweitert-dunkel.png` | nur semantische Tokens |
| Deploy Product, dunkel | wie `639:6549` / `639:6982` | `640:8586` / `640:8701` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8586) / [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8701) | `deploy-produkt-standard-dunkel.png` / `deploy-produkt-erweitert-dunkel.png` | nur semantische Tokens |
| Upgrade Stack, dunkel | wie `640:7334` | `640:8822` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8822) | `upgrade-stack-standard-dunkel.png` | nur semantische Tokens |

Bilder liegen unter `docs/specs/variablen-erweiterter-modus/entwurf/`. Komponenten (Seite „Components“):

| Komponente | Node-ID | Im Code |
|---|---|---|
| Advanced Switch | `634:270` | `AdvancedSwitch`: Label, Zahl (nur aus), Schalter am Ende; Hover `bg-raised`, Fokus 2 px `focus` |
| Hidden Variables Note | `634:287` | `HiddenVariablesNote`: Neutral (gestrichelt `border-line-strong`, Text `text-fg-secondary`), Warning (`bg-status-degraded-bg`, Rand `status-degraded`, `AlertTriangleIcon` 16 px) |
| Variable Field | `635:323` | heutige Eingaben, Label-Zeile: Label, `*`, Tags, „(default: …)“ |
| Stack Header | `635:396` | Kopf des Akkordeons mit `Tag` (E11) |
| Tag | `633:245` | `Tag`: Neutral (`bg-raised`, `border-line`, `text-fg-secondary`), Brand (`bg-primary-subtle`, `text-fg-brand`), Danger (`bg-status-unhealthy-bg`, `text-status-unhealthy`); 12 px Medium, Innenabstand 2/7, Radius 6 |
| Text Button | `633:252` | „Show“ im Hinweis, 13 px Semi Bold `text-fg-brand`, Hover unterstrichen |
| Checkbox | `633:257` | keine Änderung (E19) |

Die Tokens gibt es schon (`apps/rsgo-generic/src/index.css:180-213`); der Entwurf ändert keine. Raster-Icons: keine
(README „Symbole und Raster-Icons“).

## 6. Schritte der Umsetzung

1. **Domain:** `RsgoVariable.Advanced` (`RsgoVariable.cs`), `Variable.IsAdvanced` mit Konstruktor-Parameter;
   `WithDefaultValue` (`Variable.cs:130-136`) gibt `IsAdvanced` weiter.
2. **Abbildung (E2):** `src/ReadyStackGo.Infrastructure/Parsing/RsgoVariableMapper.cs` mit `ToVariable(name, def)`
   inkl. `defaultTransient` und `isAdvanced`; `RsgoManifestParser.ConvertToVariable` und
   `LocalDirectoryProductSourceProvider.ConvertToVariable` entfallen zugunsten dieser Funktion.
3. **Abfragen und API (E3, E4):** `IsAdvanced = false` in `StackVariableItem` und `StackVariableDetails`; Handler
   `ListStacksHandler`, `ListProductsHandler`, `GetProductHandler` geben `v.IsAdvanced`, `GetStackHandler` dazu
   `v.DefaultTransient`; `StackVariableDto` bekommt `DefaultTransient` und `IsAdvanced`, die vier Endpunkte bilden
   beide ab.
4. **.NET-Tests** (Abschnitt 7), `dotnet build` ohne Warnungen.
5. **`@rsgo/core`, Typ:** `isAdvanced?: boolean` in `StackVariable` (`api/stacks.ts:35-51`).
6. **`@rsgo/core`, Logik (E6–E8, E10–E12, E14):** `lib/advancedVariables.ts` mit `groupVariables`,
   `visibleVariableGroups(variables, showAdvanced)`, `hasAdvancedVariables`, `isMissingRequired(variable, value,
   storedSecretNames?)`, `summarizeSection(variables, values, { showAdvanced, storedSecretNames?, isNew? })` (Anzahl
   ausgeblendet, davon neu, ausgeblendete leere Pflichtvariablen), `countHiddenVariables`,
   `shouldExpandStack(...)` und `collectMissingRequired(...)`/`formatMissingRequiredError(...)`; Export in
   `index.ts`. In `upgradeVariables.ts`: `computeSharedVariables` mit der Regel aus E8, `buildUpgradeVariableState`
   mit Option `showAdvanced` und `shouldExpandStack`.
7. **`@rsgo/core`, Stores (E5, E10, E14):** in allen vier `showAdvanced`, `setShowAdvanced`, `missingRequired`;
   Pflichtprüfung über `collectMissingRequired`; `useDeployProductStore` nutzt `computeSharedVariables` aus
   `upgradeVariables.ts` und setzt `expandedStacks` erst nach den gespeicherten und mitgegebenen Werten.
8. **`@rsgo/ui-generic`, Texte und Bausteine (E9, E19):** `components/variables/advancedVariableTexts.ts`;
   `components/ui/Tag.tsx`; `components/variables/AdvancedSwitch.tsx`; `components/variables/HiddenVariablesNote.tsx`;
   `VariableInputProps` um `isNew?: boolean`; die sieben Eingaben unter `components/variables/inputs/` zeigen nach dem
   `*` die Tags „Advanced“ (aus `variable.isAdvanced`) und „New“ (aus `isNew`).
9. **Seiten** `pages/Deployments/DeployStack.tsx`, `DeployProduct.tsx`, `UpgradeStack.tsx`, `UpgradeProduct.tsx`:
   Kopf mit Schalter (nur bei `hasAdvancedVariables`, E16), Live-Region, Abschnitte über `visibleVariableGroups` mit
   Hinweis am Ende (E12), Fokus (E15), Akkordeon-Kopf mit `Tag` (E11), `Alert` für fehlende Pflichtwerte (E14), Info
   der Upgrade-Seiten (E17), „New“ statt Ring (E13).
10. **Testmanifest (E18):** `stacks/examples/e2e-advanced/` mit einem Single-Stack-Produkt und einem Multi-Stack-Produkt
    (Abschnitt 7), Image `traefik/whoami:v1.11` wie `e2e-platform`.
11. **Browsertest** `src/ReadyStackGo.WebUi/e2e/advanced-variables.spec.ts`, Bilder nach
    `docs/plans/variablen-erweiterter-modus/bilder/` (wie `sso-wysch.spec.ts:20`).
12. **Dokumentation (E20).**
13. **Build-Ausgabe (E21):** `pnpm run build`, `wwwroot` committen.
14. **Bug-Issue (E22)** „Upgrade Stack: form ignores stored values“ anlegen, mit den Stellen aus E22, und im
    Umsetzungs-PR verlinken.

## 7. Tests und Abnahme

**.NET, neu** („rot“ heißt: ohne die Änderung rot):

- `tests/ReadyStackGo.UnitTests/Manifests/RsgoManifestParserTests.cs`: `advanced: true`, `false` und fehlend →
  `IsAdvanced` true/false/false; Multi-Stack: geteilt `advanced: true`, ein Stack-Eintrag definiert sie ohne
  `advanced` neu → in `ExtractStackVariablesAsync` für diesen Stack false, für einen anderen true; umgekehrt geteilt
  ohne, im Stack mit; `advanced` in einem per `include` geladenen Fragment. Rot (Eigenschaft fehlt).
- `tests/ReadyStackGo.UnitTests/Services/LocalDirectoryProductSourceProviderTests.cs`: Multi-Stack-Produkt im
  Temp-Verzeichnis (Muster des Tests ab `:25`), `IsAdvanced` und `DefaultTransient` je Stack an den
  `StackDefinition`s, auch bei Neudefinition im Stack-Eintrag (in beide Richtungen) und für einen per `include`
  geladenen Stack. Rot (Provider verliert beides). Das ist der Weg, den Produkt-Stacks wirklich nehmen (E2).
- `tests/ReadyStackGo.UnitTests/Domain/StackManagement/StackVariableTests.cs`: Default false, gesetzt true,
  `WithDefaultValue` behält `IsAdvanced` und `DefaultTransient`. Rot.
- Neu `tests/ReadyStackGo.UnitTests/Application/Stacks/StackVariableMappingTests.cs`: die vier Handler mit gemocktem
  `IProductSourceService` (Moq, wie `CheckProductUpgradeHandlerTests.cs:16-27`); erweiterte Variable mit
  `defaultTransient` → beide Felder true, einfache Variable → false. Rot (`GetStackHandler` ohne `DefaultTransient`).
- Neu `tests/ReadyStackGo.IntegrationTests/StackVariableFlagsIntegrationTests.cs`: legt eine LocalDirectory-Quelle
  auf ein Temp-Verzeichnis an (`POST /api/stack-sources`, absolute Pfade nimmt der Provider an,
  `LocalDirectoryProductSourceProvider.cs:56-79`), synchronisiert und prüft im JSON von `GET /api/stacks/{id}` und
  `GET /api/products/{id}` die Felder `isAdvanced` und `defaultTransient` (true und false). Rot (API-DTO ohne
  Felder). Das ist der Test, der die Lücke von `defaultTransient` gezeigt hätte.

**Vitest, neu** (nur reine Logik, `.test.ts`):

- `packages/core/src/lib/advancedVariables.test.ts`, rot (Datei fehlt): keine, einige, alle Variablen erweitert;
  Gruppe nur aus erweiterten verschwindet; eine sichtbare Gruppe → keine Überschrift, zwei → Überschriften;
  Reihenfolge der Gruppen gleich in beiden Modi, auch wenn die erste Variable einer Gruppe erweitert ist (E7);
  erweiterter Modus = heutige Gruppierung; Zählung ausgeblendet/neu; Pflichtvariable ohne Default, erweitert und leer
  → im Standardmodus nicht sichtbar, in der Warnung gezählt und in `collectMissingRequired` mit `isAdvanced`;
  gespeichertes Secret gilt nicht als fehlend; Regel des Akkordeons (sichtbar, Warnung, neu, nichts); leere Liste,
  `isAdvanced` fehlt (= false). Umschalten hin und zurück: die Funktionen bekommen die Werte nicht und geben dieselbe
  Gruppierung zurück; die Werte liegen unverändert im Store (Browsertest prüft die Eingabe).
- `packages/core/src/lib/upgradeVariables.test.ts`, ergänzt: geteilte Variable erweitert nur, wenn in allen Stacks
  erweitert (rot: heute zählt der erste Stack); Stack nur mit erweiterten Variablen bleibt zu, neuer Stack geht auf
  (rot: heute geht jeder Stack mit eigenen Variablen auf); gespeicherter Wert einer erweiterten Variable kommt in den
  Werten an, geteilt und stackspezifisch, ein gespeichertes erweitertes Secret bleibt markiert, eine neue erweiterte
  Variable der Zielversion startet mit ihrem Default (Schutz: ohne Änderung grün, nur `tsc -b` fiele ohne das Feld
  im Typ). Das belegt „gespeicherter Wert beim Upgrade“ (E22).
- `packages/ui-generic/src/components/variables/advancedVariableTexts.test.ts`, rot (Datei fehlt): alle Texte aus E9
  mit 1, 2, n, „both“, „all“, mit und ohne Stack, mit und ohne ausgeblendete Variablen, Upgrade-Info mit nur neuen,
  nur entfernten und beidem.

**Browsertest gegen den Container**, `e2e/advanced-variables.spec.ts`, rot (kein Schalter):

- Testmanifest `stacks/examples/e2e-advanced/`: Single-Stack „E2E Advanced Stack“ mit zwei sichtbaren Variablen, einer
  Gruppe nur aus erweiterten (Default), einer erweiterten Pflichtvariable ohne Default (Typ String), festem
  `containerName`; Multi-Stack „E2E Advanced Product“ mit geteilten erweiterten Variablen, einem Stack nur mit
  erweiterten Variablen, einem Stack mit erweiterter Pflichtvariable ohne Default. Eigene Quelle (Id
  `e2e-advanced-source`, Pfad `/app/stacks/examples/e2e-advanced`) wie `product-deployment.spec.ts:40-61`; im
  Standardlauf von Playwright meldet `global-setup.ts:105-109` ohnehin ganz `/app/stacks` als `e2e-test-source` an.
- Deploy Stack: Standardmodus zeigt nur die sichtbaren, „3 hidden“, Gruppe fehlt, Warnung nennt die Pflichtvariable;
  „Deploy“ → `Alert` „Missing required variable“ mit Hinweis, Modus bleibt aus; einschalten → alle Felder in der
  Reihenfolge des Manifests mit „Advanced“; Wert eingeben, aus- und wieder einschalten → Wert steht; ausschalten;
  `.env` mit der Pflichtvariable importieren → Warnung wird zum neutralen Hinweis; im Standardmodus deployen. Belegt
  wird es zweimal: Der abgefangene Request (`page.waitForRequest` auf
  `/api/environments/{env}/stacks/{id}/deploy`, `deployments.ts:130-132`) enthält Default, importierten und
  eingegebenen Wert der ausgeblendeten Variablen, und `docker inspect` des Containers zeigt sie in seiner Umgebung.
  Die API für Deployments taugt dafür nicht, `configuration` bleibt leer (E22). Danach aufräumen.
- Deploy Product: Standardmodus, Stack nur mit erweiterten zu und ohne „variables“-Tag, Stack mit Warnung offen mit
  „Required“; „Show“ im Hinweis schaltet ein und setzt den Fokus ins erste eingeblendete Feld; Tastatur: Leertaste am
  Schalter.
- Upgrade Stack und Upgrade Product mit `page.route`-Stubs (Muster `product-release-notes.spec.ts:103-139`): Info
  nennt die neue erweiterte Variable bzw. den neuen Stack, Hinweis „…, 1 of them new“ / „…, both new“. Upgrade
  Product: Der abgefangene Request enthält den gespeicherten Wert einer ausgeblendeten Variable aus dem gestubbten
  Product Deployment. Upgrade Stack: Der Request enthält den Wert, den das Formular für die ausgeblendete Variable hat
  (Default der Zielversion bzw. im erweiterten Modus eingegeben), wie für sichtbare (E22).
- `e2e-platform` (ohne `advanced`) auf Deploy Product: kein Schalter, keine Hinweise.
- Bestehende Specs bleiben grün, besonders `product-deployment.spec.ts` (Tags im Akkordeon-Kopf geändert, E11).

**Bilder im Umsetzungs-PR** (neben den Rahmen des Entwurfs): Deploy Stack Standard und erweitert (hell, dunkel),
Deploy Product Standard (hell, dunkel) und erweitert (hell), Fehler nach „Deploy“, Upgrade Stack Standard (hell,
dunkel) und erweitert, Upgrade Product mit neuem Stack, Deploy Product ohne `advanced`.

**Abnahme** wie in der Spezifikation; dazu „Build & Test“ (`.github/workflows/ci.yml`) und die Browsertests gegen den
Container nach `CLAUDE.md`.

## 8. Wo umgesetzt wird

GitHub-Runner (`ubuntu-latest`). Docker ist dort vorhanden, also laufen auch der Container und die Browsertests mit
echtem Deployment dort.

## 9. Risiken

- **Verhalten bei `defaultTransient` (E4):** Manifeste mit `defaultTransient: true` starten künftig ohne Haken bei
  „Save value“. Nicht nachgelesen: welche externen Manifeste (rsgo-community-stacks, private Distributionen) das
  Kennzeichen nutzen; im Repo nutzt es keines. Annahme: Ihre Autoren wollten genau dieses Verhalten.
- **Distributionen:** Nicht nachgelesen (private Repos): ob eine Distribution die Rückgabetypen der Stores selbst
  implementiert oder den Text von `error` auswertet. Annahme: Sie nutzen die Stores; neue Felder brechen nichts, der
  Text der Pflicht-Meldung ändert sich (E14).
- **Ausgeblendete Pflichtvariablen:** Ein Autor kann eine Pflichtvariable ohne Default als erweitert markieren
  (gewollt, Spezifikation). Warnung im Abschnitt, „Required“ am Stack und die Meldung nach „Deploy“ fangen das ab.
- **Feldzustand beim Ausblenden:** Ausgeblendete Felder werden nicht gerendert; ihr eigener UI-Zustand (z. B.
  „Passwort zeigen“ in `PasswordInput.tsx:9`) geht beim Umschalten verloren, die Werte nicht (sie liegen im Store).
- **Fokus:** Die Labels der Eingaben sind nicht mit dem Feld verknüpft (`StringInput.tsx:36` ohne `htmlFor`); der Fokus
  geht auf das erste fokussierbare Element im Feld.
- **Bewegung:** Nicht nachgelesen (Bibliothek): die Variante `starting:` (`@starting-style`) in Tailwind 4.2.1
  (`pnpm-lock.yaml:2075`). Annahme: vorhanden; sonst erscheinen die Felder ohne Bewegung, was der Entwurf erlaubt.
- **Upgrade-Info (E17):** Was in der Zielversion neu, in der neuesten aber wieder entfernt ist, markiert die Seite
  nicht, weil der Server nur gegen die neueste Version vergleicht.
- **Browsertest mit echtem Deployment:** braucht Docker und `traefik/whoami:v1.11`; Port des Testmanifests (8094)
  darf nicht mit `e2e-platform` (8091, 8092) kollidieren.
- **Testbeispiel im Image (E18):** erscheint in „RSGO Embedded Stacks“ wie `e2e-platform`.
- **Upgrade Stack (E22):** Bis zum Bug-Issue setzt Upgrade Stack gespeicherte Werte von Variablen mit Default auf den
  Default der Zielversion zurück, ausgeblendet oder nicht. Mit dem erweiterten Modus sieht der Benutzer das bei
  ausgeblendeten Variablen nicht einmal mehr im Formular. Das Verhalten ist nicht neu, wird aber unauffälliger.

## 10. Offene Fragen

Keine. Über die Liste der Spezifikation hinaus gehen E3 und E4 (GetStack, API-DTO, Reparatur von `defaultTransient`);
E18 weicht beim Ort des Testmanifests ab, E22 bei der Annahme, Upgrade Stack nehme gespeicherte Werte mit. Mit der
Freigabe des Plans sind sie entschieden.
