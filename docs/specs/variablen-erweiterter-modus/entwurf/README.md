# Entwurf: Erweiterter Modus für Deployment-Variablen

- **Vorhaben:** #522
- **Spezifikation:** [`docs/specs/variablen-erweiterter-modus.md`](../../variablen-erweiterter-modus.md)
- **Figma-Datei:** [ReadyStackGo Design](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1) (`RxVNdSKNs7PpJqkYgvb6a1`), Seiten „App“ (`2:99`) und „Components“ (`2:98`)
- **Freigabe:** freigegeben mit dem Merge von PR #523
- **Grundlage:** App-Rahmen mit Seitenleiste und Kopfzeile aus „App / Profile – no local password“ (`177:4834`, Vorhaben #498). Die Seiten selbst gab es in Figma noch nicht; sie sind nach dem heutigen Aufbau von `DeployStack.tsx`, `DeployProduct.tsx`, `UpgradeStack.tsx` und `UpgradeProduct.tsx` gebaut (Hauptspalte mit Karten, Seitenleiste mit Aktionen und Info), mit den Komponenten der Datei.
- **Inhalte:** Die Stack-Seiten zeigen den öffentlichen WordPress-Stack aus `Wiesenwischer/rsgo-community-stacks` (7 Variablen, davon hier Database Name, Database User, WordPress Version und MySQL Version als `advanced: true`). Die Produkt-Seiten zeigen ein Beispielprodukt „Order Platform“ mit 4 Stacks und 18 Variablen.

## Rahmen

Ganze Seite, 1440 breit, Theme Türkis. Abschnitt auf der Seite „App“ unter der Überschrift `640:8416`, dunkel unter `640:8417`.

| Oberfläche | Zustand | Node-ID | Link | Bild |
|---|---|---|---|---|
| Deploy Stack | Standardmodus: 4 erweiterte Variablen ausgeblendet, Gruppe „Versions“ entfällt | `637:6237` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=637-6237) | `deploy-stack-standard.png` |
| Deploy Stack | erweiterter Modus: alle 7 Variablen, erweiterte mit Tag „Advanced“ | `637:6545` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=637-6545) | `deploy-stack-erweitert.png` |
| Deploy Product | Standardmodus: 13 ausgeblendet; Stack ohne sichtbare Variable (worker) zu; ausgeblendete Pflichtvariable ohne Wert (monitoring) als Warnung | `639:6549` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=639-6549) | `deploy-produkt-standard.png` |
| Deploy Product | erweiterter Modus: alle 18 Variablen, die leere Pflichtvariable sichtbar | `639:6982` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=639-6982) | `deploy-produkt-erweitert.png` |
| Deploy Product | nach „Deploy“ im Standardmodus: Pflichtvariable ausgeblendet und leer | `639:7536` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=639-7536) | `deploy-produkt-pflichtwert-fehlt.png` |
| Upgrade Stack | Standardmodus, 6.9.1 auf 7.0.0 mit 2 neuen Variablen, eine davon erweitert | `640:7334` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-7334) | `upgrade-stack-standard.png` |
| Upgrade Stack | erweiterter Modus, neue erweiterte Variable mit „Advanced“ und „New“ | `640:7646` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-7646) | `upgrade-stack-erweitert.png` |
| Upgrade Product | Standardmodus, neuer Stack „reporting“, dessen Variablen alle erweitert sind | `640:8041` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8041) | `upgrade-produkt-neuer-stack.png` |
| Deploy Stack (dunkel) | wie `637:6237` | `640:8418` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8418) | `deploy-stack-standard-dunkel.png` |
| Deploy Stack (dunkel) | wie `637:6545` | `640:8499` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8499) | `deploy-stack-erweitert-dunkel.png` |
| Deploy Product (dunkel) | wie `639:6549` | `640:8586` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8586) | `deploy-produkt-standard-dunkel.png` |
| Deploy Product (dunkel) | wie `639:6982` | `640:8701` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8701) | `deploy-produkt-erweitert-dunkel.png` |
| Upgrade Stack (dunkel) | wie `640:7334` | `640:8822` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=640-8822) | `upgrade-stack-standard-dunkel.png` |

Die dunklen Rahmen setzen nur den Modus „Turquoise Dark“ (`2:2`) der Sammlung „Theme“, wie `177:5913`.

## Komponenten

Neu auf der Seite „Components“, alle Farben an Variablen der Sammlung „Theme“ gebunden:

