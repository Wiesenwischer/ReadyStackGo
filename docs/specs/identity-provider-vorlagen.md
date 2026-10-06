# Anmeldung mit WYSCH und Vorlagen für Identity Provider

## Ziel

ReadyStackGo unterstützt WYSCH ID von Haus aus als Identity Provider. Wer WYSCH nutzen will, wählt es unter Settings ›
Single Sign-On als Vorlage aus, und ein Einrichtungslauf koppelt die Installation mit WYSCH, testet die Anmeldung und
speichert den Provider, ohne dass jemand Adressen, Client-IDs oder Secrets abtippt. Schon die Ersteinrichtung fragt,
ob der erste Administrator die integrierte Anmeldung nutzt oder sich mit WYSCH anmeldet. Vorlagen sind ein
erweiterbarer Katalog: Weitere Anbieter, als Nächstes das on-premise betriebene ams.Identity, kommen als neue Vorlage
dazu, ohne den Einrichtungslauf neu zu bauen.

## Was dazugehört

- **Katalog von Provider-Vorlagen.** Eine Vorlage beschreibt einen Anbieter: Kennung, Anzeigename, Beschreibung,
  Symbol, Vorschlag für den Provider-Namen (die Kennung in den Routen, z. B. `wysch`), Scopes, die Adresse des
  Anbieters (Authority) entweder fest oder vom Nutzer einzugeben (mit Hinweistext und Beispiel), die Art der
  Registrierung (`manual` oder `pairing`), ob PAR verlangt wird, welche Claims Benutzername, Anzeigename und E-Mail
  liefern, ein Link zur Hilfe und ob die Vorlage in der Ersteinrichtung angeboten wird.
  - Eingebaut sind zwei Vorlagen: **WYSCH** (Authority fest `https://id.wysch.wiesenwischer.de/`, Registrierung
    `pairing`, PAR, Scopes `openid profile email`, in der Ersteinrichtung angeboten) und **Generic OIDC**
    (Authority vom Nutzer, Registrierung `manual`, entspricht dem heutigen Formular).
  - Der Katalog lädt Vorlagen wie der Theme-Katalog (`ThemeCatalog`): eingebaute Vorlagen aus dem Image, dazu
    Vorlagen aus einem Verzeichnis, das man dem Container mitgibt; eine Vorlage aus dem Verzeichnis ersetzt eine
    eingebaute mit derselben Kennung, eine Liste in der Konfiguration kann den Katalog einschränken. So bringt eine
    Distribution eigene Vorlagen mit, etwa für ams.Identity.
  - Vorlagen sind Daten. Die Arten der Registrierung sind Code hinter einer Schnittstelle, damit eine weitere Art (zum
    Beispiel eine andere Kopplung oder dynamische Registrierung nach RFC 7591) dazukommen kann, ohne Einrichtungslauf
    und Vorlagen zu ändern.
  - Das Format der Vorlagen ist dokumentiert, damit Betreiber und Distributionen eigene bauen können.
- **Einrichtungslauf unter Settings › Single Sign-On.** „Add provider“ beginnt mit der Auswahl einer Vorlage und führt
  dann Schritt für Schritt:
  1. **Vorlage wählen.**
  2. **Adresse des Anbieters**, nur wenn die Vorlage sie nicht festlegt. ReadyStackGo prüft sofort die Discovery
     (siehe Verbindungstest).
  3. **Adresse dieser Installation bestätigen.** Ohne gesetzte Basis-Adresse (`SystemConfig.BaseUrl`) schlägt der
     Lauf die Adresse vor, unter der der Browser ReadyStackGo gerade aufruft, und speichert sie nach Bestätigung.
     Daraus entsteht die Redirect-URI `<Basis-Adresse>/api/auth/oidc/<name>/callback`.
  4. **Registrieren.** Bei `pairing` mit einem Knopf „Connect with <Anbieter>“: Der Browser geht zum Anbieter, der
     Nutzer meldet sich dort an und bestätigt, ReadyStackGo erhält Client-ID und Secret ohne Abtippen. Bei `manual`
     zeigt der Lauf die Redirect-URI mit Kopierknopf und fragt Client-ID und Secret ab.
  5. **Testen** (siehe Verbindungstest), mit verständlicher Meldung bei jedem Fehler.
  6. **Speichern.** Erst ein erfolgreicher Test schaltet den Provider ein; ohne Test lässt er sich nur ausgeschaltet
     speichern.
  - Bestehende Provider lassen sich wie bisher bearbeiten, dazu kommen „Test“ und, bei gekoppelten Providern,
    „Reconnect“ (neu koppeln, ersetzt Client-ID und Secret).
