# Entwurf: Anmeldung mit WYSCH und Vorlagen für Identity Provider

- **Vorhaben:** #498 (Wiesenwischer/ReadyStackGo), Spezifikation `docs/specs/identity-provider-vorlagen.md`
- **Figma-Datei:** „ReadyStackGo Design“, Key `RxVNdSKNs7PpJqkYgvb6a1` (aus `works/products/readystackgo.md`)
- **Seiten:** „App“ (`2:99`) für alle Rahmen, „Components“ (`2:98`) für die neuen Komponenten
- **Freigabe:** freigegeben mit dem Merge von PR #499
- **Gegenstück:** Wiesenwischer/WYSCH#170 (Spezifikation `docs/specs/client-kopplung.md` dort, Bestätigungsseite bei
  WYSCH); Entwurf dort: Wiesenwischer/WYSCH#171

Alle Rahmen sind im Theme Türkis gebaut (Sammlung „Theme“, Modus „Turquoise Light“ `2:1`, dunkel „Turquoise Dark“
`2:2`) und binden ausschließlich vorhandene Variablen. Die dunklen Rahmen sind Kopien der hellen mit umgeschaltetem
Modus; jeder Rahmen gilt hell und dunkel.

## Rahmen

Link-Muster: `https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=<a>-<b>`

### 1. Wizard (Ersteinrichtung)

| Oberfläche | Zustand | Node-ID | Link | Bild |
|---|---|---|---|---|
| Wizard, Schritt 1 „How do you want to sign in?“ | nichts gewählt, „Continue“ gesperrt | `171:2417` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=171-2417) | `wizard-anmeldeart-leer.png` |
| Wizard, Schritt 1 | WYSCH gewählt | `171:2534` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=171-2534) | `wizard-anmeldeart-wysch.png` |
| Wizard, Schritt 1 (dunkel) | WYSCH gewählt | `177:5565` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5565) | `wizard-anmeldeart-wysch-dunkel.png` |
| Wizard, WYSCH-Weg | Adresse bestätigen, „Connect with WYSCH“ | `171:2624` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=171-2624) | `wizard-wysch-adresse.png` |
| Wizard, WYSCH-Weg | Adresse ohne HTTPS, Kopplung gesperrt | `171:2717` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=171-2717) | `wizard-wysch-ohne-https.png` |
| Wizard, WYSCH-Weg | Wartezustand nach der Rückkehr von WYSCH | `171:2802` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=171-2802) | `wizard-wysch-kopplung-laeuft.png` |
| Wizard, WYSCH-Weg (dunkel) | Wartezustand | `177:5590` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5590) | `wizard-wysch-kopplung-laeuft-dunkel.png` |
| Wizard, WYSCH-Weg | Ergebnis „Signed in as …“ | `172:2728` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=172-2728) | `wizard-wysch-angemeldet.png` |
| Wizard, WYSCH-Weg | Fehler: WYSCH nicht erreichbar | `172:2794` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=172-2794) | `wizard-wysch-fehler-nicht-erreichbar.png` |
| Wizard, WYSCH-Weg | Fehler: Kopplung abgebrochen | `172:2859` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=172-2859) | `wizard-wysch-fehler-abgebrochen.png` |
| Wizard, WYSCH-Weg | Fehler: Zeitfenster abgelaufen | `172:2923` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=172-2923) | `wizard-wysch-fehler-zeitfenster.png` |

### 2. Settings › Single Sign-On