| Komponente | Node-ID | Varianten und Eigenschaften | Bild |
|---|---|---|---|
| Advanced Switch | `634:270` | `Value=Off` × `State=Default` `634:240`, `Hover` `634:245`, `Focus` `634:250`; `Value=On` × `Default` `634:255`, `Hover` `634:260`, `Focus` `634:265`. Text `Label`, `Count`. Reihenfolge Label, Zahl, `Toggle` (`170:276`) am Ende; Zahl nur bei `Off`. Innenabstand 7/8/7/10, Abstand 10, Radius 10; Hover Fläche `bg/raised`, Fokus 2 px `focus/ring` | `komponente-schalter.png` |
| Hidden Variables Note | `634:287` | `Tone=Neutral` `634:271`: gestrichelter Rand 1 px `border/strong` (4/3), ohne Fläche, Text 13 `text/secondary`. `Tone=Warning` `634:279`: Fläche `status/degraded-bg`, Rand `status/degraded`, `Icon/Alert Triangle` 16 px, Text 13 Medium `text/primary`. Text `Message`, rechts `Text Button` „Show“. Innenabstand 9/10/9/16, Radius 12, volle Breite | `komponente-hinweis.png` |
| Variable Field | `635:323` | `State=Default` `635:251`, `Focus` `635:269`, `Error` `635:287`, `Empty` `635:305`. Text `Label`, `Value`, `Default`, `Description`, `Error`; Schalter `Required`, `Show default`, `Show description`, `Advanced`, `New`, `Save value`. Aufbau wie `VariableInput` heute: Label-Zeile (Label, `*`, Tags, „(default: …)“), Beschreibung, Feld 44 hoch Radius 10, Fehler, „Save value“ | `komponente-variablenfeld.png` |
| Stack Header | `635:396` | `Expanded=No` × `State=Default` `635:324`, `Hover` `635:336`, `Focus` `635:348`; `Expanded=Yes` × `Default` `635:360`, `Hover` `635:372`, `Focus` `635:384`. Text `Name`; Schalter `Show variables`, `Show required`, `Show new`; Tags „N services“, „N variables“, „Required“, „New“ | `komponente-stack-kopf.png` |
| Tag | `633:245` | `Tone=Neutral` `633:239` (Fläche `bg/raised`, Rand `border/default`, Text `text/secondary`), `Brand` `633:241` (`primary/subtle`, `text/brand`), `Danger` `633:243` (`status/unhealthy-bg`, `status/unhealthy`). Text `Label`, 12 Medium, Innenabstand 2/7, Radius 6 | `komponente-tag.png` |
| Text Button | `633:252` | `State=Default` `633:246`, `Hover` `633:248` (unterstrichen), `Focus` `633:250` (2 px `focus/ring`). Text `Label`, 13 Semi Bold `text/brand` | `komponente-textknopf.png` |
| Checkbox | `633:257` | `State=Checked` `633:253`, `Unchecked` `633:256`; 16 px, Radius 4 | `komponente-checkbox.png` |

Wiederverwendet: `Toggle` (`170:276`), `Button` (`5:52`, Primary und Secondary), `Alert` (`169:309`, Info und Error), `Icon/Chevron Down` (`3:69`), `Icon/Alert Triangle` (`168:191`), `Icon/Check` (`168:224`), `Nav Item` (`4:52`, Active), App-Rahmen aus `177:4834`.

## Geänderte Tokens und Variablen

Keine.

## Symbole und Raster-Icons

Keine neuen. Verwendet werden nur Symbole aus dem Rahmen „Icons“ (`3:2`).

## Texte (englisch)

- Schalter: „Show advanced variables“, im Standardmodus daneben „{n} hidden“ (alle ausgeblendeten Variablen der Seite).
- Hinweis am Ende eines Abschnitts: „1 advanced variable hidden“, „{n} advanced variables hidden“. Auf Upgrade-Seiten mit neuen darunter: „{n} advanced variables hidden, {k} of them new“, bei zwei neuen von zwei „…, both new“, bei allen „…, all new“, bei einer einzigen „1 new advanced variable hidden“. Knopf „Show“.
- Warnung am Ende eines Abschnitts, wenn dort eine ausgeblendete Pflichtvariable leer ist: „{Label} is required and has no value“, bei mehreren „{k} required advanced variables have no value“. Knopf „Show“.
- Fehler nach „Deploy“: Titel „Missing required variable“, Text „{Label} (stack {stack}) has no value. It is an advanced variable: turn on “Show advanced variables” to enter it.“ Bei geteilten Variablen ohne „(stack …)“; bei mehreren „Missing required variables“ mit Aufzählung der Labels.
- Tags: „Advanced“, „New“, „Required“, „{n} variable(s)“, „{n} service(s)“.
- Upgrade-Info: „{k} new variable(s) in {version}“, Text nennt die Labels und welche davon erweitert sind; bei neuem Stack „New stack in {version}: {name}“.