- **Verbindungstest.** Zwei Stufen:
  - **Ohne Anmeldung** (vom Server aus): Discovery unter `/.well-known/openid-configuration` erreichbar, Issuer passt
    zur Authority, Endpunkte für Authorization, Token und JWKS vorhanden, bei verlangtem PAR auch der PAR-Endpunkt.
    Bietet der Anbieter PAR an, prüft ein PAR-Aufruf auch Client-ID und Secret, ohne dass sich jemand anmeldet.
  - **Test-Anmeldung**: Der Nutzer meldet sich einmal beim Anbieter an. ReadyStackGo zeigt danach, welche Angaben
    angekommen sind (Subject, E-Mail und ob sie bestätigt ist, Benutzername, Anzeigename) und ob sie für eine Anmeldung
    reichen. Die Test-Anmeldung meldet niemanden bei ReadyStackGo an und verknüpft kein Konto.
- **OIDC-Client mit PAR.** `OidcService` unterstützt Pushed Authorization Requests (RFC 9126): Verlangt die Discovery
  sie (`require_pushed_authorization_requests`) oder die Vorlage, schickt ReadyStackGo die Anfrage zuerst an den
  PAR-Endpunkt und leitet nur mit `client_id` und `request_uri` weiter. Claims liest der Client weiter aus dem ID-Token
  (WYSCH hat keinen Userinfo-Endpunkt), zusätzlich `preferred_username` und den Anzeigenamen nach der Vorlage.
- **Ersteinrichtung: Anmeldeart wählen.** Der Wizard bekommt vor dem Admin-Schritt einen ersten Schritt „How do you
  want to sign in?“ mit „Built-in sign-in (username and password)“ und je einer Kachel für jede Vorlage, die in der
  Ersteinrichtung angeboten wird (eingebaut: WYSCH).
  - **Integriert:** weiter wie heute (Admin-Konto, dann SMTP).
  - **WYSCH:** Adresse dieser Installation bestätigen, koppeln, dann direkt mit WYSCH anmelden. Das angemeldete
    WYSCH-Konto wird der erste SystemAdmin, ohne lokales Passwort und mit der Verknüpfung (Provider, Subject); danach
    folgt SMTP wie heute. Verlangt wird eine bestätigte E-Mail (`email_verified`). Den Benutzernamen bildet
    ReadyStackGo aus `preferred_username` nach den heutigen Regeln (Zeichen, Länge, eindeutig), sonst aus der E-Mail.
  - Ist WYSCH nicht erreichbar oder bricht die Kopplung ab, zeigt der Wizard den Grund und bietet „Use built-in
    sign-in instead“ an. Ein schon gekoppelter Provider bleibt dann ausgeschaltet in den Settings stehen.
  - Der Schutz des Wizards gilt auch für diesen Weg: Starten nur im Zeitfenster und solange es keinen SystemAdmin
    gibt; abschließen kann nur der Browser, der den Lauf gestartet hat (Bindung an den `state` und ein Cookie des
    Laufs). Damit Kopplung und Anmeldung bei WYSCH (eventuell mit Kontoanlage) nicht am Zeitfenster scheitern, darf
    ein im Fenster gestarteter Lauf bis zu 15 Minuten dauern. Gewinnt ein anderer Weg (es gibt inzwischen einen
    SystemAdmin), endet der Lauf mit einer Meldung.
