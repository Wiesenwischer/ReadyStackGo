# Plan: Lokales Passwort nur nach erneuter Anmeldung

Vorhaben #506 · Spezifikation [`docs/specs/lokales-passwort-erneute-anmeldung.md`](../specs/lokales-passwort-erneute-anmeldung.md) ·
Entwurf [`docs/specs/lokales-passwort-erneute-anmeldung/entwurf/README.md`](../specs/lokales-passwort-erneute-anmeldung/entwurf/README.md)
(freigegeben mit dem Merge von PR #507) · Entscheidung „sudo mode“: Issue #504 (Marcus, 13.10.2026) · Stand der
Planung: `origin/main` 7ccfbc2, 13.10.2026

## 1. Kontext

Ein Konto ohne lokales Passwort (WYSCH-Weg im Wizard, per SSO angenommene Einladung) setzt sich heute im Profil eines,
allein mit einem gültigen Token: `POST /api/user/set-password` prüft nur, dass noch kein Passwort da ist
(`SetPasswordEndpoint.cs:43-64`). Ein gestohlenes Token oder ein offener Browser genügt so für einen dauerhaften
Zugang, der eine Sperre beim Identity Provider überlebt. Die Spezifikation verlangt das Muster „sudo mode“: vorher eine
frische Anmeldung beim verknüpften Provider (`max_age=0`, `auth_time` und `sub` geprüft), danach ein kurzes
Zeitfenster, erzwungen auf dem Server mit eigenem Fehlercode. Vorbild für den Ablauf ist die Test-Anmeldung des
Einrichtungslaufs: Start per API, Browser zum Provider, Rückkehr über den Callback des Providers mit einem Zweck im
`OidcFlowState`, gebunden an einen einmaligen `state` und das Cookie `rsgo_sso_flow` (`SsoSetupService.cs:488-549`,
`SsoFlows.cs:10-34`, `:103-148`).

## 2. Ergebnis aus Nutzersicht

- **Profil ohne Passwort:** Die Karte „Set a local password“ erklärt die Bestätigung und zeigt „Confirm with WYSCH“,
  bei mehreren eingeschalteten Providern einen Knopf je Provider. Der Knopf führt zur Anmeldeseite des Providers, auch
  wenn man dort noch angemeldet ist.
- **Zurück im Profil:** „Confirmed with WYSCH – Set your password within 5 minutes“, darunter das Formular. Nach
  „Set password“ verschwindet die Karte, und die Anmeldung mit Benutzername und Passwort geht.
- **Scheitern:** Die Karte nennt den Grund und bietet die Bestätigung erneut an: abgebrochen, Provider nicht erreichbar
  (mit Adresse, Ursache und `rsgo admin set-password <user>`), anderes Konto, Anmeldung nicht frisch, sonstiger Fehler.
  Nach 5 Minuten „Confirmation expired“. Ohne nutzbaren Provider (ausgeschaltet, entfernt) nur der Hinweis auf den
  Befehl im Container.
- **Mehrere Provider:** Karte und Warnung nennen alle; „Unlink“ geht dann auch ohne Passwort (Regel der Domain).
- **API:** `POST /api/user/set-password` ohne frische Bestätigung antwortet 403 `reauthentication_required`.
- **Passwort vergessen:** Der Reset per E-Mail setzt Konten ohne Passwort kein erstes Passwort mehr (E14).
- Website (en/de) und `docs/Architecture/Identity-Provider-Templates.md` beschreiben den Ablauf.

## 3. Umfang und Nicht-Umfang

**Umfang**

- Backend: Status und Start der erneuten Anmeldung, Zweck im Callback, Speicher der Bestätigungen, Prüfregeln,
  `set-password`, OIDC-Client (`max_age`, `prompt`, `auth_time`, Ursache bei „nicht erreichbar“), Passwort-Reset (E14).
- Oberfläche (`@rsgo/core`, `@rsgo/ui-generic`): Karte „Set a local password“, Profil, „Connected accounts“.
- Test-Identity-Provider, Unit-, Integrations-, Vitest- und Browsertests.
- Dokumentation: Website en/de, `docs/Architecture/Identity-Provider-Templates.md`, `docs/Security/Overview.md`.

**Nicht-Umfang**

- Was die Spezifikation ausschließt: „Change password“, ein E-Mail-Link als Weg zum ersten Passwort, erneute Anmeldung
  vor anderen Aktionen, zweiter Faktor in ReadyStackGo, Änderungen am Notzugang `rsgo admin set-password`.
- Eine Prüfung, ob nach „Unlink“ noch ein eingeschalteter Provider bleibt (Entwurf übernimmt die Regel der Domain,
  siehe Risiken).
- Der Punkt „`reason` nur die Ursache“ aus #504 für Verbindungstest und Wizard (E10 baut die Ursache nur für den
  OIDC-Client).
- Lokalisierung.

## 4. Entscheidungen, die in diesem Plan stecken

### Zeitfenster und Ort (offene Fragen der Spezifikation)

- **E1 – Zeitfenster 5 Minuten** ab der Rückkehr vom Provider (Vorschlag der Spezifikation, wie GitHub ungefähr).
  `Sso:ReauthenticationSeconds` darf es nur verkürzen, für Tests, nach dem Muster von `Sso:FlowStateSeconds`
  (`SsoFlows.cs:63-66`). Die Oberfläche nimmt die Dauer aus dem Status (`windowSeconds`) und zeigt sie als festen Text
  ohne Countdown (Entwurf, „Festlegungen ohne Vorgabe der Spezifikation“).