## Interaktion und Navigation

- **Schalter:** ein Element mit `role="switch"` und `aria-checked`; die ganze Zeile (Label, Zahl, Toggle) ist Klickfläche, Leertaste und Enter schalten. Die Zahl hängt per `aria-describedby` am Schalter („4 advanced variables hidden“). Jede Seite öffnet im Standardmodus (aus).
- **Einschalten:** Die ausgeblendeten Felder erscheinen an ihrer Stelle in Gruppe und Reihenfolge, die Seite scrollt nicht. Eine `aria-live`-Region meldet „{n} advanced variables shown“. Bewegung nur als kurzes Aufklappen (höchstens 150 ms), bei `prefers-reduced-motion` ohne.
- **„Show“ im Hinweis:** schaltet denselben Seitenschalter ein (alle Abschnitte zugleich, wie in der Spezifikation) und setzt den Fokus auf das erste eingeblendete Feld dieses Abschnitts.
- **Ausschalten:** Werte bleiben. Steht der Fokus in einem Feld, das verschwindet, geht er auf den Schalter.
- **Akkordeon auf den Produkt-Seiten:** Ein Stack klappt beim Öffnen der Seite auf, wenn er im aktuellen Modus sichtbare Variablen oder eine Warnung hat; sonst bleibt er zu (heute klappt jeder Stack mit eigenen Variablen auf). Beim Umschalten bleibt der Zustand der Stacks, wie der Benutzer ihn gesetzt hat.
- **Fehler nach „Deploy“:** Die Seite scrollt wie heute nach oben zur Fehlermeldung; der Schalter steht daneben im Kopf. Die Seite schaltet den erweiterten Modus nicht selbst ein.
- **Eingabegeräte:** Maus und Tastatur. Fokusreihenfolge: Schalter im Seitenkopf, dann die Karten von oben nach unten, im Abschnitt die Felder, dann „Show“; in der Seitenleiste die Aktionen.
- **Bildschirmgrößen:** Entwurf in 1440 (Desktop). Unter `lg` stapeln sich Haupt- und Seitenleiste wie heute; der Schalter rutscht dann im Seitenkopf unter den Untertitel, linksbündig, und bleibt vor den Karten.

## Festlegungen ohne Vorgabe der Spezifikation

Marcus hat die Gestaltung im Chat am 19.11.2026 an den Entwurf übergeben („du bist der designer“). Diese Punkte legt der Entwurf fest; sie beantworten auch die offenen Fragen der Spezifikation zur Gestalt:

- **Ort des Schalters:** rechts im Seitenkopf, auf allen vier Seiten an derselben Stelle. Er wirkt auf die ganze Seite und gehört deshalb nicht in eine einzelne Karte; im Kopf ist er beim Öffnen sofort zu sehen und steht neben einer Fehlermeldung, die auf ihn verweist.
- **Schalter am rechten Ende:** Label und Zahl stehen vor dem Toggle. Verschwindet die Zahl beim Einschalten, bleibt der Toggle an seiner Stelle unter dem Mauszeiger.
- **Zahl am Schalter:** ja, gedämpft „{n} hidden“, nur im Standardmodus, ohne Unterscheidung nach geänderten Werten (Spezifikation, „Abweichende Werte sind kein Sonderfall“).
- **Hinweis am Inhalt:** Am Ende jedes Abschnitts mit ausgeblendeten Variablen steht eine gestrichelte Zeile. Der gestrichelte Rand zeigt, dass hier etwas eingeklappt ist; sie bleibt auch stehen, wenn der Abschnitt im Standardmodus sonst leer ist, damit die Seite beim Umschalten nicht springt (Karte „Shared Variables“ und Stacks bleiben).
- **Warnung statt Stille bei leeren Pflichtvariablen:** Die Variable bleibt ausgeblendet (Entscheidung Marcus, 18.11.2026). Der Hinweis ihres Abschnitts wird zur Warnung und nennt sie, das Tag „Required“ am Stack zählt sie mit. So erfährt der Benutzer davon vor dem Deployen, nicht erst durch den Fehler.
- **Badges im Akkordeon:** „{n} variables“ zählt die im aktuellen Modus sichtbaren Variablen und entfällt bei null; „Required“ berücksichtigt auch ausgeblendete Pflichtvariablen ohne Wert (Vorschlag der Spezifikation).
- **Tag „Advanced“:** im erweiterten Modus an jeder erweiterten Variable, damit erkennbar ist, was der Standardmodus weglässt. Neutral und rechteckig, damit es nicht mit den runden Status-Badges verwechselt wird.
- **Neue Variablen beim Upgrade:** Tag „New“ an der Label-Zeile statt des heutigen Rings um das Feld, gleich gebaut wie „Advanced“. Die Info-Box nennt auch neue erweiterte Variablen; im Formular sind sie im Standardmodus ausgeblendet, der Hinweis zählt sie („…, 1 of them new“) (Vorschlag der Spezifikation).
- **Gruppenüberschriften** bleiben wie heute (Großbuchstaben, gesperrt, Linie darunter) und richten sich nach den sichtbaren Variablen.

