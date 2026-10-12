# Identity-Provider-Vorlagen und Single Sign-On

ReadyStackGo meldet Benutzer über OpenID-Connect-Anbieter an. Jeder Provider entsteht aus einer **Vorlage** (Template),
die den Anbieter beschreibt; der Einrichtungslauf unter Settings › Single Sign-On und der erste Schritt des
Setup-Wizards lesen alles Anbieterspezifische aus ihr. Spezifikation: `docs/specs/identity-provider-vorlagen.md`,
Plan: `docs/plans/identity-provider-vorlagen.md`. Benutzer-Doku: Website, „Single Sign-On“ und „Identity Provider
Templates“.

## Katalog

- `IIdentityProviderTemplateCatalog` (Application), `IdentityProviderTemplateCatalog` (Infrastructure,
  `Services/IdentityProviders/`), Muster wie `ThemeCatalog`.
- **Eingebaut** sind `wysch` und `generic-oidc` als Embedded Resources unter `Services/IdentityProviders/BuiltIn/`
  (LogicalName `IdentityProviderTemplates/<id>/<datei>`), damit Distributionen mit eigenem Host und Frontend sie ohne
  Zutun bekommen.
- **Verzeichnis** `IdentityProviderTemplates:Path` (Standard `/app/identity-provider-templates`, darf fehlen); eine
  Vorlage dort ersetzt eine eingebaute mit derselben Id. `IdentityProviderTemplates:Enabled` schränkt ein
  (Komma-Liste, leer = alle). Ungültige Vorlagen werden mit Warnung übersprungen; das Ergebnis wird 30 s
  zwischengespeichert.
- Format: Ordner `<id>/` mit `template.json` und optional `icon.svg` (nur SVG, höchstens 64 KB). Felder siehe
  Website-Doku. Symbole liefert `GET /api/identity-provider-templates/{id}/icon` anonym mit
  `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; sandbox` und `nosniff`; die Oberfläche
  bindet sie nur per `<img>` ein.
- Beim Anlegen kopiert ReadyStackGo Vorlage, Registrierungsart, `requirePar` und Claim-Namen in den Provider
  (`rsgo.oidc.json`). Zur Laufzeit gilt diese Kopie; der Katalog liefert nur Name und Symbol.

## Registrierungsarten

`IClientRegistrationMethod` (Application) mit `Kind`, `Interaction` (`ManualEntry` oder `Connect`), `StartAsync`
und `CompleteAsync`. Eingebaut (Security-Projekt, `IdentityProviders/RegistrationMethods.cs`):

- `manual`: Redirect-URI kopieren, Client-ID und Secret eintragen.
- `pairing`: Kopplung nach dem Protokoll von WYSCH ID (verbindlich: `Wiesenwischer/WYSCH`, `docs/CLIENT-PAIRING.md`;
  Regeln für ReadyStackGo in #498, Kommentar vom 06.10.2026): Formular-POST `manifest` (kind, name, url,
  redirect_uri, return_uri) und `state` an `wysch_pairing_endpoint`; Rücksprung mit `code` und `state` (oder `error=access_denied`/`limit_reached`) nach
  `GET /api/sso/registration/callback`; Einlösen `code` + `state` an `wysch_pairing_token_endpoint` →
  `client_id`, `client_secret`, `issuer`. Beide Endpunkte kommen aus der Discovery.

Weitere Arten (andere Kopplung, RFC 7591) kommen per DI dazu, ohne Einrichtungslauf und Vorlagen zu ändern.

## Ablauf und Zustand

- **Einrichtungssitzungen** (`SsoSetupSession`, im Speicher, 60 Minuten gleitend, an den Admin gebunden) halten
  Vorlage, Authority, Name, Basis-Adresse, Client-ID, Secret (verschlüsselt), Prüfungen und Test-Anmeldung.
  Endpunkte unter `/api/settings/oidc/setup/...`. „Cancel“ verwirft die Sitzung ohne Spuren.
- **Rücksprünge über den Browser** (Kopplung, Test-Anmeldung, Wizard) sind an einen einmaligen `state` (10 Minuten)
  und das Cookie `rsgo_sso_flow` (HttpOnly, SameSite=Lax, Path `/api`) gebunden. `OidcFlowState` trägt einen Zweck:
  `SignIn`, `TestSignIn`, `WizardAdmin`.
- **Verbindungstest** (`OidcConnectionChecker`): Discovery, Issuer, Endpunkte, PAR, Client-Daten per PAR-Aufruf.
  **Test-Anmeldung**: echte Anmeldung ohne Sitzung in ReadyStackGo, Ergebnis mit Fingerabdruck der Verbindung.
- **Schutz vor Aussperren** (auf dem Server): Einschalten nur mit bestandenen Prüfungen und Test-Anmeldung für die
  aktuelle Verbindung; der einzige Anmeldeweg eines Benutzers ohne Passwort lässt sich nicht ausschalten oder
  entfernen; geänderte Zugangsdaten eines eingeschalteten Providers gelten erst nach einer erfolgreichen
  Test-Anmeldung, außer beim Reconnect gekoppelter Provider (nächster Punkt). Eine Sitzung überschreibt keine
  Verbindung, die seit ihrem Beginn anderswo geändert wurde (409 `provider_changed`).
- **Zuordnung** (`OidcAccountResolver`): zuerst (Provider, Subject), dann E-Mail nur bestätigt oder mit „Trust
  unverified email addresses“, anderes Subject wird abgelehnt.
- **Widerruf**: `invalid_client` eines gekoppelten Providers setzt „Reconnect needed“.
- **Reconnect gekoppelter Provider** (Marcus, 12.10.2026): WYSCH macht den alten Client ungültig, sobald derselbe
  Account einen neuen Code für dieselbe Redirect-URI einlöst. ReadyStackGo übernimmt den neuen Client sofort, wenn seine
  Redirect-URI die ist, mit der sich die Installation gerade anmeldet (aus der Basis-Adresse, auch der ungesetzten
  `http://localhost:5000`), und die Prüfung von Client-ID und Secret bestanden ist
  (`SsoSetupService.AdoptRedeemedPairingAsync`). Dann ist er der Client, den Anmeldungen brauchen, egal ob WYSCH den
  alten ersetzt hat oder dieser eine andere Redirect-URI hatte. Scheitert die erste Prüfung (Zeitüberschreitung, 5xx),
  übernimmt eine spätere bestandene Prüfung derselben Sitzung. Hat sich die Adresse während des Reconnect geändert,
  gilt E14: Die alte Verbindung bleibt bis zur bestandenen Test-Anmeldung. Einschalten verlangt in jedem Fall eine
  Test-Anmeldung.
- **Wizard** (`WizardSsoRun`, `WizardSsoService`): Start im Zeitfenster, dann bis zu 15 Minuten
  (`Wizard:SsoRunSeconds`); legt den ersten SystemAdmin ohne Passwort an. Integrierter Weg und WYSCH-Weg teilen eine
  Sperre in `SystemAdminRegistrationService`.

## Notzugang

`rsgo admin set-password <username> [--generate]` (`AdminCommandLine` in Infrastructure, Aufruf am Anfang von
`Program.Main`, Wrapper `/usr/local/bin/rsgo` im Image). Distributionen mit eigenem `Program.cs` übernehmen den
Aufruf, siehe `Distribution-Architecture.md`.