- **E2 – Bestätigung serverseitig im Speicher** (Vorschlag der Spezifikation): neuer `ReauthenticationStore`
  (Singleton, `IMemoryCache` wie `SsoFlowStore`, `SsoFlows.cs:51-96`), Schlüssel Benutzer + Anmeldesitzung (E3), ein
  Eintrag lebt 60 Minuten nach dem letzten Schreiben. Ein Neustart verwirft offene Bestätigungen. Nicht als Claim in
  einem neu ausgestellten Token: „einmal verbraucht“ ginge ohne Serverzustand nicht, und die Oberfläche müsste ihr
  Token tauschen.

### Bindung und Prüfung

- **E3 – Sitzung = Anmelde-Token und Browser.** Der Start (mit Bearer-Token) merkt sich Benutzer, die `jti` des Tokens
  (`TokenService.cs:67`) und das Cookie `rsgo_sso_flow` (`SsoFlowCookie.Ensure`, `SsoFlows.cs:108-121`, Lebensdauer
  10 + 5 Minuten). Die Rückkehr zählt nur mit diesem Cookie, das Setzen nur mit demselben Benutzer, derselben `jti`
  und demselben Cookie (`SsoFlowCookie.Matches`, `SsoFlows.cs:127-135`). Abmelden widerruft die `jti`
  (`LogoutEndpoint.cs:25-31`, Prüfung `Program.cs:105-118`) und beendet damit auch die Bestätigung. Grund: Startet
  jemand mit gestohlenem Token die erneute Anmeldung und schiebt den Link dem echten Benutzer unter, entsteht keine
  Bestätigung (dessen Browser hat das Cookie nicht); und eine Bestätigung des echten Benutzers nützt einem gestohlenen
  Token ohne dessen Cookie nichts. Ohne `jti` (kein Anmelde-Token) lehnen Start und Setzen mit 401 ab.
- **E4 – Anfrage an den Provider.** Über `IOidcService.BuildAuthorizeUrlAsync` wie die normale Anmeldung (PAR wenn
  verlangt, PKCE, `state`, `nonce`; `OidcService.cs:47-91`), dazu `max_age=0`. `prompt=login` nur, wenn die Discovery
  `login` in `prompt_values_supported` anbietet (`OpenIdConnectConfiguration.PromptValuesSupported` im Paket
  `Microsoft.IdentityModel.Protocols.OpenIdConnect` 8.14.0, `ReadyStackGo.Infrastructure.Security.csproj:10`); WYSCH ID
  bietet es an (Discovery, abgerufen am 13.10.2026). Grund: hilft bei Providern, die `max_age` übergehen, und stört
  keinen, der `login` nicht kennt. Geprüft wird nur `auth_time` (Spezifikation). Der Rücksprung nutzt die beim
  Provider registrierte Redirect-URI `/api/auth/oidc/{name}/callback` (`OidcEndpoints.cs:97-98`) mit dem neuen Zweck
  `Reauthentication` (`OidcFlowPurpose`, `SsoFlows.cs:10-20`): keine neue Redirect-URI, nichts beim Provider zu ändern.
- **E5 – Was als frische Anmeldung gilt.** Alle Bedingungen zugleich, als reine Regel in der Application-Schicht
  (`ReauthenticationRules`, ohne HTTP testbar wie `OidcAccountResolver`):
  1. Das ID-Token ist gültig wie heute: Signatur, Issuer, Audience, Laufzeit, `nonce`, `sub` (`OidcService.cs:172-199`).
  2. `auth_time` ist vorhanden und eine Zahl (Sekunden seit 1970, auch mit Nachkommastellen), nicht früher als
     `Start − 5 min` und nicht später als `jetzt + 5 min`. Start ist der Zeitpunkt des Start-Aufrufs auf dem Server. Die
     5 Minuten sind die Toleranz der übrigen Token-Prüfung (`ClockSkew`, `OidcService.cs:182`); sie wird eine
     gemeinsame Konstante. Sonst `not_recent`.
  3. `user.FindExternalIdentity(provider.Name)?.Subject` ist gleich `sub` (ordinal; der Provider ohne Groß/Klein wie
     `User.cs:214-221`, `ExternalIdentity.cs:27`). Sonst `different_account`. Kein `OidcAccountResolver`, keine neue
     Verknüpfung, kein ReadyStackGo-Token.
  4. Die Rückkehr bringt den einmaligen `state` dieses Starts (`SsoFlowStore.TakeOidc`, `SsoFlows.cs:74`, `:80-91`)
     und dessen Cookie (E3).

  `OidcUserInfo` (`OidcProviderSettings.cs:137-142`) bekommt dafür `AuthTime`.

### API

- **E6 – Endpunkte** (angemeldeter Benutzer, wie die übrigen unter `/api/user`):
  - `GET /api/user/reauthentication` liefert den Status dieser Sitzung:

    ```json
    {
      "windowSeconds": 300,
      "providers": [{ "name": "wysch", "displayName": "WYSCH", "iconUrl": "/api/identity-provider-templates/wysch/icon",
                      "address": "https://id.wysch.wiesenwischer.de/" }],
      "unavailableProviders": [{ "name": "company", "displayName": "Company SSO", "reason": "disabled" }],
      "state": "confirmed", "provider": "wysch", "providerDisplayName": "WYSCH",
      "expiresAt": "2026-10-13T12:05:00Z", "failure": null
    }
    ```

    `providers`: verknüpft und eingeschaltet, in der Reihenfolge der Verknüpfungen. `unavailableProviders`: verknüpft,
    aber ausgeschaltet (`disabled`) oder nicht mehr in `rsgo.oidc.json` (`removed`). `state`: `none`, `confirmed`,
    `expired`, `failed`. `failure`: `{ "code", "reason", "address" }`.
  - `POST /api/user/reauthentication` mit `{ "provider": "wysch" }` liefert `{ "url": "…" }`. Fehler: 401; 409
    `password_exists`; 409 `provider_unavailable` (unbekannt, nicht verknüpft oder ausgeschaltet); 502 `unreachable`
    (mit `reason`); 502 `provider_failed`. Die beiden 502 stehen danach auch im Status (E8).
  - Fehler im Format `{ code, message, reason }` wie `SsoErrorDto` (`SsoDtos.cs:160-167`), das `ApiError` der
    Oberfläche liest (`client.ts:43-59`).