| Oberfläche | Zustand | Node-ID | Link | Bild |
|---|---|---|---|---|
| Provider-Liste | leer | `173:2891` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=173-2891) | `sso-liste-leer.png` |
| Provider-Liste | drei Provider, WYSCH mit „Reconnect needed“, Hinweis „kein Passwort“ | `173:3055` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=173-3055) | `sso-liste.png` |
| Provider-Liste (dunkel) | wie oben | `177:5611` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5611) | `sso-liste-dunkel.png` |
| Add provider, Schritt „Template“ | WYSCH gewählt | `173:3277` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=173-3277) | `sso-lauf-vorlage.png` |
| Add provider, „Provider address“ (Generic OIDC) | Discovery gefunden | `173:3483` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=173-3483) | `sso-lauf-anbieter-adresse.png` |
| Add provider, „Provider address“ | Discovery nicht gefunden | `173:3679` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=173-3679) | `sso-lauf-anbieter-adresse-fehler.png` |
| Add provider, „This installation“ | Vorschlag ohne Basis-Adresse, Redirect-URI | `174:3431` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=174-3431) | `sso-lauf-installation.png` |
| Add provider, „Connect“ (Kopplung) | vor der Kopplung | `174:3627` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=174-3627) | `sso-lauf-koppeln.png` |
| Add provider, „Connect“ (dunkel) | vor der Kopplung | `177:5698` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5698) | `sso-lauf-koppeln-dunkel.png` |
| Add provider, „Connect“ | gekoppelt | `174:3829` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=174-3829) | `sso-lauf-gekoppelt.png` |
| Add provider, „Register“ (manuell, Generic OIDC) | Redirect-URI kopiert, Client-ID und Secret | `174:4021` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=174-4021) | `sso-lauf-manuell.png` |
| Add provider, „Save“ | Test-Anmeldung bestanden | `176:4394` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=176-4394) | `sso-lauf-speichern.png` |
| Add provider, „Save“ | ohne bestandene Prüfungen und Test-Anmeldung, nur ausgeschaltet | `176:4615` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=176-4615) | `sso-lauf-speichern-ohne-test.png` |
| Bestehender Provider (WYSCH) mit „Test“ und „Reconnect“ | eigener Anmeldeweg ohne Passwort (Schutz vor Aussperren), Test zeigt abgelehnte Zugangsdaten | `176:4851` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=176-4851) | `sso-provider-wysch.png` |

### 3. Testergebnis

| Oberfläche | Zustand | Node-ID | Link | Bild |
|---|---|---|---|---|
| Test, Prüfungen ohne Anmeldung (WYSCH) | alle bestanden | `175:3875` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=175-3875) | `sso-test-pruefungen-ok.png` |
| Test, Prüfungen ohne Anmeldung (Generic OIDC) | Issuer falsch, Secret abgelehnt, PAR übersprungen | `175:4103` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=175-4103) | `sso-test-pruefungen-fehler.png` |
| Test-Anmeldung (WYSCH) | Claims reichen | `175:4351` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=175-4351) | `sso-test-claims-ok.png` |
| Test-Anmeldung (dunkel) | Claims reichen | `177:5774` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5774) | `sso-test-claims-ok-dunkel.png` |
| Test-Anmeldung (Generic OIDC) | E-Mail unbestätigt, Benutzername fehlt | `175:4606` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=175-4606) | `sso-test-claims-unvollstaendig.png` |

### 4. Anmeldeseite

| Oberfläche | Zustand | Node-ID | Link | Bild |
|---|---|---|---|---|
| Sign in | mit WYSCH und einem Generic-OIDC-Provider | `177:4768` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-4768) | `login-provider.png` |
| Sign in (dunkel) | wie oben | `177:5892` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5892) | `login-provider-dunkel.png` |

### 5. Profil

| Oberfläche | Zustand | Node-ID | Link | Bild |
|---|---|---|---|---|
| Profile | einziger SystemAdmin ohne Passwort, „Set a local password“, „Unlink“ gesperrt | `177:4834` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-4834) | `profil-ohne-passwort.png` |
| Profile (dunkel) | wie oben | `177:5913` | [Figma](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5913) | `profil-ohne-passwort-dunkel.png` |

## Komponenten (Seite „Components“)

Neu angelegt, alle Farben an Variablen der Sammlung „Theme“ gebunden:

| Komponente | Node-ID | Varianten / Eigenschaften |
|---|---|---|
| Provider Logo/WYSCH | `168:246` | WYSCH-Symbol aus `Wiesenwischer/WYSCH`, `brand/wysch-symbol.svg`, in Markenfarben, 24×24 |
| Provider Logo/Generic OIDC | `168:247` | Schlüssel-Symbol in `text/secondary` |
| Provider Logo/Built-in | `168:251` | Schloss-Symbol in `text/brand` |
| Provider Mark | `168:271` | `Provider=WYSCH` `168:255`, `Generic OIDC` `168:261`, `Built-in` `168:266`; Kachel 44×44, Radius 12 |
| Sign-in Option | `169:233` | `State=Default` `169:184`, `Hover` `169:196`, `Selected` `169:208`, `Focus` `169:221`; Text `Title`, `Description`; 340 breit, Radius 16, Zustände wie „Theme Card“ |
| Stepper Item | `169:248` | `State=Done` `169:234`, `Current` `169:240`, `Upcoming` `169:244`; Text `Label`, `Number` |
| Check Row | `169:277` | `Result=Passed` `169:249`, `Failed` `169:256`, `Running` `169:264`, `Skipped` `169:270`; Text `Title`, `Detail`, Schalter `Show detail` |
| Alert | `169:309` | `Tone=Info` `169:278`, `Success` `169:286`, `Warning` `169:293`, `Error` `169:301`; Text `Title`, `Body`, Schalter `Show body` |
| Input | `170:249` | `State=Default` `170:224`, `Focus` `170:229`, `Error` `170:234`, `Disabled` `170:239`, `Read-only` `170:244`; Text `Label`, `Value`, `Hint`, Schalter `Show hint`; Feld 44 hoch, Radius 10 |
| Copy Field | `170:269` | `State=Default` `170:250`, `Copied` `170:260`; Wert in Monospace |
| Toggle | `170:276` | `State=On` `170:270`, `Off` `170:272`, `Disabled` `170:274`, `On Disabled` `227:239`; 40×22 |
| Provider Button | `170:298` | `State=Default` `170:277`, `Hover` `170:284`, `Focus` `170:291`; Text `Label`, Instanztausch `Logo`; 48 hoch, Radius 10 |

