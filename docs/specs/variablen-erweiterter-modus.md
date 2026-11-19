# Erweiterter Modus für Deployment-Variablen

## Ziel

Beim Deployen und beim Upgrade zeigt ReadyStackGo heute jede Variable eines Stacks oder Produkts. Bei Produkten mit
vielen Variablen (40 und mehr, die meisten mit passendem Default) sucht der Benutzer die wenigen, die er wirklich
setzen muss, zwischen Ports, Timeouts und Tuning-Werten. Künftig zeigen die Seiten standardmäßig nur die wichtigen
Variablen. Alle übrigen erscheinen erst, wenn der Benutzer den erweiterten Modus einschaltet. Welche Variable zum
erweiterten Modus gehört, legt der Autor des Manifests mit einem Kennzeichen fest (Marcus, 18.11.2026).

## Was dazugehört

- **Kennzeichen im Manifest.** Eine Variable bekommt die Eigenschaft `advanced` (boolean, Default `false`). Sie gilt
  überall, wo Variablen definiert werden: `variables:` eines Stacks, `variables:` eines Stack-Eintrags im Produkt und
  `sharedVariables:` des Produkts. Definiert ein Stack-Eintrag eine geteilte Variable neu, gilt wie bei allen anderen
  Eigenschaften seine Definition (heutige Regel in `RsgoManifestParser`: „stack-specific overrides shared“).
- **Durchgereicht bis zur Oberfläche**, wie heute `defaultTransient`: `RsgoVariable`, `Variable` (Domain), die DTOs
  der Abfragen (`GetProduct`, `ListProducts`, `ListStacks`) und der Typ `StackVariable` in `@rsgo/core`.
- **Schalter auf vier Seiten:** Deploy Stack, Deploy Product, Upgrade Stack und Upgrade Product. Ein Schalter je Seite
  (Text siehe „Vorgaben“), er wirkt auf alle Variablen der Seite zugleich, geteilte und stackspezifische.
  - **Standardmodus (aus):** Variablen mit `advanced: true` sind ausgeblendet.
  - **Erweiterter Modus (ein):** alle Variablen, in Gruppen und Reihenfolge wie heute.
- **Gruppen.** Gruppen und ihre Überschriften richten sich nach den sichtbaren Variablen. Eine Gruppe, deren
  Variablen alle erweitert sind, verschwindet im Standardmodus samt Überschrift. Die heutige Regel, dass eine einzelne
  Gruppe keine Überschrift bekommt, gilt für die sichtbaren Gruppen.
- **Werte bleiben, Ausblenden ändert nur die Anzeige.** Ausgeblendete Variablen gehen beim Deployen mit dem Wert mit,
  den sie haben: Default, gespeicherter Wert aus dem letzten Deployment (Upgrade), Wert aus einem `.env`-Import oder im
  erweiterten Modus eingegeben. Umschalten verwirft keine Eingabe. Dasselbe gilt für die Einstellung „Save value“
  einer ausgeblendeten Variable.
- **Ohne erweiterte Variablen kein Schalter.** Trägt keine Variable der Seite `advanced: true`, sieht die Seite aus
  wie heute.
- **Dokumentation:** Website en/de (`reference/manifest-format.md`: Tabelle der Variablen-Eigenschaften und ein
  Abschnitt mit Beispiel), `docs/Reference/Manifest-Schema.md` und in `docs/Concepts/Best-Practices.md` ein kurzer
  Hinweis, welche Variablen ins erweiterte gehören (passender Default, selten geändert).

## Was ausdrücklich nicht dazugehört

- Ansichten ohne Eingabe: Product Deployment Detail mit den Karten „Shared Variables“ und „Stack Variables“ und das
  Deployment-Detail zeigen weiter alle Variablen.
- Deployen über die API, über Hooks (CI/CD) oder über andere Clients: Das Kennzeichen wirkt nur in der Oberfläche,
  der Server behandelt erweiterte Variablen wie alle anderen.
- Ein Kennzeichen auf Ebene der Gruppe, Bedingungen („zeige X, wenn Y = …“) oder eine Sicht abhängig von Rolle oder
  Benutzer.
- Bestehende Manifeste umstellen, etwa Variablen in einer Gruppe „Advanced“ auf das neue Kennzeichen. Das machen die
  Autoren der Manifeste selbst.
- Die Oberflächen von Distributionen mit eigenen Deploy- und Upgrade-Seiten. Sie bekommen das Feld über `@rsgo/core`
  und ziehen in einem eigenen Schritt nach.

## Vorgaben

- **Name des Kennzeichens:** `advanced`. Manifeste ohne `advanced` verhalten sich wie heute, das neue Feld im DTO ist
  additiv.
- **Pflichtvariablen ohne Default sind kein Sonderfall** (Marcus, 18.11.2026). Trägt eine Pflichtvariable ohne
  Default `advanced: true`, bleibt sie im Standardmodus ausgeblendet. Das Manifest wird deshalb weder abgelehnt noch
  gewarnt. Die heutige Prüfung beim Deployen bleibt: Fehlt der Wert, meldet die Seite „Missing required variables: …“
  (`useDeployStackStore`, `useDeployProductStore`).
- **Abweichende Werte sind kein Sonderfall** (Marcus, 18.11.2026). Eine erweiterte Variable, deren Wert vom Default
  abweicht (gespeichert, importiert), ist im Standardmodus ausgeblendet wie jede andere erweiterte Variable, ohne
  eigenen Hinweis.
- **Texte der Oberfläche englisch** (bis zur Lokalisierung vor 1.0, `CLAUDE.md`). Vorschlag für den Schalter:
  „Show advanced settings“.