- **E7 – `set-password`** prüft in dieser Reihenfolge: 401 → 409 Passwort vorhanden (wie heute,
  `SetPasswordEndpoint.cs:43-49`) → 403 `reauthentication_required` (keine gültige Bestätigung dieser Sitzung: fehlt,
  abgelaufen, verbraucht, anderer Benutzer, andere `jti`, anderes Cookie) → 400 Passwortregeln (die Bestätigung bleibt)
  → Bestätigung verbrauchen, unter einer Sperre wie `SsoFlowStore.Take` (`SsoFlows.cs:80-91`; von zwei gleichzeitigen
  Aufrufen gewinnt einer, der andere bekommt 403) → `SetInitialPassword` → 204.
- **E8 – Rückkehr und Ergebnis.** Der Callback leitet immer nach `{Basis-Adresse}/profile`, ohne Parameter: Die
  Oberfläche zeigt keinen Text aus der Adresse, das Ergebnis steht im Speicher der Sitzung. Ein neuer Start verwirft
  Bestätigung und Fehler der Sitzung. Fehlercodes:
  - `cancelled`: `error=access_denied`.
  - `unreachable`: Provider beim Start (Discovery, PAR) oder beim Token-Austausch nicht erreichbar, mit Ursache (E10)
    und Adresse (Authority des Providers).
  - `different_account`, `not_recent`: E5.
  - `failed` mit kurzem Grund: anderer Fehler des Providers (zum Beispiel `invalid_client`), ungültiges ID-Token,
    Provider inzwischen ausgeschaltet oder entfernt, Rückkehr in einem anderen Browser (Cookie fehlt: keine
    Bestätigung, Warnung im Protokoll), Rückkehr nach Ablauf des `state` (10 Minuten): wie
    `WizardSsoService.FailLateSignInReturn` (`WizardSsoService.cs:187-212`) statt heute `/login?error=oidc_state`
    (`OidcEndpoints.cs:184-194`).
- **E9 – Letztes Ergebnis am Provider.** Fehler des Providers (`invalid_client`, nicht erreichbar, ungültiges Token)
  schreibt die erneute Anmeldung wie eine normale Anmeldung an den Provider (`OidcSignInFailures.RecordAsync`,
  `OidcEndpoints.cs:258-274`, bei gekoppelten Providern mit „Reconnect needed“); eine bestandene setzt es zurück wie
  `OidcEndpoints.cs:235-240`. Ablehnungen nach E5 (anderes Konto, `auth_time`) ändern am Provider nichts, er hat
  richtig gearbeitet.
- **E10 – Ursache bei „nicht erreichbar“.** `OidcAuthorizeResult` und `OidcExchangeResult` (`IOidcService.cs:62-78`)
  bekommen `Reason`: nur die Ursache, englisch, für den Satz „ReadyStackGo could not reach <Adresse> (<Ursache>).“ des
  Entwurfs: `connection timed out`, `connection refused`, `name could not be resolved`, `TLS connection failed`,
  `connection failed`, `HTTP <Status>`. Gebildet aus `HttpRequestError` wie im Verbindungstest
  (`OidcConnectionChecker.cs:187-193`). Die bestehenden Aufrufer bleiben unverändert.
- **E11 – Protokoll** (`ILogger`, englisch, nie Token, Code, `state`, Cookie oder Passwort): Start (Information),
  bestanden (Information), gescheitert mit Code und Grund (Warning), Passwort gesetzt nach erneuter Anmeldung mit
  Provider (Information), `set-password` mit 403 abgelehnt (Information).

### Oberfläche

- **E12 – Karte „Set a local password“.**
  - Inhalt 640 px breit wie im Entwurf (heute 448 px, `SetLocalPasswordCard.tsx:49`); `ProviderButton`
    (`SsoComponents.tsx:36-64`) 320 px breit, untereinander, Abstand 16 px; `Alert` (`Alert.tsx:15-43`) in den Tönen
    des Entwurfs.
  - Die Karte lädt den Status selbst. Ein Knopf ruft den Start und navigiert nur zu `http(s)`-Adressen (`isHttpUrl`,
    wie `SetupSteps.tsx:430-441`). Abweichung vom Entwurf („Ein Ladezustand entfällt“): Bis die Antwort da ist (mit PAR
    bis zu 10 s, Timeout des Clients `Oidc`, `DependencyInjection.cs:21-26` im Security-Projekt), sind die Knöpfe
    gesperrt, gegen doppelte Starts; sonst kein Ladezustand. Nach einem Fehler des Starts lädt die Karte den Status neu.
  - 403 `reauthentication_required` beim Absenden zeigt „Confirmation expired“ (Entwurf), auch wenn der Server die
    Bestätigung nicht mehr kennt (Neustart).
  - Reine Logik (Zustand, Texte, Dauer, Namenslisten) in `pages/Profile/localPasswordCard.ts`, getestet mit Vitest,
    nach dem Muster von `Wizard/wizardFlow.ts`.
  - Texte aus dem Entwurf, Zahl und Namen zur Laufzeit. Texte für Zustände ohne Rahmen:
    - Sonstiger Fehler: „Confirmation failed“ / „<Provider> could not confirm it’s you (<Grund>). Try again. If it
      keeps failing, set the password on the server: run rsgo admin set-password <user> inside the container.“
    - Kein Provider: mehrere ausgeschaltet „WYSCH and Company SSO are disabled, so …“; entfernt „<Name> is no longer
      set up, so …“ (Mehrzahl „are no longer set up“); gemischt „<Namen> are disabled or no longer set up, so …“; ohne
      Verknüpfung „No provider is connected to your account, so …“; weiter wie im Entwurf „… you can’t confirm it’s you
      here. Set the password on the server: run rsgo admin set-password <user> inside the container.“
    - „Nicht erreichbar“ ohne Ursache: „ReadyStackGo could not reach <Adresse>. Try again later. …“
    - Dauer: volle Minuten „5 minutes“, „1 minute“, sonst Sekunden („30 seconds“, nur in Tests).
    - Status nicht ladbar: „The confirmation status could not be loaded.“ im Fehlerkasten wie
      `ConnectedAccounts.tsx:60-64`.