Neue Symbole im Rahmen „Icons“ (`3:2`), 24×24, Strich 2 wie die vorhandenen: `Icon/Key` `168:173`, `Icon/Lock`
`168:177`, `Icon/Check Circle` `168:181`, `Icon/X Circle` `168:186`, `Icon/Alert Triangle` `168:191`, `Icon/Info`
`168:196`, `Icon/Loader` `168:199`, `Icon/Copy` `168:203`, `Icon/Minus Circle` `168:221`, `Icon/Check` `168:224`,
`Icon/Arrow Left` `168:228`, `Icon/Clock` `168:236` (Formen nach Lucide, wie in der App).

Wiederverwendet: `Button` (`5:52`), `Status Badge` (`5:68`, Beschriftung je Rahmen überschrieben), `Logo/Lockup`
(`6:67`), App-Rahmen mit Sidebar und Kopfzeile aus „App / Settings – Appearance – Türkis, hell“ (`106:2929`).

## Geänderte Tokens und Variablen

Keine. Alle Rahmen nutzen die bestehenden Variablen; die Kachel-Hintergründe der Status-Töne kommen aus
`status/*-bg`, der Rahmen der Hinweise aus der Statusfarbe mit 35 % Deckkraft, das Eingabefeld aus `border/strong`
mit 55 % Deckkraft (in der App heute `border-gray-300`).

## Symbole und Raster-Icons

Keine Symbol-Exporte und keine Raster-Icons. Das WYSCH-Symbol ist das Logo aus dem Repo WYSCH; als Asset der Vorlage
bringt es die Umsetzung aus `brand/wysch-symbol.svg` mit (Spezifikation: „Das Symbol für WYSCH ist das WYSCH-Logo“).

## Interaktion und Navigation

**Eingabe:** Maus und Tastatur, Bildschirm 1440 breit (Bezugsgröße wie die übrigen App-Rahmen). Kacheln, Knöpfe und
Felder haben den Fokuszustand der Komponenten (Rahmen `focus/ring`, 2 px außen).

**Wizard**
- Kopf wie heute (Titel, Untertitel, Countdown), neu darüber das Logo und darunter die Schrittanzeige
  „Sign-in method · Administrator · Email“ (Marcus im Chat, 06.10.2026).
- Schritt 1: zwei Kacheln „Built-in sign-in“ und je Vorlage mit Angebot in der Ersteinrichtung (eingebaut: WYSCH).
  Auswahl wie die Theme-Kacheln, danach „Continue“; ohne Auswahl ist „Continue“ gesperrt.
- „Built-in sign-in“ führt in den heutigen Admin-Schritt (Schritt 2 „Administrator“), danach SMTP.
- WYSCH: Adresse dieser Installation (vorgeschlagen aus der Browser-Adresse, wird Basis-Adresse), Kasten „What
  happens“, Knopf „Connect with WYSCH“ im Stil des Provider-Knopfs, darunter „Use built-in sign-in instead“.