- **Zuordnung beim Callback.** Zuerst über die Verknüpfung (Provider, Subject). Nur wenn es keine gibt, über die
  E-Mail, und dann nur, wenn der Anbieter sie als bestätigt meldet oder beim Provider „Trust unverified email
  addresses“ eingeschaltet ist. Für Provider, die vor diesem Vorhaben angelegt wurden, ist diese Einstellung an (heutiges
  Verhalten), für neue aus. Eine bestehende Verknüpfung mit anderem Subject wird nicht still überschrieben, sondern
  abgelehnt. Wer sich nach der Einrichtung anmelden darf, bleibt wie heute: bestehende Nutzer und offene Einladungen.
- **Notzugang.** Ein Befehl im Container setzt für einen Benutzer ein lokales Passwort, auch ohne laufenden Identity
  Provider, z. B. `docker compose exec readystackgo rsgo admin set-password <username>` (Name und Form legt der Plan
  fest). Der Befehl liest das Passwort von der Eingabe oder erzeugt eines und zeigt es einmal an, und er protokolliert
  den Vorgang. Dazu kann ein Benutzer ohne Passwort im Profil ein lokales Passwort setzen („Set a local password“);
  Settings › Single Sign-On und das Profil weisen darauf hin, solange der einzige SystemAdmin kein Passwort hat.
- **Anmeldeseite:** Knöpfe der Provider zeigen das Symbol ihrer Vorlage („Sign in with WYSCH“).
- **Dokumentation** auf der Website, Deutsch und Englisch: Anmeldung mit WYSCH, Einrichtungslauf, Vorlagenformat,
  Notzugang. Die veraltete Stelle in `docs/Configuration/Overview.md` („OIDC providers (future)“) wird korrigiert.

## Was ausdrücklich nicht dazugehört

- **Die Seite von WYSCH:** Kopplungs-Endpunkt, Bestätigungsseite, Verwaltung der verbundenen Installationen und neue
  Clients in WYSCH ID. Das ist ein eigenes Vorhaben im Repo WYSCH (Spezifikation `docs/specs/client-kopplung.md`
  dort). Dieses Vorhaben nutzt dessen Protokoll.
- **Eine Vorlage für ams.Identity.** Der Katalog muss sie ermöglichen (Authority vom Nutzer, Registrierung `manual`),
  gebaut wird sie in einem eigenen Vorhaben.
- **Rollen oder Rechte aus Claims.** Rollen vergibt weiter nur ReadyStackGo (erster Admin, Einladungen, Benutzerverwaltung).
- **Abmeldung beim Anbieter** (Single Logout, `end_session_endpoint`) und Refresh-Tokens des Anbieters.
- **Dynamische Registrierung nach RFC 7591** als eingebaute Art; die Schnittstelle lässt sie später zu.
- **Vorlagen aus dem Netz laden** (über eine URL): nur Image und Verzeichnis, wie bei den Themes.
- **Lokalisierung:** neue Texte bleiben englisch bis kurz vor 1.0.

## Vorgaben

- **Kopplung nach dem Vorbild des App-Manifest-Flows von GitHub** (Marcus, 06.10.2026): ReadyStackGo schickt eine
  Beschreibung von sich an den Anbieter, der Nutzer meldet sich an und bestätigt, die Zugangsdaten kommen über einen
  einmaligen Code zurück, den ReadyStackGo vom Server aus einlöst. Das genaue Protokoll legt die Spezifikation in WYSCH
  fest; sie ist dafür verbindlich. ReadyStackGo muss für die Kopplung nicht aus dem Internet erreichbar sein, nur den
  Anbieter erreichen; alle Rücksprünge laufen über den Browser.