- **E13 – Profil mit mehreren Providern** (Entwurf, Rahmen `545:5683`):
  - Kopf der Karte und Warnung nennen alle verknüpften Provider (heute nur den ersten, `Profile.tsx:22`):
    „You sign in with WYSCH or Company SSO. … for example when they are unavailable.“ und „If both are unavailable,
    nobody can sign in.“; ab drei „A, B or C“ und „If all of them are unavailable“.
  - „Unlink“ ohne Passwort gesperrt nur bei höchstens einer Verknüpfung, wie `User.UnlinkExternalIdentity`
    (`User.cs:179-183`); der Hinweis darunter nur dann (heute ohne Passwort immer, `ConnectedAccounts.tsx:82`,
    `:89-94`). Nach einem „Unlink“ meldet `ConnectedAccounts` die neue Liste (heute nur beim Laden, `:29-38`), und die
    Karte lädt ihren Status neu.

### Passwort-Reset

- **E14 – Reset per E-Mail nur für Konten mit Passwort** (über die Liste der Spezifikation hinaus). Heute schickt
  `request-password-reset` den Link auch an Konten ohne Passwort (`PasswordResetEndpoints.cs:65-80`), und
  `reset-password` setzt mit `ChangePassword` ein erstes Passwort (`PasswordResetEndpoints.cs:148`; `User.cs:465-473`
  prüft nicht, ob eines da ist). Das ist ein Weg zum ersten Passwort ohne erneute Anmeldung, also der verworfene
  E-Mail-Weg. Neu: Für Konten ohne Passwort keine E-Mail (Antwort unverändert, keine Aufzählung von Konten), und
  `reset-password` antwortet 400 „Invalid or expired token.“ wie bei ungültigem Token. Folge: Wer nur per SSO angemeldet
  ist, kommt bei ausgefallenem Provider nur über `rsgo admin set-password` an ein Passwort, wie die Spezifikation es für
  „Kein Provider erreichbar“ vorsieht. Willst du das nicht, entfällt E14 und steht als Risiko im Plan.

### Tests und Dokumentation

- **E15 – Test-Identity-Provider:**
  - PAR nimmt `max_age` (ganze Zahl ≥ 0, sonst `invalid_request`) und `prompt` an und merkt sie sich im
    `PushedRequest` (`TestIdentityProviderModel.cs:73-80`); `/authorize` nimmt weiter nur `client_id` und
    `request_uri` an (`Program.cs:118-122`).
  - Mit `max_age` zeigt die Anmeldeseite „A new sign-in is required (max_age=0).“; der Browsertest prüft daran, dass
    ReadyStackGo es schickt. Die Seite zeigt ohnehin immer die Auswahl der Testnutzer, meldet also immer frisch an.
  - Die Entscheidung auf der Anmeldeseite merkt sich die Anmeldezeit, das ID-Token trägt sie als `auth_time`
    (`CreateIdToken`, `Program.cs:379-411`).
  - Steuerung für Tests wie `/test/clients/{clientId}/revoke` (`Program.cs:343-352`):
    `POST /test/next-sign-in?authTime=missing|stale` gilt für die nächste Anmeldung (`stale` = eine Stunde alt), danach
    wieder normal; andere Werte 400.
  - Die Discovery nennt `prompt_values_supported: ["none", "login"]` wie WYSCH ID.
  - `TestIdentityProviderHost` gibt den `ProviderState` heraus, damit Integrationstests `max_age` und `prompt` der
    PAR-Anfrage prüfen können.