- **Tests** nach `CLAUDE.md`, besonders:
  - Parser: `advanced: true`, `false`, fehlt; Neudefinition einer geteilten Variable im Stack-Eintrag mit anderem
    `advanced`; das Feld kommt in den DTOs an.
  - Oberfläche (Unit): keine, einige und alle Variablen erweitert; Gruppe nur aus erweiterten Variablen; einzelne
    sichtbare Gruppe ohne Überschrift; Umschalten hin und zurück verliert keine Eingabe.
  - Werte: Default, `.env`-Import und gespeicherter Wert einer ausgeblendeten Variable kommen im Deployment an.
  - Pflichtvariable ohne Default, erweitert und leer: im Standardmodus nicht sichtbar, Deployen meldet sie als
    fehlend.
  - Upgrade: neue Variable mit `advanced: true` in der Zielversion.
  - Browsertest gegen den Container mit einem Testmanifest unter `src/ReadyStackGo.WebUi/e2e/test-data/stacks/`:
    Standardmodus, Umschalten, Deployen mit ausgeblendetem Wert.

## Oberfläche und Bilder

Verbindlich: Entwurf in `docs/specs/variablen-erweiterter-modus/entwurf/` (freigegeben mit dem Merge von PR #PR_NUMMER).
Er zeigt Deploy Stack, Deploy Product und Upgrade Stack hell und dunkel, Upgrade Product und den Fehler nach „Deploy“
hell. Der Schalter sitzt im Seitenkopf; Hinweise, Badges, Tags und Texte beschreibt die README des Entwurfs.

Richtung: die bestehenden Karten, Gruppenüberschriften und Badges der vier Seiten.

## Abnahme

- Ein Stack mit einigen Variablen `advanced: true`: Deploy Stack zeigt im Standardmodus nur die übrigen; nach dem
  Einschalten alle, in Gruppen und Reihenfolge wie vorher.
- Dasselbe auf Deploy Product (geteilte und stackspezifische Variablen), Upgrade Stack und Upgrade Product.
- Ein Deployment im Standardmodus übernimmt die Werte der ausgeblendeten Variablen (Default, `.env`-Import,
  gespeicherter Wert beim Upgrade); Tests belegen es.
- Ein Manifest ohne `advanced`: keine sichtbare Änderung, kein Schalter.
- Parser- und Oberflächentests aus „Vorgaben“ sind grün; der Browsertest spielt Umschalten und Deployen durch.
- Website en/de und `docs/Reference/Manifest-Schema.md` beschreiben `advanced` mit Beispiel.

## Material

- Vorbild für ein Kennzeichen vom Manifest bis zur Oberfläche: `defaultTransient`, Plan
  `docs/plans/PLAN-transient-variables.md`. In der Tabelle der Website fehlt `defaultTransient` bis heute.
- Manifest und Parser: `src/ReadyStackGo.Domain/StackManagement/Manifests/RsgoVariable.cs`,
  `src/ReadyStackGo.Domain/StackManagement/Stacks/Variable.cs`,
  `src/ReadyStackGo.Infrastructure/Parsing/RsgoManifestParser.cs` (`ConvertToVariable`,
  `ExtractStackVariablesAsync`).
- DTOs: `src/ReadyStackGo.Application/UseCases/Stacks/GetProduct/GetProductQuery.cs`,
  `ListStacks/ListStacksQuery.cs`, `ListProducts/ListProductsHandler.cs`; Typ `StackVariable` in
  `src/ReadyStackGo.WebUi/packages/core/src/api/stacks.ts`.
- Oberfläche: `groupVariables` in `packages/ui-generic/src/components/variables/VariableInput.tsx`; die Seiten
  `DeployStack.tsx`, `DeployProduct.tsx`, `UpgradeStack.tsx`, `UpgradeProduct.tsx` unter
  `packages/ui-generic/src/pages/Deployments/`.
- Stores: `useDeployStackStore.ts` und `useDeployProductStore.ts` (Prüfung „Missing required variables“; Deploy
  Product klappt jeden Stack mit eigenen Variablen auf), `useUpgradeStackStore.ts`,
  `useUpgradeProductStore.ts`, `packages/core/src/lib/upgradeVariables.ts`.

## Offene Fragen

- **Zustand des Schalters.** Startet jede Seite im Standardmodus, oder merkt sich der Browser die Wahl (pro Seite oder
  für alle)? Vorschlag: jede Seite startet im Standardmodus, nichts wird gemerkt.
- **Zahl am Schalter.** Nennt der Schalter, wie viele Variablen ausgeblendet sind („Show advanced settings (12)“)?
  Vorschlag: ja, eine einfache Zahl ohne Unterscheidung nach geänderten Werten.
- **Badges im Akkordeon von Deploy Product und Upgrade Product.** „N variables“ zählt die sichtbaren Variablen des
  Modus. „required“ berücksichtigt auch ausgeblendete Pflichtvariablen ohne Wert, weil es einen Zustand meldet, den
  sonst erst der Fehler beim Deployen zeigt. Deploy Product klappt heute jeden Stack mit eigenen Variablen auf; ein
  Stack, dessen Variablen im Standardmodus alle ausgeblendet sind, bleibt zu. Vorschlag: so.
- **Meldung bei fehlender ausgeblendeter Pflichtvariable.** Nennt „Missing required variables: …“ den erweiterten
  Modus? Vorschlag: ja, als Hinweis im Text, ohne den Modus selbst einzuschalten.
- **Neue Variablen beim Upgrade.** Upgrade Stack listet neue Variablen der Zielversion in einer Info-Box und rahmt
  sie im Formular. Vorschlag: Die Info-Box nennt auch neue erweiterte Variablen, im Formular sind sie im
  Standardmodus ausgeblendet wie jede erweiterte Variable.