- **Manuelle Eingabe bleibt der allgemeine Weg** (Marcus, 06.10.2026), für jeden Anbieter ohne Kopplung.
- **Notzugang über einen Befehl im Container, kein Passwort im Wizard** (Marcus, 06.10.2026). Vorbilder:
  `grafana cli admin reset-admin-password`, `gitea admin user change-password`.
- **Erweiterbarkeit wie beim Theme-Katalog:** eingebaut, Verzeichnis, Einschränkung per Konfiguration. Distributionen
  ersetzen oder ergänzen Vorlagen, ohne dieses Repo zu ändern.
- **Kompatibel:** Bestehende Einträge in `rsgo.oidc.json` funktionieren unverändert weiter und gelten als „Generic
  OIDC“. Neue Felder (Vorlage, Art der Registrierung, Zeitpunkt der Kopplung, „Trust unverified email addresses“) sind
  optional. Die Routen `/api/auth/oidc/...` bleiben.
- **Ohne Internetzugang** bleibt die integrierte Anmeldung voll nutzbar; der Wizard zeigt WYSCH auch dann an, meldet
  aber verständlich, wenn WYSCH nicht erreichbar ist.
- **Secrets** werden nie angezeigt, protokolliert oder an den Browser gegeben; gespeichert verschlüsselt wie heute
  (`CredentialEncryptionService`).
- **Sicherheit:** PKCE S256 bleibt Pflicht; `state`, `nonce` und Kopplungs-Codes sind einmalig und laufen nach
  10 Minuten ab; die Endpunkte des Wizards prüfen dieselben Bedingungen wie `CreateAdminEndpoint`
  (`WizardTimeoutPreProcessor`, kein SystemAdmin vorhanden).
- **Tests:** Unit-Tests für Katalog, Zuordnung beim Callback (alle Fälle: Subject bekannt, nur E-Mail, E-Mail
  unbestätigt, anderes Subject, keine Einladung), PAR und Wizard-Zustände; ein Browsertest mit einem lokalen
  Test-Anbieter im Container, der Kopplung und Anmeldung nachbildet (kein Aufruf von WYSCH in CI).

## Oberfläche und Bilder