- **E16 – Browsertest** (`e2e/sso-wysch.spec.ts`, läuft schon in „Build & Test“, `ci.yml:79-86`):
  - Der Test zum Profil (`sso-wysch.spec.ts:294-310`) spielt den Ablauf durch (Abschnitt 7) und setzt am Ende dasselbe
    Passwort `Local1234`, auf dem die folgenden Tests aufbauen (`:312-313`).
  - „Nicht erreichbar“ mit `docker pause`/`unpause` des Test-Providers statt `stop`/`start` (`:28-41`): Ein Neustart
    verlöre den gekoppelten WYSCH-Client und den Signaturschlüssel des Test-Providers (Zustand im Speicher, Schlüssel
    beim Start neu, `TestIdentityProviderModel.cs:108`, `:137`), die die späteren Tests brauchen. Die Ursache lautet dann
    „connection timed out“ wie im Entwurf, nach 10 s.
  - „Kein Provider“ und „abgelaufen“ über `page.route`, wie es `auth-email-oidc.spec.ts:66-67` schon tut: Den einzigen
    Provider des einzigen Admins kann man nicht ausschalten (`SsoSetupService.cs:594`, `:674-688`), und 5 Minuten zu warten
    kostet zu viel CI-Zeit. Die Regeln dahinter decken Integrationstests ab.
  - Bilder dieses Vorhabens nach `docs/plans/lokales-passwort-erneute-anmeldung/bilder/` unter den Namen der
    Entwurfsbilder. Die Bilder unter `docs/plans/identity-provider-vorlagen/bilder/` (`sso-wysch.spec.ts:20`) werden
    nicht neu committet; die Aufnahme `profil-ohne-passwort` (`:300`) entfällt.
  - Kosten: rund 40 s mehr je CI-Lauf.
- **E17 – Dokumentation:**
  - Website en/de `docs/configuration/single-sign-on.md`: neuer Abschnitt „Set a local password“ / „Lokales Passwort
    setzen“ (bestätigen, 5 Minuten, mehrere Provider, Fehler, Weg über den Container) nach „Don't lock yourself out“,
    dessen letzter Punkt (`:77`) darauf verweist; „Emergency access“ nennt den Fall „Provider aus oder nicht
    erreichbar“; ein Satz zu E14. Bild `sso-04-set-local-password.png` aus dem Browsertest.
  - `docs/Architecture/Identity-Provider-Templates.md`: Zweck `Reauthentication` (`:46-48`) und ein Punkt „Erneute
    Anmeldung“ (Speicher, Bindung, Regeln, Zeitfenster, Endpunkte), dazu E14.
  - `docs/Security/Overview.md` (`:18`): ein Satz zur erneuten Anmeldung.
- **E18 – Build-Ausgabe.** `src/ReadyStackGo.Api/wwwroot` ist eingecheckte Build-Ausgabe (`vite.config.ts:32-33`); die
  Umsetzung baut sie neu und committet sie.

## 5. Oberflächen

Verbindlich: [`docs/specs/lokales-passwort-erneute-anmeldung/entwurf/README.md`](../specs/lokales-passwort-erneute-anmeldung/entwurf/README.md),
Figma-Datei „ReadyStackGo Design“ (`RxVNdSKNs7PpJqkYgvb6a1`), Seite „App“ (`2:99`); die dunklen Rahmen im Modus
„Turquoise Dark“ (`2:2`) der Sammlung „Theme“. Links nach dem Muster
`https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=<a>-<b>`; Bilder unter
`../specs/lokales-passwort-erneute-anmeldung/entwurf/`. Abweichung: Knöpfe während des Starts gesperrt (E12).