- Steht dort keine `https://`-Adresse (außer `localhost`), ist „Connect with WYSCH“ gesperrt und ein Hinweis „HTTPS
  required“ erklärt den Weg (Entscheidung im Issue #498, 06.10.2026: WYSCH nimmt nur `https`-Redirect-URIs an).
- Nach der Rückkehr von WYSCH: Wartezustand mit drei Zeilen (bestätigt, Zugangsdaten abholen, anmelden) und dem Hinweis
  auf die 15 Minuten. Der Countdown des Wizards ist hier ausgeblendet, weil der Lauf bis zu 15 Minuten gilt.
- Ergebnis „Signed in as <Anzeigename>“ mit E-Mail, Benutzername, Rolle, Provider und „Local password: Not set“; dann
  „Continue“ zum Schritt „Email“.
- Fehler: eigene Karte mit Symbol, Titel, Grund und zwei Knöpfen („Try again“ bzw. „Start again“, „Use built-in sign-in
  instead“). Beim abgelaufenen Zeitfenster zusätzlich der Hinweis, dass der gekoppelte Provider ausgeschaltet bleibt.
  Weitere Fehler aus der Spezifikation nutzen dieselbe Karte (ohne eigenen Rahmen, Texte als Vorschlag):
  - unbestätigte E-Mail: „Your WYSCH email address is not confirmed“ / „Confirm your email address in WYSCH, then try
    again.“
  - anderer Weg hat gewonnen: „Setup was completed elsewhere“ / „A system administrator was created in the meantime.
    Sign in instead.“ mit dem Knopf „Go to sign-in“ statt der beiden Knöpfe
  - zweiter Browser: „This sign-in run belongs to another browser“ / „Start the setup again in this browser.“

**Settings › Single Sign-On**
- Liste: je Provider Symbol der Vorlage, Name, Kennung, Vorlage, Adresse bzw. Kopplungsdatum, Status-Badge
  „Enabled“/„Disabled“, letztes Testergebnis und die Knöpfe „Test“, „Reconnect“ (nur gekoppelte Provider) und „Edit“.
  „Add provider“ oben rechts. Ohne Provider ein leerer Zustand mit demselben Knopf.
- Hinweis (Warnung) über der Liste und im Profil, solange der einzige SystemAdmin kein Passwort hat, mit dem Befehl für
  den Notzugang.
- „Add provider“ ist eine eigene Seite unter `Settings / Single Sign-On / Add provider` mit waagrechter Schrittanzeige
  (Marcus im Chat, 06.10.2026). Schritte bei WYSCH: Template · This installation · Connect · Test · Save. Bei Generic
  OIDC: Template · Provider address · This installation · Register · Test · Save. Ein Schritt ohne Inhalt (Adresse fest
  in der Vorlage) erscheint nicht.
- Karte 860 breit, unten „Back“ links, „Cancel“ und „Continue“ rechts. „Continue“ ist gesperrt, solange der Schritt
  nicht erfüllt ist (Discovery nicht gefunden, noch nicht gekoppelt).
- „Connect with WYSCH“ verlässt die Seite zu WYSCH; der Rücksprung landet wieder im Schritt „Connect“ mit dem Zustand
  „Connected with WYSCH“. Im Fehlerfall zeigt derselbe Schritt die Fehler wie im Wizard (Alert „Error“ mit „Try
  again“).
- „Test“ in der Liste öffnet die Seite des Providers und führt die Prüfungen aus; „Reconnect“ startet die Kopplung
  wie im Schritt „Connect“ und ersetzt Client-ID und Secret. Die Seite des Providers zeigt Verbindung, Test und die
  Felder zum Bearbeiten.

**Testergebnis**
- Prüfungen ohne Anmeldung als Checkliste (bestanden, fehlgeschlagen, läuft, übersprungen), jede mit einer Zeile, was
  geprüft wurde bzw. warum es scheitert und was zu tun ist. Bei Fehlern oben ein Alert „n of m checks failed“.
- Die Test-Anmeldung ist erst möglich, wenn alle Prüfungen bestanden sind. Danach sind die Prüfungen zu einer Zeile
  zusammengeklappt („Show“), darunter eine Tabelle Detail · Claim · Value · For sign-in mit Badges „Received“,
  „Verified“, „Not verified“, „Missing“ und ein Alert, ob die Angaben für eine Anmeldung reichen.
- Fehlertexte im Entwurf: Discovery 404, Issuer passt nicht (mit beiden Werten), `invalid_client` beim PAR-Aufruf,
  PAR nicht angeboten, E-Mail unbestätigt, Benutzername fehlt.

**Speichern**
- Name (Kennung in der Route) und Anzeigename, Schalter „Enable provider“ (nur nach bestandenen Prüfungen und
  erfolgreicher Test-Anmeldung bedienbar, sonst gesperrt mit Erklärung und Knopf „Save disabled“), Schalter „Trust unverified email addresses“ (bei neuen
  Providern aus) und eine Vorschau des Knopfs auf der Anmeldeseite.

**Anmeldeseite:** Aufbau wie heute; die Provider-Knöpfe zeigen das Symbol der Vorlage links vom Text
„Sign in with <Anzeigename>“ (Generic OIDC: Schlüssel). Die rechte Hälfte nutzt `nav/bg` im dunklen Modus.

**Profil:** Hat der Benutzer kein lokales Passwort, ersetzt die Karte „Set a local password“ die Karte „Change
Password“ (ohne Feld „Current Password“); „Account Information“ zeigt „Password: Not set“. „Unlink“ ist gesperrt,
solange der Benutzer kein lokales Passwort hat, mit dem Hinweis darunter, erst ein Passwort zu setzen (Marcus im Chat,
06.10.2026).

**Schutz vor Aussperren** (Marcus im Chat, 06.10.2026: „Es muss sichergestellt sein, dass man sich nicht aussperrt“)
- Einschalten lässt sich ein Provider erst nach bestandenen Prüfungen **und** einer erfolgreichen Test-Anmeldung.
- Meldet sich der aktuelle Benutzer nur über diesen Provider an (kein lokales Passwort), zeigt die Seite des Providers
  eine Warnung; „Enable provider“ ist eingeschaltet und gesperrt (`Toggle` `On Disabled`), „Remove provider“ ist
  gesperrt.
- Neue Zugangsdaten aus „Reconnect“ und geänderte Einstellungen eines solchen Providers gelten erst nach einer
  erfolgreichen Test-Anmeldung; bis dahin bleiben die alten Einstellungen aktiv.
- „Unlink“ im Profil ist ohne lokales Passwort gesperrt (siehe Profil).

**Widerruf erkennen** (offene Frage der Spezifikation, Marcus im Chat, 06.10.2026): Scheitert eine Anmeldung oder ein
Test bei einem gekoppelten Provider mit `invalid_client`, zeigt die Liste am Provider das Badge „Reconnect needed“
(Ton „Degraded“), als letztes Ergebnis „Sign-in failed“ und in der Beschreibung den Grund. Das Badge verschwindet nach
einem erfolgreichen „Reconnect“ mit Test-Anmeldung.

## Festlegungen, die die Spezifikation nicht vorgibt

- Einrichtungslauf als eigene Seite mit waagrechter Schrittanzeige — Marcus im Chat, 06.10.2026.
- Wizard mit Logo und Schrittanzeige, sonst Layout wie heute, in den Türkis-Tokens — Marcus im Chat, 06.10.2026.
- Gesperrte Kopplung mit Hinweis bei Adressen ohne `https` — aus der Entscheidung im Issue #498 (06.10.2026).
- Countdown des Wizards im Wartezustand und im Ergebnis ausgeblendet (der Lauf gilt bis 15 Minuten) — Vorschlag im
  Entwurf.
- „Enable provider“ verlangt bestandene Prüfungen und eine erfolgreiche Test-Anmeldung; Schutz vor Aussperren wie oben
  — Marcus im Chat, 06.10.2026.
- „Unlink“ ohne lokales Passwort gesperrt — Marcus im Chat, 06.10.2026.
- Badge „Reconnect needed“ bei `invalid_client` eines gekoppelten Providers — Marcus im Chat, 06.10.2026.
- Die Liste zeigt das letzte Testergebnis je Provider — Vorschlag im Entwurf.
- Beispielbefehl für den Notzugang `docker compose exec readystackgo rsgo admin set-password <username>` aus der
  Spezifikation; Name und Form legt der Plan fest, der Text folgt ihm.
- Werte aus Beispielen (`rsgo.example.com`, `alex@example.com`, `rsgo-7f3a9c`, „Company Keycloak“) sind Beispieldaten.

## Hinweise für die Umsetzung

- Die Seite nutzt die semantischen Tokens wie Settings › Appearance (`bg-surface`, `border-line`, `text-fg-*`,
  `bg-primary`, `ring-focus`), nicht mehr `gray-*`/`brand-*`. Das gilt auch für Wizard und Anmeldeseite, die heute
  noch feste Grautöne nutzen.
- Neue Variablen gibt es nicht; ein Export der Tokens ist nicht nötig.
- Die Monospace-Schrift im Entwurf (JetBrains Mono) steht für `font-mono` der App (Tailwind-Standard).
- Secrets erscheinen nie im Klartext: im manuellen Schritt als Passwortfeld, sonst als „Stored encrypted“.

## Offene Fragen

Keine.