Neu oder geändert: der erste Wizard-Schritt (Anmeldeart), der WYSCH-Weg im Wizard, die Vorlagenauswahl und der
Einrichtungslauf unter Settings › Single Sign-On, das Testergebnis, die Knöpfe auf der Anmeldeseite und „Set a local
password“ im Profil. Verbindlich: Entwurf in `docs/specs/identity-provider-vorlagen/entwurf/` (freigegeben mit dem
Merge von PR #PR_NUMMER). Richtung: die Kacheln der Theme-Auswahl
unter Settings › Appearance (PR #489) und das bestehende Wizard-Layout. Das Symbol für WYSCH ist das WYSCH-Logo.

## Abnahme

- Unter Settings › Single Sign-On führt „Add provider“ › WYSCH durch den Lauf; nach „Connect with WYSCH“ und Bestätigung
  bei WYSCH sind Client-ID und Secret gesetzt, ohne dass etwas eingetippt wurde, und der Test ist grün.
- „Generic OIDC“ zeigt die Redirect-URI zum Kopieren und speichert wie heute; ein falsches Secret, eine falsche
  Authority oder ein falscher Issuer führen im Test zu je einer eigenen, verständlichen Meldung.
- Die Anmeldung mit WYSCH funktioniert gegen WYSCH ID mit verlangtem PAR.
- Eine Vorlage, die als Datei im Vorlagen-Verzeichnis liegt, erscheint nach dem Neustart in der Auswahl; eine mit der
  Kennung `wysch` ersetzt die eingebaute.
- Die Ersteinrichtung fragt zuerst nach der Anmeldeart. Mit WYSCH entsteht ein SystemAdmin ohne Passwort, der sich
  danach mit WYSCH anmeldet; mit „Built-in sign-in“ läuft alles wie bisher.
- Ein zweiter Browser kann einen laufenden WYSCH-Lauf im Wizard nicht abschließen; nach Ablauf des Zeitfensters lässt
  sich kein neuer Lauf starten.
- Eine Anmeldung mit unbestätigter E-Mail wird bei neuen Providern nicht einem bestehenden Benutzer zugeordnet.
- Der Befehl zum Notzugang setzt ein Passwort, mit dem sich der Admin lokal anmelden kann, während der Anbieter nicht
  erreichbar ist.
- Bestehende Provider aus `rsgo.oidc.json` funktionieren nach dem Update unverändert.
- „Build & Test“ ist grün, inklusive des neuen Browsertests mit lokalem Test-Anbieter.

## Material

- Bestehendes OIDC-SSO: PR #422 (Anmeldung, Einladungen) und PR #426 (Verknüpfung im Profil lösen).
- Code heute: `src/ReadyStackGo.Infrastructure.Security/Authentication/OidcService.cs` (Client ohne PAR),
  `src/ReadyStackGo.Api/Endpoints/Auth/OidcEndpoints.cs` (Challenge, Callback, `ResolveUser` ordnet nur per E-Mail
  zu), `src/ReadyStackGo.Api/Endpoints/Settings/OidcSettingsEndpoints.cs` (ganze Liste per PUT),
  `src/ReadyStackGo.Infrastructure/Configuration/OidcConfig.cs`,
  `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/Oidc/OidcSettingsPage.tsx`.
- Wizard heute: `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Wizard/index.tsx`,
  `src/ReadyStackGo.Api/Endpoints/Wizard/CreateAdminEndpoint.cs`,
  `src/ReadyStackGo.Infrastructure/Configuration/WizardTimeoutService.cs`.
- Vorbild Katalog: `src/ReadyStackGo.Infrastructure/Services/Themes/ThemeCatalog.cs`, Spezifikation
  `docs/specs/theme-und-logo.md`.
- WYSCH ID: OpenIddict, PAR serverweit Pflicht, kein Userinfo, Clients bisher fest in `WyschClients.cs`, keine
  Mandanten (Entscheidungen E-14, E-15 im Repo WYSCH). Spezifikation der Kopplung: `Wiesenwischer/WYSCH`,
  `docs/specs/client-kopplung.md`.
- Ältere Zielbilder: `docs/Reference/Full-Specification.md` (Auth-Modi, nennt Keycloak und ams.identity),
  `docs/Security/Overview.md`.
- Standards: RFC 9126 (PAR), RFC 7636 (PKCE), RFC 7591 (dynamische Registrierung), GitHub App Manifest Flow.

## Offene Fragen

- **Installationen ohne HTTPS:** Viele Installationen laufen unter `http://server:8080`. Ob WYSCH solche
  Redirect-URIs annimmt (nur private Adressen? nur mit Warnung?), entscheidet die WYSCH-Spezifikation; ReadyStackGo
  muss die Ablehnung verständlich anzeigen.
- **Widerruf:** Wird die Kopplung in WYSCH gelöscht, scheitern Anmeldungen. Soll ReadyStackGo das erkennen und
  „Reconnect“ vorschlagen, und soll das Entfernen eines Providers in ReadyStackGo die Kopplung bei WYSCH mit löschen?
- **Wo liegt die Vorlage für ams.Identity:** im öffentlichen Repo oder nur in der Distribution, die sie braucht?
- **Kontoanlage im Wizard:** Hat jemand noch kein WYSCH-Konto, legt er es während der Anmeldung bei WYSCH an. Reichen
  die 15 Minuten dafür, inklusive Bestätigung der E-Mail?
- **Format der Vorlagen:** eine JSON-Datei je Vorlage mit Symbol als Datei daneben, wie die Theme-Pakete? Der Plan
  schlägt das Format vor.