| Oberfläche | Zustand | Rahmen hell / dunkel | Bilder | Was gebaut wird |
|---|---|---|---|---|
| Profil | vor der Bestätigung, ein Provider | [`545:5499`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-5499) / [`545:6819`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6819) | `profil-bestaetigen.png`, `…-dunkel.png` | Erklärung, `ProviderButton` „Confirm with WYSCH“ (E12) |
| Profil | vor der Bestätigung, mehrere Provider | [`545:5683`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-5683) / [`545:6899`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6899) | `profil-bestaetigen-mehrere-provider.png`, `…-dunkel.png` | ein Knopf je Provider; Kopf, Warnung, „Connected accounts“ nach E13 |
| Profil | bestätigt | [`545:5897`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-5897) / [`545:6985`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6985) | `profil-bestaetigt.png`, `…-dunkel.png` | `Alert` Success, Formular aus #498 mit 640 px |
| Karte | Zeitfenster abgelaufen | [`545:6573`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6573) / [`545:6766`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6766) | `karte-abgelaufen.png`, `…-dunkel.png` | `Alert` Warning, Knöpfe; auch nach 403 beim Absenden |
| Karte | abgebrochen | [`545:6607`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6607) / [`545:6775`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6775) | `karte-abgebrochen.png`, `…-dunkel.png` | `Alert` Warning, Knöpfe |
| Karte | Provider nicht erreichbar | [`545:6641`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6641) / [`545:6784`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6784) | `karte-nicht-erreichbar.png`, `…-dunkel.png` | `Alert` Error mit Adresse und Ursache (E10), Befehl, Knöpfe |
| Karte | anderes Konto | [`545:6675`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6675) / [`545:6793`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6793) | `karte-anderes-konto.png`, `…-dunkel.png` | `Alert` Error, Knöpfe |
| Karte | Anmeldung nicht frisch | [`545:6709`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6709) / [`545:6802`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6802) | `karte-nicht-frisch.png`, `…-dunkel.png` | `Alert` Error, Knöpfe |
| Karte | kein nutzbarer Provider | [`545:6743`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6743) / [`545:6811`](https://www.figma.com/design/RxVNdSKNs7PpJqkYgvb6a1?node-id=545-6811) | `karte-kein-provider.png`, `…-dunkel.png` | `Alert` Warning, ohne Knopf |

Zustände ohne Rahmen (E12): sonstiger Fehler (`Alert` Error wie „nicht erreichbar“), Varianten von „kein Provider“,
Laden, Status nicht ladbar, drei und mehr Provider.

Komponenten: keine neuen (Entwurf). `Alert` (`169:309`) ist `Alert.tsx`, `Provider Button` (`170:298`) ist
`ProviderButton`, `Provider Mark` (`168:261`) ist `ProviderMark` (`SsoComponents.tsx:24-34`), `Input` und `Button` sind
`TextField` und `Button` wie heute in der Karte (`SetLocalPasswordCard.tsx:50-80`). Keine neuen Tokens.

**Raster-Icons:** keine. Der Entwurf hat keine Platzhalterliste; die Symbole der Provider kommen als SVG aus den
Vorlagen.

## 6. Schritte der Umsetzung

1. **Application** – `OidcUserInfo.AuthTime` (`OidcProviderSettings.cs:137-142`); `Reason` in den Ergebnissen und eine
   Option für `max_age` an `BuildAuthorizeUrlAsync` (`IOidcService.cs:19-25`, `:62-78`; die drei heutigen Aufrufer
   `OidcEndpoints.cs:104`, `SsoSetupService.cs:500`, `WizardSsoService.cs:388` senden sie nicht); gemeinsame Konstante
   der Toleranz; neu `Services/Oidc/ReauthenticationRules.cs` (Prüfung E5, Einordnung der Provider für den Status E6).
2. **OIDC-Client** – `OidcService.cs`: `max_age` und `prompt` (E4), `auth_time` lesen, Ursachen (E10), Konstante statt
   `TimeSpan.FromMinutes(5)` (`:182`).
3. **Speicher und Dienst** – `Endpoints/Sso/SsoFlows.cs`: Zweck `Reauthentication`, `OidcFlowState` um `SessionId`
   und `StartedAt`; neu `Endpoints/Sso/ReauthenticationStore.cs` (E1–E3, E7, E8) und `ReauthenticationService.cs`
   (Status, Start, Rückkehr, späte Rückkehr, Verbrauchen; E9, E11); Registrierung neben den SSO-Diensten
   (`Program.cs:151-156`).
4. **Endpunkte** – neu `Endpoints/User/ReauthenticationEndpoints.cs` (E6); `SetPasswordEndpoint.cs` (E7);
   `OidcEndpoints.cs`: Zweig `Reauthentication` (`:196-204`) und späte Rückkehr (`:184-194`).
5. **Passwort-Reset** – `PasswordResetEndpoints.cs` (E14).
6. **Test-Identity-Provider** – `Program.cs`, `TestIdentityProviderModel.cs` (E15); `TestIdentityProviderHost.cs`
   (`State`).
7. **.NET-Tests** – Abschnitt 7.
8. **`@rsgo/core`** – `api/user.ts` (`user.ts:38-47`): Typen, `getReauthentication`, `startReauthentication`; Export
   wie bisher über `index.ts:26`.
9. **Oberfläche** – `pages/Profile/localPasswordCard.ts` mit Test, `SetLocalPasswordCard.tsx` neu aufgebaut,
   `Profile.tsx` (Namen, Warnung), `ConnectedAccounts.tsx` (E13).
10. **Browsertest** – `e2e/sso-wysch.spec.ts` (E16): Helfer für `pause`/`unpause`, Bildordner, Aufnahmen nur der Karte.
11. **Dokumentation** – E17.
12. **Build-Ausgabe** – E18, dann die Prüfungen aus `CLAUDE.md` („Vorhaben: bauen und testen“).

## 7. Tests und Abnahme

Rot auf dem heutigen `main` sind alle Tests der neuen Endpunkte und Regeln, inhaltlich besonders: `set-password` ohne
Bestätigung (heute 204), `auth_time` im Token des Test-Providers (heute keines), Reset für Konten ohne Passwort (heute
gesetzt), der Ablauf im Browsertest (heute kein „Confirm with …“). Vorher und nachher grün bleiben: 409 bei vorhandenem
Passwort, 401 anonym, die normale Anmeldung ohne `max_age`, der Reset für Konten mit Passwort, der Notzugang.

**Unit (`tests/ReadyStackGo.UnitTests`, xUnit, FluentAssertions)**

- `Application/Oidc/ReauthenticationRulesTests`: bestanden; `auth_time` fehlt; `Start − 5 min − 1 s` → `not_recent`,
  `Start − 5 min` → bestanden; `jetzt + 5 min + 1 s` → `not_recent`; anderes `sub`; Provider nicht verknüpft;
  Provider „Legacy“ mit Verknüpfung „legacy“ → bestanden. Einordnung (Filter): eingeschaltet und verknüpft → nutzbar;
  ausgeschaltet → `disabled`; ohne Eintrag in `rsgo.oidc.json` → `removed`; eingeschaltet, aber nicht verknüpft → nicht
  gelistet; Reihenfolge der Verknüpfungen.
- `Api/Sso/ReauthenticationStoreTests` (Muster `SsoFlowStoreTests.cs:10-16`): Fenster 300 s; per Konfiguration kürzer;
  `0`, negativ, `600` → 300 s; gültig bis zum Ablauf, danach `expired` und nicht verbrauchbar; einmal verbrauchbar;
  anderer Benutzer, andere `jti`, anderes Cookie → nichts; neuer Start verwirft Bestätigung und Fehler; späte Rückkehr
  findet die Sitzung über den `state`.
- `Infrastructure/Security/OidcServiceTests` (mit `FakeOidcProvider`): `max_age=0` in der Adresse (ohne PAR) und im
  PAR-Formular; `prompt=login` nur mit `login` in `prompt_values_supported`; normale Anfrage ohne beides; `auth_time` als
  Zahl, mit Nachkommastellen, fehlend, als Text; Ursachen `connection refused`, `name could not be resolved`,
  `connection timed out`, `HTTP 503` an Discovery, PAR und Token-Endpunkt.

**Integration (`tests/ReadyStackGo.IntegrationTests/Sso`, Test-Provider im Prozess, `SsoTestContext`)**

- Neu `ReauthenticationIntegrationTests`:
  - Ablauf: Start (PAR mit `max_age=0` und `prompt=login`, geprüft am `ProviderState`), Anmeldung als „alex“, Status
    `confirmed`, `set-password` 204, Anmeldung mit Passwort; die Verknüpfung bleibt.
  - `set-password` ohne Bestätigung → 403 `reauthentication_required` (heute 204, rot).
  - Abgelaufen (`Sso:ReauthenticationSeconds=1`, warten wie `WizardSsoIntegrationTests.cs:518-524`) → 403, Status
    `expired`.
  - Anderes Konto („uma“) → `different_account`; keine neue Verknüpfung, kein Konto für „uma“, kein `#token=` in der
    Weiterleitung.
  - `auth_time` fehlt bzw. eine Stunde alt (`/test/next-sign-in`) → `not_recent`, danach 403.
  - Abbruch → `cancelled`. Nicht erreichbar beim Start (`Idp.Unreachable`, `TestIdentityProviderHost.cs:42-46`) → 502
    `unreachable` mit `reason` `connection refused`; nicht erreichbar beim Token-Austausch → `unreachable`.
  - Provider ausgeschaltet → Start 409, Status listet ihn unter `unavailableProviders` (`disabled`); Eintrag aus
    `rsgo.oidc.json` entfernt, Verknüpfung noch da → `removed`; zwischen Start und Rückkehr ausgeschaltet → `failed`.
  - Bestätigung eines anderen Kontos: Benutzer B im selben Browser → 403. Derselbe Benutzer mit neuem Token (andere
    `jti`) → 403. Rückkehr in einem anderen Browser → keine Bestätigung, Status `failed`, Setzen 403.
  - Zweites Setzen: nacheinander → 409, das erste Passwort gilt; zwei gleichzeitige Aufrufe → genau einer 204.
  - 400 (Passwortregeln) lässt die Bestätigung stehen, das nächste gültige Passwort geht ohne neue Bestätigung.
  - Konto mit Passwort → Start 409 `password_exists`. Späte Rückkehr (`Sso:FlowStateSeconds=1`) → `failed` statt
    Anmeldeseite.
  - Normale Anmeldung von der Anmeldeseite zählt nicht (danach 403) und schickt kein `max_age`.
  - `invalid_client` bei der erneuten Anmeldung an einem gekoppelten Provider → „Reconnect needed“ (E9).
  - Protokoll (E11): Mit einem im Test eingehängten Logger erscheinen die Zeilen für bestanden, gescheitert und
    gesetzt, und keine enthält Passwort, Code, `state` oder Token.
- `UserLocalPasswordIntegrationTests` angepasst: Der Ablauf (`:20-42`) und der Test mit 400 (`:55-66`) bestätigen
  zuerst; 409 (`:44-53`), 401 (`:68-73`) und die Warnung (`:75-91`) bleiben.
- `TestIdentityProviderTests`: `max_age` ungültig → `invalid_request`; Token mit `auth_time` zur Anmeldezeit;
  `/test/next-sign-in` wirkt genau einmal; Anmeldeseite mit Hinweis bei `max_age`; Discovery mit
  `prompt_values_supported`.
- Neu `PasswordResetIntegrationTests` (E14): `reset-password` mit gültigem Token für ein Konto ohne Passwort → 400,
  weiter ohne Passwort (heute gesetzt, rot); mit Passwort → 200; `request-password-reset` schickt mit eingeschaltetem
  SMTP (Ersatz von `ISmtpSettingsService` und `IEmailService` über `ConfigureTestServices`) für ein Konto ohne Passwort
  keine E-Mail, für eines mit Passwort eine.

**Frontend (Vitest, reine Logik, `vitest.config.ts:7-12`)**

- `localPasswordCard.test.ts`: Ansicht je Status (ein Provider, mehrere, bestätigt, `expired`, jeder Fehlercode, kein
  Provider in allen Varianten); 403 nach „bestätigt“ → abgelaufen mit dem Provider der Bestätigung; Ursache vorhanden
  oder nicht; Dauer (300, 60, 30 s); Namenslisten mit einem, zwei, drei Namen; Text der Warnung; „Unlink“ gesperrt
  nur ohne Passwort und mit höchstens einer Verknüpfung.

**Browsertest (`e2e/sso-wysch.spec.ts`, Test zum Profil)**, der Reihe nach als Alex, zuerst nur mit WYSCH verknüpft:

1. Profil: Karte vor der Bestätigung, „Unlink“ gesperrt → `profil-bestaetigen` und das Website-Bild.
2. „Kein Provider“ (Status per `page.route`) → `karte-kein-provider`.
3. „Confirm with WYSCH“, beim Test-Provider „Cancel“ → `karte-abgebrochen`.
4. Bestätigen, Hinweis „max_age=0“ auf der Anmeldeseite, „Uma Unverified“ wählen → `karte-anderes-konto`.
5. `/test/next-sign-in?authTime=stale`, bestätigen als Alex → `karte-nicht-frisch`.
6. Test-Provider pausieren, bestätigen → „WYSCH is not reachable“ mit „(connection timed out)“ →
   `karte-nicht-erreichbar`; fortsetzen.
7. Abmelden, über den Provider `company` (Vorlage „Company SSO“) als Alex anmelden (verknüpft über die bestätigte
   E-Mail, `OidcAccountResolver.cs:60-86`), Profil: zwei Knöpfe, „Unlink“ frei → `profil-bestaetigen-mehrere-provider`.
   Der Anzeigename dieses Providers ist im Browsertest „company“, weil die Vorlage keinen setzt und der Test ihn beim
   Speichern lässt (`SsoSetupService.cs:91`, `SetupSteps.tsx:532`); das Bild zeigt also „Confirm with company“ statt
   „Company SSO“. Namen sind im Entwurf Beispiele.
8. „Confirm with WYSCH“ als Alex → „Confirmed with WYSCH“ → `profil-bestaetigt`.
9. Absenden mit `set-password` per `page.route` auf 403 → „Confirmation expired“ → `karte-abgelaufen`; Route weg.
10. Erneut bestätigen, `Local1234` setzen → Karte weg; abmelden, mit Passwort anmelden (Abnahme der Spezifikation).

**Bilder im Umsetzungs-PR:** die neun Zustände aus Abschnitt 5, je hell und dunkel, unter
`docs/plans/lokales-passwort-erneute-anmeldung/bilder/`, die Karten-Zustände nur mit der Karte wie im Entwurf; dazu
`sso-04-set-local-password.png` der Website. Zustände ohne Weg im Browsertest (sonstiger Fehler, späte Rückkehr,
anderer Browser) belegen die Integrationstests.

**Pflicht-Checks:** „Build & Test“ (`ci.yml:20-114`) mit dem SSO-Browsertest, dazu die Prüfungen aus `CLAUDE.md`.

**Abnahme gegen WYSCH ID:** von Hand durch Marcus nach dem Release: „Confirm with WYSCH“ zeigt die Anmeldeseite, obwohl
man bei WYSCH angemeldet ist, danach lässt sich das Passwort setzen. CI ruft WYSCH nie auf.

## 8. Wo umgesetzt wird

GitHub-Runner (`ubuntu-latest` mit Docker): Build, alle Tests, Browsertest über `scripts/sso-e2e.sh`, das „Build &
Test“ schon aufruft (`ci.yml:85-86`); `ci.yml` ändert sich nicht. Eine Umsetzungsrolle braucht es nicht. Die Abnahme
gegen die echte WYSCH ID macht Marcus von Hand (Abschnitt 7).

## 9. Risiken

- Nicht nachgelesen: dass WYSCH ID `max_age` auswertet, trotz „Angemeldet bleiben“ die Anmeldeseite zeigt und
  `auth_time` ins ID-Token schreibt (Spezifikation nach WYSCH `AuthorizationEndpoints.cs` und `docs/CLIENT-PAIRING.md`;
  das Repo antwortet diesem Lauf mit 404). Annahme: wie die Spezifikation. Sonst scheitert jede Bestätigung mit „No new
  sign-in“, und es bleibt nur `rsgo admin set-password`. Die Discovery von WYSCH ID nennt in `claims_supported` nur
  `aud`, `exp`, `iat`, `iss`, `sub` (abgerufen am 13.10.2026); das schließt `auth_time` nicht aus.
- Nicht nachgelesen: wie andere Provider (Generic OIDC) mit `max_age` und `auth_time` umgehen. Annahme: Keycloak wie in
  der Spezifikation; ein Provider ohne `auth_time` kann nicht bestätigen, dann bleibt nur der Befehl im Container.
- Toleranz: Ein Provider, der `max_age` und `prompt` übergeht, aber eine Anmeldung von höchstens 5 Minuten vor dem
  Start hat, besteht (dieselbe Toleranz wie die Token-Prüfung, so verlangt die Spezifikation).
- Ein Neustart verwirft Bestätigungen (E2).
- Öffnet jemand ReadyStackGo unter einer anderen Adresse als der Basis-Adresse, kommt er dort ohne Cookie und ohne
  Anmeldung zurück: keine Bestätigung, wie heute bei der Test-Anmeldung (`SsoSetupService.cs:515-518`).
- Nicht ausprobiert: `docker pause` auf `ubuntu-latest` (E16). Annahme: geht. Sonst nimmt der Browsertest auch diesen
  Zustand per `page.route` auf.
- Distributionen mit eigenem `Program.cs` müssen die neuen Dienste registrieren wie die übrigen SSO-Dienste
  (`Program.cs:151-156`; `Distribution-Architecture.md:172`: „Rest wie in ReadyStackGo.Api/Program.cs“). Nicht
  nachgelesen (privates Repo): ob `@rsgo/ui-ams` `set-password` selbst aufruft; dann bekäme es 403. Annahme: nein.
- „Unlink“ mit mehreren Providern zählt Verknüpfungen, nicht eingeschaltete Provider (`User.cs:179-183`, Entwurf): Wer
  den einzigen eingeschalteten entfernt, behält nur einen ausgeschalteten.
- E14 ändert „Passwort vergessen“ für Konten ohne Passwort: keine E-Mail mehr.
- Außerhalb dieses Vorhabens, nur gelesen, nicht ausprobiert: Reset- und Bestätigungs-Tokens tragen `uid` und sind mit
  Schlüssel, Issuer und Audience der Anmelde-Tokens signiert (`PasswordResetTokenService.cs:28-45`,
  `EmailVerificationTokenService.cs:28-45`, Prüfung `Program.cs:74-84`); der JWT-Handler nähme sie als Anmeldung an.
  Rollen fehlen ihnen (`RbacService.cs:108-110`), und die erneute Anmeldung braucht trotzdem den Provider. Gehört in ein
  eigenes Issue.
- Die eingecheckte Build-Ausgabe (E18) führt bei parallelen Oberflächen-PRs zu Konflikten.

## 10. Offene Fragen

Keine. E14 geht über die Liste der Spezifikation hinaus; mit der Freigabe entscheidest du es mit.
