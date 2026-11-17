# Plan: Anmeldung mit WYSCH und Vorlagen für Identity Provider

Vorhaben #498 · Spezifikation [`docs/specs/identity-provider-vorlagen.md`](../specs/identity-provider-vorlagen.md) ·
Entwurf [`docs/specs/identity-provider-vorlagen/entwurf/README.md`](../specs/identity-provider-vorlagen/entwurf/README.md)
(freigegeben mit dem Merge von PR #499) · Entscheidung zu HTTPS: Kommentar von Marcus im Issue #498 (06.10.2026) ·
Stand der Planung: `origin/main` 5ec3778, 06.10.2026

## 1. Kontext

ReadyStackGo bindet seit PR #422 generische OIDC-Provider an, aber nur über ein Formular, in das man Authority,
Client-ID und Secret abtippt (`OidcSettingsPage.tsx`, ganze Liste per PUT), ohne Test und ohne PAR
(`OidcService.cs:23-50`). Beim Callback ordnet `ResolveUser` nur über die E-Mail zu und prüft weder `email_verified` noch
das Subject einer bestehenden Verknüpfung (`OidcEndpoints.cs:244-290`). Die Spezifikation will WYSCH ID als eingebauten
Anbieter: einen erweiterbaren Katalog von Provider-Vorlagen nach dem Muster des Theme-Katalogs, einen geführten
Einrichtungslauf mit Kopplung nach dem Vorbild des GitHub-App-Manifest-Flows, Verbindungstest und Speichern, einen ersten
Wizard-Schritt, in dem ein WYSCH-Konto erster Administrator wird, PAR im OIDC-Client, die Zuordnung zuerst über das
Subject und einen Notzugang per Befehl im Container. Die Seite von WYSCH (Kopplung, Bestätigungsseite) ist das Vorhaben
Wiesenwischer/WYSCH#170; dessen Protokoll ist verbindlich.

## 2. Ergebnis aus Nutzersicht

- **Ersteinrichtung:** Der Wizard zeigt Logo und die Schrittanzeige „Sign-in method · Administrator · Email“ und fragt
  zuerst „How do you want to sign in?“. „Built-in sign-in“ führt wie heute zum Admin-Formular und zu SMTP. „WYSCH“
  schlägt die Adresse der Installation aus dem Browser vor, koppelt mit „Connect with WYSCH“ (anmelden und bestätigen bei
  WYSCH, nichts abtippen), meldet direkt mit WYSCH an und zeigt „Signed in as …“. Das WYSCH-Konto ist danach erster
  SystemAdmin ohne lokales Passwort. Ohne HTTPS (außer `localhost`) ist die Kopplung gesperrt und erklärt. Bei Fehlern
  zeigt der Wizard den Grund und „Use built-in sign-in instead“.
- **Settings › Single Sign-On:** Liste mit Symbol der Vorlage, Status, letztem Ergebnis und „Test“, „Reconnect“ (nur
  gekoppelte Provider), „Edit“. „Add provider“ führt durch Vorlage, Adresse des Anbieters (nur ohne feste Adresse),
  Adresse dieser Installation mit Redirect-URI, Koppeln oder Client-ID/Secret, Prüfungen ohne Anmeldung, Test-Anmeldung
  und Speichern. Eingeschaltet wird ein Provider erst nach bestandenen Prüfungen und erfolgreicher Test-Anmeldung.
- Löscht jemand die Kopplung bei WYSCH, zeigt die Liste „Reconnect needed“; „Reconnect“ holt neue Zugangsdaten.
- **Anmeldeseite:** Provider-Knöpfe mit dem Symbol ihrer Vorlage („Sign in with WYSCH“). Angemeldet wird zuerst über die
  Verknüpfung (Provider, Subject); eine unbestätigte E-Mail ordnet bei neuen Providern niemanden einem Konto zu.
- **Profil:** Ohne lokales Passwort ersetzt „Set a local password“ die Karte „Change Password“, „Unlink“ ist gesperrt.
  Profil und Settings › Single Sign-On warnen, solange kein SystemAdmin ein lokales Passwort hat.
- **Notzugang:** `docker compose exec readystackgo rsgo admin set-password <username>` setzt ein lokales Passwort, auch
  wenn der Anbieter nicht erreichbar ist.
- **Betreiber und Distributionen** legen eigene Vorlagen (`template.json`, `icon.svg`) in ein Verzeichnis und schränken
  den Katalog per Konfiguration ein. Website und Dokumentation (de/en) beschreiben Anmeldung mit WYSCH,
  Einrichtungslauf, Vorlagenformat und Notzugang.

## 3. Umfang und Nicht-Umfang

**Umfang**

- Backend: Vorlagen-Katalog, Registrierungsarten `manual` und `pairing`, Einrichtungssitzungen, Verbindungstest, PAR im
  OIDC-Client, neue Zuordnung beim Callback, Wizard-Lauf mit WYSCH, Profil-Endpunkte, Befehl für den Notzugang, neue
  optionale Felder in `rsgo.oidc.json`.
- Weboberfläche (`@rsgo/ui-generic`, `@rsgo/core`): Wizard, Settings › Single Sign-On (Liste, „Add provider“, Seite des
  Providers), Anmeldeseite, Profil.
- Test-Anbieter `tests/ReadyStackGo.TestIdentityProvider`, Browsertest gegen den Container, Schritt in „Build & Test“.
- Dokumentation: Website (de/en), `docs/Architecture`, `docs/Configuration`, `docs/Security`.

**Nicht-Umfang**

- Alles, was die Spezifikation ausschließt: Seite von WYSCH (Wiesenwischer/WYSCH#170), Vorlage für ams.Identity (auch
  ihr Ablageort, offene Frage der Spezifikation), Rollen aus Claims, Single Logout, Refresh-Tokens, RFC 7591 als
  eingebaute Art, Vorlagen aus dem Netz, Lokalisierung.
- Löschen der Kopplung bei WYSCH, wenn man einen Provider in ReadyStackGo entfernt (E21).
- Neue Screenshots der übrigen Doku-Seiten; neu entstehen nur die Bilder der geänderten und neuen Seiten (E30).

## 4. Entscheidungen, die in diesem Plan stecken

### Vorlagen-Katalog

- **E1 – Format der Vorlagen** (offene Frage der Spezifikation). Eine Vorlage ist ein Ordner `<id>/` mit `template.json`
  und optional `icon.svg`, wie die Theme-Pakete. Die Id folgt dem Muster der Theme-Ids
  `^[a-z0-9][a-z0-9-]{0,39}\z` (`IThemeCatalog.cs:62`) und ist gleich dem Ordnernamen.

  ```json
  {
    "id": "wysch",
    "name": "WYSCH",
    "description": "Sign in with WYSCH ID. Connects automatically, no client ID or secret to type.",
    "setupDescription": "Sign in with your WYSCH ID. Connects this installation to WYSCH, nothing to copy or type.",
    "order": 10,
    "provider": { "name": "wysch", "displayName": "WYSCH" },
    "authority": { "url": "https://id.wysch.wiesenwischer.de/" },
    "registration": { "kind": "pairing" },
    "requirePar": true,
    "requireHttps": true,
    "scopes": "openid profile email",
    "claims": { "username": "preferred_username", "displayName": "name", "email": "email" },
    "helpUrl": "https://readystackgo.pages.dev/en/docs/configuration/single-sign-on/",
    "offerInSetup": true
  }
  ```

  Pflicht: `id`, `name`, `authority` mit genau einem von `url` (fest) oder `input` (`{ "hint", "example" }`, vom Nutzer
  einzugeben), `registration.kind` einer bekannten Art. Vorgaben: `description` leer, `setupDescription` = `description`
  (eigenes Feld, weil der Entwurf im Wizard und in Settings verschiedene Texte zeigt), `order` zuletzt,
  `provider.name` = `id`, `provider.displayName` = `name`, `requirePar`/`requireHttps`/`offerInSetup` = `false`, `scopes`
  `openid profile email` (muss `openid` enthalten), `claims` mit den Standardnamen. `registration` ist ein Objekt, damit
  eine Art später eigene Optionen bekommen kann, ohne das Format zu brechen. `requireHttps` ist neu gegenüber der
  Feldliste der Spezifikation (E9). Die eingebaute Vorlage `generic-oidc` hat `authority.input` mit Hinweis
  „For Keycloak it ends with /realms/<realm>.“ und Beispiel `https://login.example.com/realms/main` (Texte aus den Rahmen
  `173:3483`/`173:3679`), `registration.kind` `manual`, `provider.name` `oidc`, keinen festen Anzeigenamen.
- **E2 – Herkunft und Konfiguration.** Eingebaut sind `wysch` und `generic-oidc` als Embedded Resources in
  `ReadyStackGo.Infrastructure`, nicht unter `wwwroot` wie die Themes (`Program.cs:33-41`): Distributionen mit eigenem
  Host und eigenem Frontend (`Distribution-Architecture.md:14`) bekommen sie so ohne Zutun. Dazu das Verzeichnis
  `IdentityProviderTemplates:Path` (Standard `/app/identity-provider-templates`, darf fehlen; `Dockerfile` legt es an wie
  `/app/themes`, `Dockerfile:78`) und `IdentityProviderTemplates:Enabled` (Komma-Liste, leer = alle). Eine Vorlage im
  Verzeichnis ersetzt eine eingebaute mit derselben Id. Ungültige Vorlagen werden mit Warnung im Log übersprungen. Wie
  `ThemeCatalog` (`ThemeCatalog.cs:19`, `:94-123`) wird das Ergebnis 30 Sekunden zwischengespeichert: eine neue Vorlage
  erscheint spätestens 30 s nach dem Ablegen, also sicher nach dem Neustart (Abnahme der Spezifikation).
- **E3 – Symbole.** Nur SVG, höchstens 64 KB, ausgeliefert anonym unter `GET /api/identity-provider-templates/{id}/icon`
  (auch die Anmeldeseite zeigt sie) mit `Content-Type: image/svg+xml`, `X-Content-Type-Options: nosniff`,
  `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; sandbox` und ETag wie
  `GetThemeCssEndpoint.cs:45-62`. Die Oberfläche bindet Symbole nur per `<img>` ein, dort laufen keine Skripte. Ohne
  Symbol zeigt sie den Schlüssel (Komponente „Provider Logo/Generic OIDC“ des Entwurfs).
- **E4 – Registrierungsarten als Code.** Schnittstelle in der Application-Schicht:

  ```csharp
  public interface IClientRegistrationMethod
  {
      string Kind { get; }                               // "manual", "pairing"
      RegistrationInteraction Interaction { get; }       // ManualEntry | Connect
      Task<RegistrationStart> StartAsync(RegistrationContext context, CancellationToken ct);   // redirect or completed
      Task<RegisteredClient> CompleteAsync(RegistrationContext context, string code, CancellationToken ct);
  }
  ```

  `ManualEntry` zeigt Redirect-URI, Client-ID und Secret (Schritt „Register“). `Connect` zeigt den Knopf „Connect with
  <Anbieter>“ (Schritt „Connect“); `StartAsync` liefert entweder eine Weiterleitung für den Browser (Kopplung) oder
  sofort ein Ergebnis. So passt später eine weitere Kopplung oder RFC 7591 (ohne Browser) hinein, ohne Einrichtungslauf
  und Vorlagen zu ändern. Die Arten kommen per DI; eine Vorlage mit unbekannter Art wird übersprungen.
  `RegisteredClient` trägt Client-ID, Secret, Zeitpunkt und, falls das Protokoll es liefert, das bestätigende Konto.
- **E5 – Vorlage am Provider.** Beim Anlegen kopiert ReadyStackGo `template`, `registration`, `requirePar` und `claims`
  in den Provider. Zur Laufzeit gilt diese Kopie; der Katalog liefert nur Namen und Symbol für die Anzeige. Fehlt die
  Vorlage später, arbeitet der Provider weiter und zeigt den Schlüssel. Grund: Ändert eine Distribution ihre Vorlage,
  ändert sich das Verhalten bestehender Provider nicht still.

### Provider-Konfiguration

- **E6 – `rsgo.oidc.json`.** Neue Felder sind optional (`OidcConfig.cs:12-32`, gelesen mit CamelCase,
  `ConfigStore.cs:41`): `template` (fehlt → `generic-oidc`), `registration` (fehlt → `manual`), `requirePar`
  (fehlt → `false`), `claims` (fehlt → Standardnamen), `pairedAt`, `pairedBy`, `trustUnverifiedEmail` (fehlt → `true`,
  heutiges Verhalten; neue Provider schreiben `false`), `testedSignIn` (`at` und ein SHA-256-Fingerabdruck über
  Authority, Client-ID, Secret, Scopes), `lastResult` (`at`, `kind` = `checks`/`testSignIn`/`signIn`, `passed`,
  `message`) und `reconnectNeeded`. `OidcSettingsService` liest und schreibt unter einer Sperre, weil nun auch
  Anmeldungen `lastResult` schreiben. Keine Datenbank-Migration: Verknüpfungen bleiben in `UserExternalIdentities`
  (`UserConfiguration.cs:117-146`).
- **E7 – Kennung (Name) nach dem Anlegen fest.** Die Kennung steckt in der Redirect-URI, die beim Anbieter registriert
  ist, und ist der Schlüssel der Verknüpfungen (`ExternalIdentity.Provider`). Sie wird deshalb im Schritt „This
  installation“ festgelegt, wo die Redirect-URI entsteht (Feld „Name“ über der Redirect-URI, die sich live mitändert),
  und ist in „Save“ und auf der Seite des Providers nur lesbar (`Input` im Zustand `Read-only` des Entwurfs). Abweichung
  von den Rahmen `176:4394`, `176:4615` und `176:4851`, die das Feld bearbeitbar zeigen. Neue Namen: Muster wie E1,
  eindeutig ohne Groß/Klein, Vorschlag aus der Vorlage (`wysch`, `oidc`), belegt → `-2`, `-3` … Bestehende Namen aus
  `rsgo.oidc.json` bleiben gültig, auch mit Großbuchstaben (Suche ohne Groß/Klein, `OidcSettingsService.cs:32`).
- **E8 – Basis-Adresse.** Heute schreibt kein Code `BaseUrl`; sie steht auf dem Standard `http://localhost:5000`
  (`SystemConfig.cs:12`), und `GetBaseUrlAsync` liest nur (`SystemConfigService.cs:56-60`). „Nicht gesetzt“ heißt
  deshalb: leer oder gleich diesem Standard. Neu: `ISystemConfigService.GetConfiguredBaseUrlAsync()` (null, wenn nicht
  gesetzt) und `SetBaseUrlAsync()`. Gültig ist eine absolute `http`/`https`-Adresse ohne Query, Fragment und
  Benutzerangabe; ein `/` am Ende wird entfernt. Gespeichert wird beim „Continue“ in „This installation“ (Settings) bzw.
  bei „Connect with WYSCH“ (Wizard). Ist schon eine Basis-Adresse gesetzt und ändert der Nutzer sie, warnt das Feld:
  „Changing the address also changes the redirect URIs of existing providers and the links in emails.“
- **E9 – HTTPS je Vorlage** (Entscheidung im Issue). `requireHttps: true` (WYSCH) verlangt `https` oder `http` auf
  Loopback (`localhost`, `127.0.0.0/8`, `[::1]`). Geprüft im Browser (Knopf gesperrt, Hinweis „HTTPS required“, Rahmen
  `171:2717`) und auf dem Server (Fehler `https_required`). Dieselbe Regel gilt im Schritt „This installation“ unter
  Settings; dort gibt es keinen eigenen Rahmen, Texte wie im Wizard.

### Einrichtungslauf und Verbindungstest

- **E10 – Einrichtungssitzung.** „Add provider“ und die Seite eines Providers (Test, Reconnect, geänderte Verbindung)
  arbeiten auf einer Sitzung auf dem Server: im Speicher (`IMemoryCache`, `Infrastructure/DependencyInjection.cs:182`),
  60 Minuten gleitend, gebunden an den angemeldeten Admin. Sie hält Vorlage, Authority, Name, Anzeigename,
  Basis-Adresse, Client-ID, Secret (verschlüsselt mit `ICredentialEncryptionService`), Scopes und die Ergebnisse von
  Prüfungen und Test-Anmeldung. Kein Endpunkt gibt das Secret zurück. „Cancel“ verwirft die Sitzung ohne Spuren in
  `rsgo.oidc.json`. Ein Neustart verwirft offene Sitzungen (Risiko).
- **E11 – Rücksprünge über den Browser.** Jeder Rücksprung (Kopplung, Test-Anmeldung, Wizard) ist an einen einmaligen
  `state` (10 Minuten, wie heute `OidcEndpoints.cs:120-123`) und an das Cookie `rsgo_sso_flow` gebunden (HttpOnly,
  SameSite=Lax, Path `/api`, Secure bei HTTPS), das beim Start gesetzt wird. Die Kopplung kehrt zu
  `GET /api/sso/registration/callback` zurück, nicht unter `/api/auth/oidc/{provider}/…`, damit kein Provider-Name mit
  der Route kollidiert; sie speichert nur den Code und leitet zur Oberfläche, eingelöst wird er erst im Folgeaufruf der
  Oberfläche (mit JWT bzw. Wizard-Cookie). OIDC kehrt wie heute zu `/api/auth/oidc/{name}/callback` zurück und tauscht
  den Code sofort; `OidcFlowState` (`OidcEndpoints.cs:16`) bekommt einen Zweck: `SignIn` wie heute, `TestSignIn`
  (speichert nur das Ergebnis in der Sitzung), `WizardAdmin` (legt den ersten Admin an, E19). Die normale Anmeldung
  von der Anmeldeseite bleibt ohne Cookie wie heute.
- **E12 – Prüfungen ohne Anmeldung.** Frische Discovery (nicht der Cache von `OidcService.cs:146-152`), je Aufruf
  höchstens 10 s:
  1. Discovery erreichbar (`<authority>/.well-known/openid-configuration` wie `OidcService.cs:144`): eigene Meldungen
     für Name/Verbindung/Zeitüberschreitung, HTTP-Status und ungültiges JSON.
  2. Issuer gleich Authority, genau, nur ein `/` am Ende zählt nicht; die Meldung nennt beide Werte.
  3. Endpunkte für Authorization, Token und JWKS vorhanden.
  4. PAR: verlangt (Vorlage oder `require_pushed_authorization_requests`) → Endpunkt muss da sein; angeboten, nicht
     verlangt → übersprungen „offered, not used“; nicht angeboten → übersprungen.
  5. Client-ID und Secret: nur wenn ein PAR-Endpunkt da ist, per PAR-Aufruf mit der echten Redirect-URI. `201` →
     bestanden; `invalid_client` → „rejected“; `invalid_request`/`invalid_redirect_uri` → „Redirect URI not accepted“
     mit der URI. Ohne PAR übersprungen: „checked during the test sign-in“.

  „n of m checks failed“ zählt nur ausgeführte Prüfungen. Abweichung vom Rahmen `175:4103`: Dort ist PAR „not offered“,
  das Secret aber per PAR abgelehnt, und der Zähler sagt „2 of 5“; nach dieser Regel prüft der PAR-Aufruf nur, wenn PAR
  angeboten wird, und der Zähler sagt „2 of 4“. Der Schritt „Provider address“ führt nur 1 bis 3 aus.
- **E13 – Test-Anmeldung.** Echte Anmeldung beim Anbieter, ohne Sitzung in ReadyStackGo und ohne Verknüpfung. Die Tabelle
  zeigt `sub`, E-Mail mit `email_verified`, Benutzername und Anzeigename nach den Claim-Namen der Vorlage, je mit
  „Received“, „Verified“, „Not verified“ oder „Missing“. Bestanden, wenn Subject und E-Mail da sind (die E-Mail braucht
  ReadyStackGo für Einladungen und die Zuordnung, wie heute `OidcEndpoints.cs:220`); eine unbestätigte E-Mail ergibt eine
  Warnung, keinen Fehlschlag; fehlt die E-Mail, ist der Test nicht bestanden. Möglich erst nach bestandenen Prüfungen.
- **E14 – Speichern und Schutz vor Aussperren**, auf dem Server erzwungen, nicht nur in der Oberfläche:
  - Einschalten nur mit bestandenen Prüfungen und einer bestandenen Test-Anmeldung für die aktuelle Verbindung
    (Fingerabdruck, E6), sonst nur „Save disabled“. Heute eingeschaltete Provider bleiben eingeschaltet.
  - Hat der handelnde Nutzer kein lokales Passwort und ist dieser Provider der einzige eingeschaltete, mit dem er
    verknüpft ist, lehnt der Server Ausschalten und Entfernen ab (409); die Seite warnt, „Enable provider“ ist an und
    gesperrt, „Remove provider“ gesperrt (Rahmen `176:4851`).
  - Eingeschalteter Provider: Geänderte Verbindung (Authority, Client-ID, Secret, Scopes) und neue Zugangsdaten aus
    „Reconnect“ gelten erst nach einer erfolgreichen Test-Anmeldung mit ihnen; dann übernimmt ReadyStackGo sie selbst und
    löscht `reconnectNeeded`. Bis dahin bleibt die alte Verbindung aktiv. „Save changes“ speichert Anzeigename, „Trust
    unverified email addresses“ und Ausschalten.
  - Ausgeschalteter Provider: „Save changes“ speichert alles sofort.
  - Seite eines Generic-OIDC-Providers (ohne eigenen Rahmen): Karte „Connection“ mit bearbeitbarer Authority,
    Client-ID, Secret (leer = behalten) und Scopes, gebaut aus den Komponenten des Schritts „Register“.

### OIDC-Client und Zuordnung

- **E15 – PAR und HTTP.** `OidcService` holt seinen `HttpClient` aus `IHttpClientFactory` (Name `Oidc`, 10 s) statt aus
  dem statischen Feld (`OidcService.cs:18`), damit Tests den Test-Anbieter einhängen können; die Fabrik steht dem
  Security-Projekt über `FrameworkReference Microsoft.AspNetCore.App` zur Verfügung
  (`ReadyStackGo.Infrastructure.Security.csproj:24`; das Framework enthält `Microsoft.Extensions.Http`), kein neues
  Paket. PAR, wenn die Discovery `RequirePushedAuthorizationRequests` meldet oder der Provider `requirePar` hat: POST an
  `PushedAuthorizationRequestEndpoint` mit `client_secret_post` wie heute beim Token (`OidcService.cs:62-73`), danach
  Weiterleitung nur mit `client_id` und `request_uri`. Beide Eigenschaften hat `OpenIdConnectConfiguration` im Paket
  `Microsoft.IdentityModel.Protocols.OpenIdConnect` 8.14.0 (`ReadyStackGo.Infrastructure.Security.csproj:10`). Verlangt,
  aber kein Endpunkt → Fehler `par_not_supported`. Die Methoden liefern Ergebnis-Typen mit Fehlercode
  (`invalid_client`, `unreachable`, …) statt `string` bzw. `null` (`IOidcService.cs:10`, `:22`). Netz- und Formatfehler,
  die heute vor dem `try` als unbehandelte Ausnahme durchschlagen (Discovery `OidcService.cs:60`, `PostAsync` `:75`,
  `JsonDocument.Parse` `:86`), werden zu Fehlercodes; der Callback leitet dann mit `oidc_unreachable` bzw. `oidc_token`
  zur Anmeldeseite statt mit Fehler 500. Claims weiter aus dem ID-Token, dazu Benutzername und Anzeigename nach den
  Claim-Namen des Providers (E5). WYSCH ID verlangt PAR und hat
  keinen Userinfo-Endpunkt (Discovery, abgerufen am 06.10.2026: `require_pushed_authorization_requests: true`,
  `pushed_authorization_request_endpoint` `…/connect/par`, Issuer `https://id.wysch.wiesenwischer.de/`,
  `client_secret_post` angeboten, kein `userinfo_endpoint`).
- **E16 – Zuordnung beim Callback.** Neuer `OidcAccountResolver` (Application-Schicht, ohne HTTP testbar) ersetzt
  `ResolveUser` (`OidcEndpoints.cs:244-290`):
  1. Verknüpfung (Provider, Subject) vorhanden → dieser Benutzer (neu `IUserRepository.FindByExternalIdentity`; der
     eindeutige Index liegt schon auf (Provider, Subject), `UserConfiguration.cs:144-145`).
  2. Sonst nur über die E-Mail und nur, wenn `email_verified` stimmt oder der Provider unbestätigte Adressen vertraut,
     sonst `oidc_email_unverified`. Das gilt für bestehende Benutzer und für offene Einladungen, denn beide werden über
     die E-Mail gefunden.
  3. Bestehender Benutzer mit Verknüpfung zu diesem Provider, aber anderem Subject → `oidc_subject_mismatch` (heute
     wird er trotzdem angemeldet, `OidcEndpoints.cs:253-257`).
  4. Bestehender Benutzer ohne Verknüpfung → verknüpfen; E-Mail nur dann als bestätigt markieren, wenn der Anbieter sie
     bestätigt (heute immer, `OidcEndpoints.cs:259-262`).
  5. Offene Einladung → neuer Benutzer über `User.RegisterExternal` mit neuem Parameter `emailVerified` (heute immer
     bestätigt, `User.cs:111`).
  6. Sonst `oidc_no_account` wie heute.

  Zusätzlich lehnt der Callback ausgeschaltete Konten ab (`oidc_account_disabled`), wie die Anmeldung mit Passwort
  (`AuthenticationService.cs:31`); heute prüft der Callback das nicht. Eine E-Mail, die `EmailAddress` ablehnt (Muster
  `EmailAddress.cs:10`, zum Beispiel mit `+`), ergibt `oidc_email_invalid` statt der heutigen unbehandelten Ausnahme in
  `OidcEndpoints.cs:247`; das Muster selbst ändert der Plan nicht.
- **E17 – Benutzername.** Aus dem Claim `username` des Providers (Standard `preferred_username`), sonst aus dem lokalen
  Teil der E-Mail. Zeichen außer `[a-zA-Z0-9_]` (Regel des Wizards, `CreateAdminValidator.cs:18`) werden zu `_`;
  kürzer als 3 → mit `0` aufgefüllt, länger als 40 → gekürzt (wie `OidcEndpoints.cs:292-305`), eindeutig mit Zahl am
  Ende, höchstens 50 Zeichen (`User.cs:67`). Bleibt nichts Brauchbares, `user`. Neuer Domain-Service
  `UsernameGenerator` für Wizard und Anlage aus Einladung; das Annehmen einer Einladung mit Passwort
  (`AcceptInvitationEndpoints.cs:169-188`) bleibt unverändert.
- **E18 – Widerruf** (offene Frage der Spezifikation, entschieden im Entwurf). `invalid_client` am PAR- oder
  Token-Endpunkt eines gekoppelten Providers setzt `reconnectNeeded` und `lastResult`; die Liste zeigt „Reconnect
  needed“ (Ton `degraded`), bis ein „Reconnect“ mit erfolgreicher Test-Anmeldung durch ist. Die Anmeldeseite zeigt
  `oidc_provider_rejected`. Manuelle Provider bekommen nur `lastResult`.

### Ersteinrichtung

- **E19 – Wizard-Lauf** (`WizardSsoRun` in der Application-Schicht, gespeichert im Speicher):
  - Start `POST /api/wizard/sso/start` prüft wie `CreateAdminEndpoint` (`WizardTimeoutPreProcessor`,
    `CreateAdminEndpoint.cs:24`; kein SystemAdmin, `SystemAdminRegistrationService.cs:36-42`), dazu `offerInSetup`,
    HTTPS (E9) und ob die Discovery erreichbar ist (sonst „WYSCH is not reachable“). Speichert die Basis-Adresse und
    setzt das Cookie des Laufs.
  - Ablauf: `start` → Browser zu WYSCH (Kopplung) → `GET /api/sso/registration/callback` → `/wizard` im Wartezustand →
    `POST /api/wizard/sso/continue` löst den Code ein und speichert den Provider → `GET /api/wizard/sso/sign-in`
    (Browser, mit PAR) → OIDC-Callback mit Zweck `WizardAdmin` legt den Admin an → `/wizard#token=…` → Ergebnis aus
    `GET /api/wizard/sso/status`, dann SMTP wie heute.
  - Der Lauf gilt 15 Minuten ab Start (`Wizard:SsoRunSeconds`, Standard 900). Das beantwortet die offene Frage
    „Kontoanlage im Wizard“: 15 Minuten wie in der Spezifikation, einstellbar. Die Folge-Endpunkte prüfen Cookie,
    Ablauf und „noch kein SystemAdmin“ statt des Zeitfensters; die Sperre, die `GetTimeoutInfoAsync` nach Ablauf des
    Fensters beim nächsten Aufruf setzt (`WizardTimeoutService.cs:133-141`), hält einen laufenden Lauf nicht an.
  - Existiert ein SystemAdmin, meldet `GET /api/wizard/status` den Wizard als abgeschlossen, ohne Zeitfenster, wie im
    Zustand `Installed` (`GetWizardStatusHandler.cs:36-39`). Heute meldet er nach Ablauf des Fensters `NotStarted` und
    nicht abgeschlossen, auch wenn der Admin schon existiert (`:45-52`), und die Oberfläche zeigt dann die Sperrseite.
    Das träfe den WYSCH-Weg regelmäßig, weil sein Lauf länger dauern darf als das Fenster, und betrifft heute schon den
    integrierten Weg, wenn das Fenster während des SMTP-Schritts abläuft.
  - Zustände `Started → Registered → SignedIn`, `Failed(reason)` aus jedem nicht endgültigen Zustand; „Try again“ im
    Lauf von `Failed` aus (abgebrochen oder nicht erreichbar → neu koppeln; E-Mail unbestätigt → neu anmelden);
    `SignedIn` ist endgültig. Ungültige Übergänge werfen.
  - Nach der Kopplung speichert ReadyStackGo den Provider ausgeschaltet (Spezifikation: bleibt bei Abbruch
    ausgeschaltet stehen) unter dem Namen der Vorlage. Gibt es schon einen ausgeschalteten Provider mit diesem Namen und
    dieser Vorlage (früherer, abgebrochener Lauf), ersetzt der neue Lauf dessen Zugangsdaten, sonst nimmt er den
    nächsten freien Namen.
  - Die Anmeldung verlangt `email_verified` (unabhängig von „Trust unverified …“). Den Admin legt
    `SystemAdminRegistrationService.RegisterExternalSystemAdmin` an (ohne Passwort, verknüpft, E-Mail bestätigt,
    SystemAdmin). Integrierter Weg und WYSCH-Weg legen den ersten Admin unter einer gemeinsamen Sperre im Prozess an,
    damit nur einer gewinnt. Danach wird der Provider eingeschaltet und `testedSignIn` gesetzt.
  - Fehler wie im Entwurf: nicht erreichbar, abgebrochen (`error=access_denied`), Lauf abgelaufen, E-Mail unbestätigt,
    anderer Weg schneller („Go to sign-in“), anderer Browser (Cookie fehlt); dazu E-Mail nicht verwendbar (E16) mit dem
    Text von `oidc_email_invalid`.
  - „Use built-in sign-in instead“ beendet den Lauf (`DELETE /api/wizard/sso`) und öffnet das Admin-Formular.
- **E20 – Wizard-Oberfläche.**
  - Schritt 1 gibt es nur, wenn mindestens eine Vorlage `offerInSetup` hat und angeboten wird; sonst beginnt der Wizard
    wie heute mit dem Admin-Formular (Schrittanzeige „Administrator · Email“).
  - Der Countdown ist ab dem Wartezustand und nach dem Anlegen des Admins ausgeblendet, auch im integrierten Weg beim
    SMTP-Schritt. Heute läuft er dort weiter und kann „Setup Window Expired“ zeigen, obwohl der Admin schon existiert
    (`WizardLayout.tsx:25-29`, `Wizard/index.tsx:56-98`).
  - Nach der Rückkehr von WYSCH fragt der Wizard zuerst `GET /api/wizard/sso/status`; einen laufenden Lauf setzt er fort,
    auch wenn das Fenster inzwischen abgelaufen ist.
  - „Try again“ im laufenden Lauf braucht kein Fenster, „Start again“ (Lauf abgelaufen) schon. Ist das Fenster vorbei,
    zeigt die Fehlerkarte statt der beiden Knöpfe den Hinweis zum Neustart mit `docker restart readystackgo` wie die
    heutige Sperrseite (`Wizard/index.tsx:83-86`). Abweichung vom Rahmen `172:2923`, der dort Countdown und „Start
    again“ zeigt.
  - Zustände ohne eigenen Rahmen nutzen die Fehlerkarte mit den Texten der Entwurfs-README (E-Mail unbestätigt, anderer
    Weg schneller, anderer Browser).

### Entfernen, Profil, Notzugang

- **E21 – Provider entfernen.** Entfernt den Provider aus `rsgo.oidc.json` und alle Verknüpfungen zu ihm (neue Methode
  im Repository), damit ein späterer Provider gleichen Namens keine Subjects eines anderen Anbieters erbt. Die Kopplung
  bei WYSCH löscht ReadyStackGo nicht (Antwort auf die offene Frage der Spezifikation); der Bestätigungsdialog eines
  gekoppelten Providers sagt „Remove the connection in WYSCH as well.“
- **E22 – Profil.**
  - `GET /api/user/profile` liefert zusätzlich `hasPassword` und die Daten für den Hinweis; `POST
    /api/user/set-password` setzt ein Passwort nur für Benutzer ohne Passwort (sonst 409), Regeln von
    `HashedPassword.cs:41-56`.
  - Hinweis, solange kein SystemAdmin ein lokales Passwort hat. Das deckt „der einzige SystemAdmin“ der Spezifikation ab
    und auch mehrere Admins ohne Passwort; dann lautet der Titel „No system administrator has a local password“. Im
    Profil sehen ihn nur Admins ohne Passwort.
  - „Unlink“ ohne Passwort gesperrt (Oberfläche, Entwurf); die Regel der Domain (`User.cs:175-179`) bleibt.
  - „Connected accounts“ zeigen Anzeigename und Symbol statt der Kennung (`ConnectedAccounts.tsx:63`):
    `ExternalIdentityDto` bekommt `displayName` und `iconUrl`.
- **E23 – Notzugang.**
  - Befehl `rsgo admin set-password <username> [--generate]`. Das Image bekommt `/usr/local/bin/rsgo`
    (`exec dotnet /app/ReadyStackGo.Api.dll "$@"`). `Program.Main` erkennt `admin …`, bevor der Web-Host entsteht: keine
    Migration, kein Bootstrap, kein Webserver. Dadurch geht bei gestopptem Container auch
    `docker compose run --rm readystackgo admin set-password <username>` (ENTRYPOINT `Dockerfile:95`).
  - Passwort zweimal verdeckt am Terminal, sonst eine Zeile von stdin (`docker compose exec -T …`); `--generate` erzeugt
    20 Zeichen nach den Regeln und zeigt sie einmal.
  - Protokoll: Warnung „Emergency access: local password set for user '<username>' from the command line“ auf der
    Konsole und, soweit beschreibbar, auf `/proc/1/fd/1`, damit sie in `docker logs` steht. Exit-Codes 0, 1 (Benutzer
    unbekannt), 2 (Passwort ungültig), 3 (Datenbank).
  - Die Logik liegt in `AdminCommandLine` in `ReadyStackGo.Infrastructure`, weil Distributionen ein eigenes `Program.cs`
    haben (`Distribution-Architecture.md:150`); dort dokumentiert.

### API und Kompatibilität

- **E24 – Endpunkte.** Neu:
  - Anonym: `GET /api/identity-provider-templates/{id}/icon`, `GET /api/sso/registration/callback`,
    `GET /api/wizard/sso/templates`, `POST /api/wizard/sso/start`, `GET /api/wizard/sso/status`,
    `POST /api/wizard/sso/continue`, `GET /api/wizard/sso/sign-in`, `POST /api/wizard/sso/retry`,
    `DELETE /api/wizard/sso`.
  - SystemAdmin (`[RequireSystemAdmin]` und `RbacPreProcessor` wie `OidcSettingsEndpoints.cs:29`, `:43`):
    `GET /api/settings/oidc/templates`; `POST /api/settings/oidc/setup` (Vorlage oder bestehender Provider),
    `GET`/`PATCH`/`DELETE /api/settings/oidc/setup/{id}`, darunter `POST …/discovery`, `…/installation`,
    `…/registration`, `…/registration/complete`, `…/checks`, `…/test-sign-in`, `…/save`;
    `DELETE /api/settings/oidc/providers/{name}`.
  - Benutzer: `POST /api/user/set-password`.
  - Erweitert, additiv: `GET /api/auth/oidc/providers` (`iconUrl`), `GET /api/settings/oidc` (Vorlage, Kopplung,
    Status, letztes Ergebnis, Hinweis „kein Passwort“), `GET /api/user/profile`, `GET /api/user/external-identities`.
  - Unverändert: die Routen `/api/auth/oidc/{provider}/challenge` und `/callback` (Vorgabe der Spezifikation).
- **E25 – `PUT /api/settings/oidc` entfällt** (`OidcSettingsEndpoints.cs:66-99`), ebenso `saveOidcSettings` in
  `@rsgo/core` (`packages/core/src/api/settings.ts:59-61`). Das Ersetzen der ganzen Liste umginge Schutz vor Aussperren
  (E14), feste Namen (E7) und das Entfernen der Verknüpfungen (E21). Sonst bleibt `@rsgo/core` additiv.

### Oberfläche

- **E26 – Komponenten.** Neu in `packages/ui-generic/src/components/ui/`: `Alert`, `TextField` (Komponente „Input“),
  `CopyField`, `Toggle`, `Stepper`. Neu in `components/sso/`: `ProviderLogo`, `ProviderMark`, `ProviderButton`,
  `SignInOptionCard`, `CheckRow`. Die neuen Symbole des Entwurfs entstehen als Inline-SVG (24×24, Strich 2) in
  `components/sso/icons.tsx`, Formen nach Lucide (ISC), mit Lizenzhinweis in der Datei; kein neues Paket.
  `useRadioKeys` zieht aus `AppearanceSettingsPage.tsx:14-31` nach `hooks/useRadioKeys.ts` und dient auch den Kacheln.
  Wizard und Anmeldeseite wechseln auf die semantischen Tokens (Hinweise der Entwurfs-README). Wiederverwendet:
  `Button` (`Button.tsx:10`), `StatusBadge` (`StatusBadge.tsx:14`), `Logo` (`Logo.tsx:60`).
- **E27 – Texte.** Alle sichtbaren Texte aus den Rahmen, englisch. Für Zustände ohne Rahmen schlägt der Plan vor:
  - Anmeldeseite: `oidc_email_unverified` „Your email address is not confirmed at the sign-in provider. Confirm it
    there, or ask an administrator.“; `oidc_subject_mismatch` „This account is already linked to a different identity
    at this provider. Ask an administrator.“; `oidc_account_disabled` „Your account is disabled.“;
    `oidc_provider_rejected` „The sign-in provider rejected ReadyStackGo. An administrator needs to reconnect it under
    Settings › Single Sign-On.“; `oidc_unreachable` „The sign-in provider is not reachable. Try again later or sign in
    with your password.“; `oidc_email_invalid` „ReadyStackGo cannot use the email address of this account. Ask an
    administrator.“
  - Wizard: die drei Texte der Entwurfs-README; Lauf abgelaufen bei geschlossenem Fenster: „The setup window has
    expired. To try again, restart the container.“
  - Test ohne E-Mail: „The provider sends no email address“ / „ReadyStackGo needs the email claim to match accounts and
    invitations. Request the scope email or set the claim in the template.“

### Tests und CI

- **E28 – Test-Anbieter** `tests/ReadyStackGo.TestIdentityProvider` (ASP.NET Core, Minimal API, JWT mit
  `System.IdentityModel.Tokens.Jwt` 8.14.0 wie im Security-Projekt, kein neues Paket): Discovery mit verlangtem PAR und
  ohne Userinfo, JWKS, PAR, Authorize mit Testnutzern (bestätigte E-Mail; unbestätigte E-Mail; ohne
  `preferred_username`) und „Cancel“, Token mit PKCE-Prüfung, Kopplung nach dem WYSCH-Protokoll, ein Pfad mit falschem
  Issuer, Widerruf eines Clients (Steuerung für Tests), `/health`. Die `Program`-Klasse liegt in einem eigenen
  Namespace, damit `WebApplicationFactory<Program>` der Integrationstests eindeutig bleibt. Die Integrationstests nutzen
  ihn im Prozess, der Browsertest als Container; ins Produkt-Image kommt er nicht.
- **E29 – Browsertest in „Build & Test“.** `docker-compose.sso-e2e.yml` ergänzt `docker-compose.yml` um `test-idp` und
  gibt ReadyStackGo ein Vorlagen-Verzeichnis mit einer Vorlage `wysch` auf `http://test-idp:9090/` (prüft zugleich das
  Ersetzen einer eingebauten Vorlage) und einer weiteren Vorlage, dazu `Wizard__TimeoutSeconds=900`. Die
  Playwright-Konfiguration `playwright.sso.config.ts` startet Chromium mit `--host-resolver-rules=MAP test-idp
  127.0.0.1`, damit Browser und Container den Anbieter unter demselben Issuer erreichen. `scripts/sso-e2e.sh` baut,
  startet, wartet auf `/health`, führt `test:e2e:sso` aus und räumt immer mit `down -v` auf. In `ci.yml` ruft ein
  Schritt am Ende des Jobs „Build & Test“ das Skript (nach `playwright install --with-deps chromium`). Kosten: rund
  8 Minuten mehr je CI-Lauf (Images von ReadyStackGo und Test-Anbieter, Chromium). Nicht gewählt: die API ohne Docker
  starten (schneller, aber nicht „im Container“, wie die Spezifikation verlangt). `sso-*.spec.ts` ist aus den anderen
  Playwright-Konfigurationen ausgenommen wie `onboarding.spec.ts` (`playwright.config.ts:19`).

### Dokumentation und Build

- **E30 – Dokumentation.** Website (de/en): neu `docs/configuration/single-sign-on.md` (Anmeldung mit WYSCH,
  Einrichtungslauf, Test, Reconnect, Schutz vor Aussperren, Notzugang) und `docs/system/identity-provider-templates.md`
  (Format, Verzeichnis, Konfiguration); geändert `docs/configuration/user-access.md` (Abschnitt SSO verweist auf die neue
  Seite) und `getting-started/initial-setup.md` (erster Wizard-Schritt). Bilder aus dem neuen Browsertest. Im Repo: neu
  `docs/Architecture/Identity-Provider-Templates.md` (wie `Themes.md`); korrigiert `docs/Configuration/Overview.md:37`
  („OIDC providers (future)“) und `docs/Security/Overview.md:19`; `docs/Configuration/Config-Files.md` bekommt
  `rsgo.oidc.json`; `docs/Architecture/Distribution-Architecture.md` Vorlagen und den Befehl. `helpUrl` der Vorlagen
  zeigt auf die neue Seite (Basis wie `AppSidebar.tsx:86`).
- **E31 – Build-Ausgabe.** `src/ReadyStackGo.Api/wwwroot` ist eingecheckte Build-Ausgabe (`vite.config.ts:32-33`); die
  Umsetzung baut sie neu und committet sie.

## 5. Oberflächen

Verbindlich: [`docs/specs/identity-provider-vorlagen/entwurf/README.md`](../specs/identity-provider-vorlagen/entwurf/README.md),
Figma-Datei „ReadyStackGo Design“ (`RxVNdSKNs7PpJqkYgvb6a1`), Seite „App“. Theme Türkis hell (`2:1`) und dunkel
(`2:2`), Bezugsbreite 1440 px, keine neuen Variablen. Links nach dem Muster
`https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=<a>-<b>`; Bilder unter `../specs/identity-provider-vorlagen/entwurf/`.
Abweichungen stehen unter E7, E12 und E20.

| Oberfläche | Zustand | Rahmen | Bild | Was gebaut wird |
|---|---|---|---|---|
| Wizard, Schritt 1 | nichts gewählt | [`171:2417`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=171-2417) | `wizard-anmeldeart-leer.png` | `SignInMethodStep` mit `SignInOptionCard` je Vorlage + „Built-in sign-in“, „Continue“ gesperrt |
| Wizard, Schritt 1 | WYSCH gewählt, hell/dunkel | [`171:2534`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=171-2534), [`177:5565`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5565) | `wizard-anmeldeart-wysch.png`, `…-dunkel.png` | Auswahl wie Radio-Gruppe (`useRadioKeys`) |
| Wizard, WYSCH-Weg | Adresse, „Connect with WYSCH“ | [`171:2624`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=171-2624) | `wizard-wysch-adresse.png` | `SsoAdminStep`, Kasten „What happens“, `ProviderButton` |
| Wizard, WYSCH-Weg | ohne HTTPS | [`171:2717`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=171-2717) | `wizard-wysch-ohne-https.png` | Regel E9, `TextField` Error, `Alert` Warning |
| Wizard, WYSCH-Weg | Wartezustand, hell/dunkel | [`171:2802`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=171-2802), [`177:5590`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5590) | `wizard-wysch-kopplung-laeuft.png`, `…-dunkel.png` | drei Zeilen aus `GET /api/wizard/sso/status`, ohne Countdown |
| Wizard, WYSCH-Weg | „Signed in as …“ | [`172:2728`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=172-2728) | `wizard-wysch-angemeldet.png` | Ergebnis aus dem Status des Laufs, „Continue“ zu SMTP |
| Wizard, WYSCH-Weg | nicht erreichbar | [`172:2794`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=172-2794) | `wizard-wysch-fehler-nicht-erreichbar.png` | Fehlerkarte (auch für die Zustände ohne Rahmen, E20) |
| Wizard, WYSCH-Weg | abgebrochen | [`172:2859`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=172-2859) | `wizard-wysch-fehler-abgebrochen.png` | „Try again“ im Lauf |
| Wizard, WYSCH-Weg | Lauf abgelaufen | [`172:2923`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=172-2923) | `wizard-wysch-fehler-zeitfenster.png` | „Start again“ bzw. Neustart-Hinweis (E20), Info „stays set up, but disabled“ |
| SSO, Liste | leer | [`173:2891`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=173-2891) | `sso-liste-leer.png` | `OidcSettingsPage` neu |
| SSO, Liste | drei Provider, „Reconnect needed“, Hinweis, hell/dunkel | [`173:3055`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=173-3055), [`177:5611`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5611) | `sso-liste.png`, `…-dunkel.png` | Zeilen mit `ProviderMark`, `StatusBadge`, letztem Ergebnis, Knöpfen |
| Add provider, „Template“ | WYSCH gewählt | [`173:3277`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=173-3277) | `sso-lauf-vorlage.png` | `AddOidcProviderPage`, `Stepper`, Schritte aus der Vorlage |
| Add provider, „Provider address“ | gefunden / nicht gefunden | [`173:3483`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=173-3483), [`173:3679`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=173-3679) | `sso-lauf-anbieter-adresse.png`, `…-fehler.png` | Prüfungen 1–3 (E12) |
| Add provider, „This installation“ | ohne Basis-Adresse | [`174:3431`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=174-3431) | `sso-lauf-installation.png` | Basis-Adresse (E8), Feld „Name“ (E7), `CopyField` |
| Add provider, „Connect“ | vorher, hell/dunkel | [`174:3627`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=174-3627), [`177:5698`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5698) | `sso-lauf-koppeln.png`, `…-dunkel.png` | Art `Connect` (E4) |
| Add provider, „Connect“ | gekoppelt | [`174:3829`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=174-3829) | `sso-lauf-gekoppelt.png` | `Alert` Success, „Connect again“ |
| Add provider, „Register“ | manuell | [`174:4021`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=174-4021) | `sso-lauf-manuell.png` | Art `ManualEntry`, Secret als Passwortfeld |
| Add provider, „Save“ | Test bestanden / ohne Test | [`176:4394`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=176-4394), [`176:4615`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=176-4615) | `sso-lauf-speichern.png`, `…-ohne-test.png` | `Toggle`, Vorschau `ProviderButton`, Name nur lesbar (E7) |
| Seite des Providers (WYSCH) | Schutz vor Aussperren, Secret abgelehnt | [`176:4851`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=176-4851) | `sso-provider-wysch.png` | `OidcProviderPage` (E14) |
| Test, Prüfungen | WYSCH bestanden / Generic fehlgeschlagen | [`175:3875`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=175-3875), [`175:4103`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=175-4103) | `sso-test-pruefungen-ok.png`, `…-fehler.png` | `CheckRow`, Zähler nach E12 |
| Test-Anmeldung | reicht, hell/dunkel | [`175:4351`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=175-4351), [`177:5774`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5774) | `sso-test-claims-ok.png`, `…-dunkel.png` | Claims-Tabelle (E13), Prüfungen eingeklappt |
| Test-Anmeldung | E-Mail unbestätigt, Name fehlt | [`175:4606`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=175-4606) | `sso-test-claims-unvollstaendig.png` | Warnung, Hinweis zum Benutzernamen (E17) |
| Anmeldeseite | WYSCH + Generic, hell/dunkel | [`177:4768`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-4768), [`177:5892`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5892) | `login-provider.png`, `…-dunkel.png` | `ProviderButton` mit Symbol, rechte Hälfte mit `Logo` und `nav/bg` dunkel |
| Profil | ohne Passwort, hell/dunkel | [`177:4834`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-4834), [`177:5913`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=177-5913) | `profil-ohne-passwort.png`, `…-dunkel.png` | „Set a local password“, „Password: Not set“, „Unlink“ gesperrt (E22) |

Komponenten (Seite „Components“, Node-IDs in der Entwurfs-README): Provider Logo (WYSCH `168:246`, Generic OIDC
`168:247`, Built-in `168:251`), Provider Mark `168:271`, Sign-in Option `169:233`, Stepper Item `169:248`, Check Row
`169:277`, Alert `169:309`, Input `170:249`, Copy Field `170:269`, Toggle `170:276`, Provider Button `170:298`; neue
Symbole im Rahmen „Icons“ (`3:2`). Gebaut nach E26.

**Raster-Icons:** keine (Entwurfs-README, „Symbole und Raster-Icons“). Das WYSCH-Symbol ist das Logo aus
`Wiesenwischer/WYSCH`, `brand/wysch-symbol.svg`; es kommt als `icon.svg` in die eingebaute Vorlage `wysch`
(Zugang: Offene Frage F1). Bis dahin zeigt die Vorlage den Schlüssel.

## 6. Schritte der Umsetzung

1. **Domain** – `User.RegisterExternal(…, emailVerified)` (`User.cs:102-113`); `IUserRepository.FindByExternalIdentity`
   und eine Methode, die alle Verknüpfungen zu einem Provider entfernt (`IUserRepository.cs`, `UserRepository.cs`);
   `UsernameGenerator` (E17); `SystemAdminRegistrationService.RegisterExternalSystemAdmin` und die gemeinsame Sperre
   für den ersten Admin (E19).
2. **Konfiguration** – `OidcConfig`/`OidcProviderConfig` mit den Feldern aus E6, `OidcProviderSettings` und
   `OidcSettingsService` mit Einzeloperationen (anlegen, ändern, entfernen, Ergebnis schreiben), Sperre und den
   Vorgaben für alte Einträge; `ISystemConfigService.GetConfiguredBaseUrlAsync`/`SetBaseUrlAsync` (E8).
3. **Vorlagen-Katalog** – `IIdentityProviderTemplateCatalog` und Modell (Application,
   `Services/IdentityProviders/`), `IdentityProviderTemplateCatalog` und `IdentityProviderTemplateOptions`
   (Infrastructure, `Services/IdentityProviders/`), eingebaute Vorlagen als Embedded Resources, Registrierung neben
   `ThemeCatalog` (`Infrastructure/DependencyInjection.cs:80-81`), `appsettings.json` Abschnitt
   `IdentityProviderTemplates`, `Dockerfile` (Verzeichnis), Kommentar in `docker-compose.yml` wie bei den Themes
   (`docker-compose.yml:40-44`), Endpunkte für Liste und Symbol.
4. **OIDC-Client** – `OidcService` nach E15 (Fabrik, PAR, Ergebnis-Typen, Claims), `IOidcService` anpassen; benannter
   Client `Oidc`; `OidcConnectionChecker` (E12) im Security-Projekt.
5. **Zuordnung** – `OidcAccountResolver` (E16) in der Application-Schicht; `OidcEndpoints.cs` umbauen: Zweck im
   `OidcFlowState`, Fehlercodes, `reconnectNeeded` (E18), `iconUrl` in der Liste der Provider.
6. **Registrierungsarten** – `IClientRegistrationMethod` (E4), `ManualRegistrationMethod`,
   `PairingRegistrationMethod` nach `client-kopplung.md` von WYSCH (F1), `GET /api/sso/registration/callback`.
7. **Einrichtungssitzungen und Settings-Endpunkte** – Sitzung (E10) mit Speicher, Endpunkte aus E24,
   Speicherregeln (E7, E14), Entfernen (E21); `PUT /api/settings/oidc` entfernen (E25).
8. **Wizard-Lauf** – `WizardSsoRun` und Speicher (E19), Endpunkte `/api/wizard/sso/*`, Admin über WYSCH;
   `GetWizardStatusHandler` meldet bei vorhandenem Admin „abgeschlossen“ (E19).
9. **Profil** – `GetProfileEndpoint` (`hasPassword`, Hinweis), `SetPasswordEndpoint`, `ExternalIdentityDto` (E22).
10. **Notzugang** – `AdminCommandLine` (E23), Aufruf am Anfang von `Program.Main`, `rsgo` im `Dockerfile`.
11. **`@rsgo/core`** – `api/identityProviders.ts` (Vorlagen, Sitzungen, Provider), Wizard-Aufrufe in `api/wizard.ts`,
    `api/user.ts` (`hasPassword`, `setPassword`, `ExternalIdentityDto`), `api/auth.ts` (`iconUrl`),
    `saveOidcSettings` entfernen; Exporte in `src/index.ts`.
12. **Komponenten** – E26; `useRadioKeys` verschieben und in `AppearanceSettingsPage.tsx` nutzen.
13. **Seiten** – Wizard (`Wizard/index.tsx`, `WizardLayout.tsx`, neu `SignInMethodStep.tsx`, `SsoAdminStep.tsx`,
    reine Logik `wizardFlow.ts`; `AdminStep.tsx`, `SmtpStep.tsx`, `WizardCountdown.tsx` auf Tokens); Settings
    (`Oidc/OidcSettingsPage.tsx` neu, `AddOidcProviderPage.tsx`, `OidcProviderPage.tsx`, Schritte unter `Oidc/steps/`,
    reine Logik `setupSteps.ts`); Routen `/settings/oidc/add` und `/settings/oidc/providers/:name` in
    `apps/rsgo-generic/src/App.tsx` neben `:403`; Anmeldeseite (`Login.tsx`: Fehlertexte `:6-12`, Knöpfe `:161-169`,
    rechte Hälfte `:178-203`); Profil (`Profile.tsx`, `ConnectedAccounts.tsx`, neu `SetLocalPasswordCard.tsx`).
14. **Test-Anbieter und Browsertest** – Projekt (E28) in `ReadyStackGo.sln`, `Dockerfile` des Test-Anbieters,
    `docker-compose.sso-e2e.yml`, Vorlagen unter `e2e/test-data/identity-provider-templates/`,
    `playwright.sso.config.ts`, Skript `test:e2e:sso` in `package.json`, `scripts/sso-e2e.sh`, `e2e/sso-wysch.spec.ts`;
    `testIgnore` in den anderen Konfigurationen; bestehende Specs anpassen (Abschnitt 7).
15. **CI** – Schritt in `.github/workflows/ci.yml` (E29, F2).
16. **Dokumentation** – E30.
17. **Build-Ausgabe** – `pnpm run build`, `wwwroot` committen (E31).

## 7. Tests und Abnahme

Neue Tests sind ohne die Änderung rot, weil Klassen und Endpunkte fehlen. Inhaltlich rot gegen das heutige Verhalten
sind besonders: anderes Subject wird abgelehnt (heute angemeldet), unbestätigte E-Mail wird bei neuem Provider nicht
zugeordnet (heute zugeordnet), PAR bei verlangter Discovery (heute nie), ausgeschaltetes Konto per OIDC abgelehnt,
Netzfehler und E-Mail mit `+` beim Callback führen zur Anmeldeseite (heute Fehler 500), Status mit vorhandenem Admin
nach Ablauf des Fensters ist abgeschlossen (heute `NotStarted`), Countdown nach dem Anlegen des Admins weg. Ein
Regressionstest für alte `rsgo.oidc.json`-Einträge ist vorher und nachher grün.

**Unit (`tests/ReadyStackGo.UnitTests`, xUnit, FluentAssertions, Moq)**

- `IdentityProviderTemplateCatalogTests`: eingebaute Vorlagen aus den Embedded Resources; Verzeichnis fehlt → nur
  eingebaute; gleiche Id ersetzt; ungültige Id, Id ≠ Ordner, kaputtes JSON, unbekannte Art, `authority` mit beidem oder
  keinem, `scopes` ohne `openid` → übersprungen; `Enabled` filtert, unbekannte Ids werden ignoriert; Symbol zu groß oder
  kein SVG → ohne Symbol; Sortierung nach `order`; leerer Katalog ohne Fehler.
- `OidcAccountResolverTests`, alle Fälle der Spezifikation: Subject bekannt (auch bei geänderter E-Mail); nur E-Mail,
  bestätigt → verknüpft; E-Mail unbestätigt, Vertrauen aus → abgelehnt, an → verknüpft, E-Mail bleibt unbestätigt;
  anderes Subject → abgelehnt; keine Einladung → `oidc_no_account`; Einladung bestätigt → angelegt mit Rolle der
  Einladung; Einladung unbestätigt, Vertrauen aus → abgelehnt; E-Mail fehlt; ausgeschaltetes Konto.
- `UsernameGeneratorTests`: gültig; Punkt/Bindestrich/Plus → `_`; zu kurz; zu lang; belegt → Zahl; leer → E-Mail;
  beides unbrauchbar → `user`; Ergebnis nie über 50 Zeichen.
- `OidcServiceTests` (Fake-`HttpMessageHandler`): PAR bei Discovery-Pflicht und bei `requirePar`; Weiterleitung nur
  mit `client_id` und `request_uri`; PAR verlangt ohne Endpunkt; kein PAR, wenn nicht verlangt; `invalid_client` an PAR
  und Token; Claims nach eigenen Claim-Namen; `email_verified` als `true`/`"true"`/fehlend.
- `OidcConnectionCheckerTests`: Discovery 404, Zeitüberschreitung, kein JSON; Issuer mit/ohne `/`, anderer Groß/Klein
  → Fehler; Endpunkte fehlen; PAR verlangt und fehlt; Secret abgelehnt; Redirect-URI abgelehnt; ohne PAR übersprungen;
  Zähler nur über ausgeführte Prüfungen.
- `WizardSsoRunTests` (`FakeTimeProvider`): gültige Übergänge; jeder ungültige Übergang (z. B. `Started → SignedIn`,
  zweimal registrieren, nach `SignedIn`) wirft; Ablauf nach 900 s; „Try again“ nur aus `Failed` und nur vor Ablauf.
- `OidcSettingsServiceTests`: alte JSON ohne neue Felder → `generic-oidc`, `manual`, Vertrauen an; neuer Provider →
  Vertrauen aus; Secret verschlüsselt, nie im Klartext in der Datei; Fingerabdruck ändert sich mit jedem
  Verbindungsfeld; Entfernen.
- `SystemConfigServiceBaseUrlTests` und `BaseUrlRulesTests`: Standard `http://localhost:5000` gilt als nicht gesetzt;
  Normalisierung; ungültige Werte; HTTPS-Regel (`https` ja; `http://localhost`, `127.0.0.1`, `[::1]` ja;
  `http://server:8080`, `http://localhost.example.com` nein).
- Domain: `RegisterExternal` mit `emailVerified: false` bestätigt nicht; `RegisterExternalSystemAdmin` wirft, wenn
  schon ein SystemAdmin existiert, und legt sonst ohne Passwort, verknüpft und bestätigt an.
- `AdminCommandLineTests`: Argumente (fehlender Name, unbekannte Option); Benutzer unbekannt → 1; Passwort zu schwach →
  2; `--generate` erfüllt die Regeln; Passwort gesetzt und mit `HashedPassword.Verify` prüfbar.

**Integration (`tests/ReadyStackGo.IntegrationTests`, `CustomWebApplicationFactory` mit eigenem Pfad,
`CustomWebApplicationFactory.cs:37-46`; Test-Anbieter im Prozess über den benannten Client `Oidc`)**

- Anmeldung: Challenge nutzt PAR; Callback ordnet über das Subject zu; `invalid_client` bei gekoppeltem Provider setzt
  `reconnectNeeded` und leitet mit `oidc_provider_rejected`; Anbieter beim Token-Austausch nicht erreichbar →
  Weiterleitung mit `oidc_unreachable`; E-Mail mit `+` → `oidc_email_invalid`; alter Eintrag aus `rsgo.oidc.json`
  meldet unverändert an.
- Einrichtung: Lebenszyklus der Sitzung; fremder Admin sieht fremde Sitzung nicht (404); Discovery 404; Secret falsch,
  Authority falsch, Issuer falsch → je eigener Code; Speichern eingeschaltet ohne Test → 409, ausgeschaltet → ok; Name
  ungültig oder belegt → 400/409; Verbindung ändern am eingeschalteten Provider ohne Test → bleibt alt, nach Test neu;
  eigener Provider ohne Passwort ausschalten/entfernen → 409; Entfernen löscht Verknüpfungen; keine Antwort enthält das
  Secret.
- Vorlagen: Liste mit eingebauten; Vorlage aus dem Verzeichnis erscheint; `wysch` im Verzeichnis ersetzt die eingebaute;
  `Enabled` filtert; Symbol mit Content-Type, CSP und `nosniff`; unbekannte oder ungültige Id (`..`, Großbuchstaben) →
  404.
- Wizard: Start nach Ablauf des Fensters → 403; Start mit vorhandenem SystemAdmin → abgelehnt; `http://server:8080` →
  `https_required`; Start setzt Cookie und Basis-Adresse; `continue` ohne Cookie (zweiter Browser) → 403; nach 900 s →
  abgelaufen; ganzer Lauf mit dem Test-Anbieter → Admin ohne Passwort, verknüpft, E-Mail bestätigt, Provider
  eingeschaltet; Lauf läuft weiter, wenn das Fenster während des Laufs abläuft; E-Mail unbestätigt → kein Admin;
  anderer Weg legt inzwischen einen Admin an → `completed_elsewhere`; `GET /api/wizard/status` nach Ablauf des
  Fensters mit vorhandenem Admin → abgeschlossen, ohne Zeitfenster.
- Profil: `set-password` ohne Passwort → ok, danach Anmeldung mit Passwort; mit Passwort → 409; `hasPassword` und
  Hinweis-Daten.

**Frontend (Vitest, reine Logik, `vitest.config.ts:7`)**

- `setupSteps.test.ts`: WYSCH → Template, This installation, Connect, Test, Save; Generic OIDC → mit Provider address
  und Register; „Continue“-Sperren je Schritt.
- `wizardFlow.test.ts`: keine Auswahl → gesperrt; integrierter Weg; WYSCH-Weg; Zustände des Laufs → Ansichten; Fenster
  zu, Lauf aktiv → weiter; Fenster zu, Lauf abgelaufen → Neustart-Hinweis.
- `baseUrl.test.ts`: Vorschlag aus `window.location.origin`, Normalisierung, HTTPS-Regel wie im Backend.

**Browsertest (`e2e/sso-wysch.spec.ts`, Container mit Test-Anbieter, E29)**, der Reihe nach: Wizard mit WYSCH bis zum
Abschluss (SMTP übersprungen), unterwegs Test-Anbieter gestoppt („not reachable“) und Kopplung abgebrochen, je mit „Try
again“; Abmelden und Anmelden mit WYSCH; Liste mit Hinweis „kein Passwort“; Add provider › Generic OIDC gegen den
Test-Anbieter mit falscher Authority, falschem Issuer, falschem Secret und dann richtig, Test-Anmeldung mit
unbestätigter E-Mail, Speichern; Anmeldung mit unbestätigter E-Mail über den neuen Provider wird nicht zugeordnet;
Widerruf beim Test-Anbieter → „Reconnect needed“ → Reconnect; Test-Anbieter stoppen, `rsgo admin set-password`,
Anmeldung mit Passwort; „Set a local password“ und „Unlink“ im Profil.

Angepasst: In `wizard.spec.ts` wählen alle Tests, die `/wizard` öffnen und das Admin-Formular erwarten oder bedienen,
zuerst „Built-in sign-in“ (`:31-32`, `:40`, `:44`, `:71`, `:94`, `:106`, `:115-122`, `:133-134`, `:145`), ebenso
`onboarding.spec.ts:32`. Dabei füllen die Tests bei `:94`, `:106` und `:145` auch das Pflichtfeld E-Mail, das sie heute
auslassen, obwohl `AdminStep` es vor dem Passwort prüft (`AdminStep.tsx:26-29`; nach dem Code, nicht ausgeführt).
`auth-email-oidc.spec.ts:49` erwartet die neue Überschrift „Single Sign-On“ (die Karte unter Settings, `:33`, behält
ihren Titel). Die übrigen Container-Specs bleiben grün.

**Bilder im Umsetzungs-PR**, aus den Browsertests: Wizard Schritt 1 (leer, WYSCH gewählt hell/dunkel), WYSCH-Weg
(Adresse, ohne HTTPS, Wartezustand, angemeldet, abgebrochen, nicht erreichbar); SSO-Liste leer (integrierter Weg) und mit
Hinweis und „Reconnect needed“ hell/dunkel; Add provider in jedem Schritt beider Vorlagen, Prüfungen bestanden und
fehlgeschlagen, Test-Anmeldung vollständig und unvollständig, Speichern mit und ohne Test; Seite des WYSCH-Providers mit
Warnung; Anmeldeseite hell/dunkel; Profil ohne Passwort hell/dunkel. Zustände ohne Weg im Browsertest (Lauf abgelaufen,
anderer Browser, anderer Weg schneller) belegen die Integrationstests.

**Pflicht-Checks:** „Build & Test“ (`ci.yml:20-94`) mit dem neuen Schritt, dazu die Browsertests gegen den Container
nach `CLAUDE.md`.

**Abnahme gegen WYSCH ID** (Spezifikation: Kopplung und Anmeldung mit verlangtem PAR): von Hand durch Marcus, sobald
Wiesenwischer/WYSCH#170 live ist; CI ruft WYSCH nie auf.

## 8. Wo umgesetzt wird

GitHub-Runner (`ubuntu-latest`): Build, alle Tests, Browsertest mit Docker. Zwei Punkte hängen nicht am Runner:
Ob die Umsetzung `ci.yml` pushen darf (F2), und die Abnahme gegen die echte WYSCH ID (Marcus, nach WYSCH#170). Eine
Umsetzungsrolle braucht es nicht.

## 9. Risiken

- Nicht nachgelesen: das Protokoll der Kopplung, `docs/specs/client-kopplung.md` in Wiesenwischer/WYSCH (das Repo
  antwortet diesem Lauf mit 404). Annahme nach der Spezifikation: wie der GitHub-App-Manifest-Flow – Formular-POST einer
  Beschreibung (Name, Basis-Adresse, Redirect-URI) mit `state` an WYSCH, Rücksprung mit `code` und `state` (bei Abbruch
  `error=access_denied`), Einlösen des Codes vom Server, Antwort mit Client-ID und Secret. Weicht es ab, ändern sich
  `PairingRegistrationMethod` und der Test-Anbieter, nicht der Einrichtungslauf (E4).
- Nicht nachgelesen: ob die Antwort der Kopplung das bestätigende Konto nennt. Annahme: vielleicht nicht; dann zeigen
  Liste und Seite nur das Datum („Connected Oct 6, 2026“) statt „by alex@example.com“.
- Nicht nachgelesen: welche Claims das ID-Token von WYSCH ID enthält. Die Discovery nennt in `claims_supported` nur
  `aud`, `exp`, `iat`, `iss`, `sub`. Annahme: mit `openid profile email` kommen `email`, `email_verified`,
  `preferred_username` und `name` im ID-Token (Spezifikation: „Claims liest der Client weiter aus dem ID-Token“).
- Nicht nachgelesen: ob ein erneutes Koppeln bei WYSCH die alten Zugangsdaten sofort ungültig macht. Annahme: ja; dann
  ist ein Provider bis zur erfolgreichen Test-Anmeldung nach „Reconnect“ nicht nutzbar, und ein Neustart dazwischen
  verwirft die neuen Zugangsdaten mit der Sitzung (E10), sodass nur ein weiteres „Reconnect“ hilft.
- Nicht nachgelesen: ob `GITHUB_TOKEN` des Vorhaben-Workflows (`vorhaben.yml:36-40`, `contents: write`) Änderungen an
  `.github/workflows/` pushen darf. Annahme: nein, GitHub verlangt dafür das Recht `workflows`, das dieses Token nicht
  bekommt (F2).
- Nicht nachgelesen (privates Repo): ob `@rsgo/ui-ams` der Distribution `saveOidcSettings` oder `PUT
  /api/settings/oidc` nutzt (E25). Annahme: nein.
- Nicht nachgelesen: ob `/proc/1/fd/1` unter einem Container ohne Root (OpenShift) beschreibbar ist. Annahme: oft nicht;
  dann steht die Zeile nur in der Ausgabe des Befehls.
- Distributionen mit eigenem `Program.cs` haben den Befehl erst, wenn sie den Aufruf von `AdminCommandLine` übernehmen
  (E23).
- Größe: Viele Endpunkte und drei neue Seiten in einem PR; die Prüfung wird lang. Die Schritte sind so geordnet, dass
  Commits einzeln prüfbar sind.
- CI dauert rund 8 Minuten länger (E29).
- Benutzernamen neuer Konten aus OIDC sehen anders aus als bisher (`first.last` → `first_last`, E17).
- Konten mit einer E-Mail, die `EmailAddress` ablehnt (zum Beispiel mit `+`, `EmailAddress.cs:10`), können sich weiter
  nicht per OIDC anmelden und auch im Wizard nicht erster Admin werden; sie bekommen nur eine verständliche Meldung
  (E16).
- Ändert ein Admin eine schon gesetzte Basis-Adresse, passen die Redirect-URIs bestehender Provider nicht mehr; das Feld
  warnt (E8).
- Symbole aus einem fremden Verzeichnis sind SVG; ausgeliefert mit CSP und nur per `<img>` eingebunden (E3).

## 10. Offene Fragen

- **F1 – Zugang zu WYSCH für die Umsetzung.** Protokoll der Kopplung (`docs/specs/client-kopplung.md`, ergänzt mit
  Wiesenwischer/WYSCH#172) und Symbol (`brand/wysch-symbol.svg`) liegen im Repo Wiesenwischer/WYSCH, das dieser Lauf
  nicht lesen kann; der Runner der Umsetzung vermutlich auch nicht. Wie kommen sie zur Umsetzung: Lesezugriff für den
  Workflow, oder eine Kopie in diesem Repo (zum Beispiel unter `docs/specs/identity-provider-vorlagen/`)? Und ist das
  Protokoll fertig, oder ändert es sich mit WYSCH#170 noch?
- **F2 – Browsertest in „Build & Test“.** Die Spezifikation verlangt ihn dort, also muss `ci.yml` sich ändern. Kann die
  Umsetzung auf dem Runner das nicht pushen (Risiko oben): Trägst du den einen Schritt nach dem Merge selbst ein (die
  Umsetzung legt `scripts/sso-e2e.sh` bei und nennt den Schritt im PR), oder soll die Umsetzung in einer Sitzung am PC
  laufen, oder bekommt der Workflow ein Token mit dem Recht `workflows`?