## Kontrast

Gerechnet für „Turquoise Light“ (`2:1`) und „Turquoise Dark“ (`2:2`), WCAG 2.2:

| Verwendung | Paar | hell | dunkel |
|---|---|---|---|
| Zahl am Schalter, Beschreibungen | `text/muted` auf `bg/surface` | 5,41 | 6,85 |
| Text des Hinweises | `text/secondary` auf `bg/surface` | 7,20 | 12,53 |
| „Show“ im Hinweis | `text/brand` auf `bg/surface` | 5,29 | 11,15 |
| „Show“ in der Warnung | `text/brand` auf `status/degraded-bg` | 4,69 | 10,37 |
| Text der Warnung | `text/primary` auf `status/degraded-bg` | 16,11 | 15,34 |
| Tag „Advanced“ | `text/secondary` auf `bg/raised` | 6,73 | 11,65 |
| Tag „New“ | `text/brand` auf `primary/subtle` | 4,94 | 9,27 |
| Tag „Required“ | `status/unhealthy` auf `status/unhealthy-bg` | 5,35 | 6,78 |
| gestrichelter Rand, Toggle aus (Nicht-Text) | `border/strong` auf `bg/surface` | 3,17 | 3,75 |
| Fokusring (Nicht-Text) | `focus/ring` auf `bg/surface` | 3,78 | 7,57 |

Alles erfüllt AA (Text ≥ 4,5, Nicht-Text ≥ 3). AAA (7 : 1) erreicht im hellen Modus nur der Hinweistext und die Warnung; gedämpfter Text und `text/brand` liegen bei 4,7 bis 5,4. Das sind die bestehenden Tokens der App; der Entwurf ändert sie nicht (siehe „Offene Fragen“).

## Hinweise für die Umsetzung

- Nichts im Designsystem des Codes nachzuziehen: keine Tokens geändert. Die neuen Figma-Komponenten entsprechen neuen Bausteinen in `@rsgo/ui-generic` (Schalter, Hinweis, Tag) und Änderungen an `VariableInput` (Tags in der Label-Zeile) und an den vier Seiten.
- Die Felder sind wie `VariableInput` heute aufgebaut; geändert ist nur die Label-Zeile (Tags). Passwortfelder mit Default zeigen weiter „(has default)“ wie in `PasswordInput`; die Rahmen lassen den Hinweis bei den Passwörtern weg, er ändert sich nicht.
- Upgrade Stack: Der Ring um neue Variablen entfällt zugunsten des Tags „New“.
- Distributionen mit eigenen Deploy- und Upgrade-Seiten haben eigene Kopien der vier Seiten und ziehen getrennt nach (Spezifikation, „Was ausdrücklich nicht dazugehört“).
- Name, Version, Labels und Werte in den Rahmen sind Beispiele und kommen zur Laufzeit aus Manifest und Deployment.

## Offene Fragen

- **Zustand des Schalters merken:** Der Entwurf zeigt jede Seite beim Öffnen im Standardmodus. Ob der Browser die Wahl danach merkt, ist keine Frage der Gestalt und bleibt beim Plan (Vorschlag der Spezifikation: nicht merken).
- **AAA-Kontrast im hellen Modus:** Soll gedämpfter Text (`text/muted`) und `text/brand` app-weit auf 7 : 1 angehoben werden? Das wäre eine Token-Änderung für die ganze App und gehört in ein eigenes Vorhaben.
