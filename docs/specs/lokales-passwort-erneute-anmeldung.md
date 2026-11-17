# Lokales Passwort nur nach erneuter Anmeldung

> **Verworfen** (Marcus, 13.10.2026): Vorhaben #506 ist geschlossen, Plan-PR #508 nicht gemergt. Ein Admin-Token erlaubt ohnehin,
> Container mit Zugriff auf den Docker-Host zu deployen; die erneute Anmeldung rechtfertigt keinen eigenen Ablauf. Das erste
> lokale Passwort setzt ein angemeldetes Konto weiter einmalig selbst. Diese Datei bleibt als Material.

## Ziel

Ein Konto ohne lokales Passwort kann sich im Profil eines setzen („Set a local password“, Vorhaben #498). Heute genügt
dafür eine gültige Sitzung. Ein gestohlenes Token oder ein offen gelassener Browser reicht also, um dem Konto ein
Passwort zu geben, und das ist dann ein dauerhafter Zugang, der auch eine Sperre beim Identity Provider überlebt.
Künftig verlangt ReadyStackGo vorher eine frische Anmeldung beim Identity Provider des Kontos, wie GitHub im „sudo
mode“ (Marcus, 13.10.2026).

## Was dazugehört

- **Erneute Anmeldung vor dem Setzen.** „Set a local password“ im Profil schickt den Benutzer zuerst zu einer
  Anmeldung beim Identity Provider, mit dem das Konto verknüpft ist. Erst nach dessen Rückkehr nimmt ReadyStackGo das
  neue Passwort an.
- **Prüfung auf dem Server.** `POST /api/user/set-password` setzt das Passwort nur, wenn für dieses Konto eine frische
  erneute Anmeldung vorliegt (Regeln unter „Vorgaben“). Sonst antwortet es mit einem eigenen Fehlercode, den die
  Oberfläche erkennt.
- **Mehrere verknüpfte Provider.** Hat das Konto mehrere, wählt der Benutzer, bei welchem er sich erneut anmeldet. Jeder
  eingeschaltete, verknüpfte Provider genügt.
- **Kein Provider erreichbar oder nutzbar.** Ist der Provider nicht erreichbar, ausgeschaltet oder entfernt, nennt das
  Profil den Weg über den Container: `rsgo admin set-password <user>` (Vorhaben #498, E23).
- **Test-Identity-Provider.** `tests/ReadyStackGo.TestIdentityProvider` beherrscht `max_age` und liefert `auth_time`,
  damit Integrations- und Browsertests den Ablauf durchspielen können, auch eine zu alte und eine fehlende
  `auth_time`.
- **Dokumentation.** Website (en/de: Profil, „Don't lock yourself out“ in `configuration/single-sign-on.md`) und
  `docs/Architecture/Identity-Provider-Templates.md` beschreiben den Ablauf.

## Was ausdrücklich nicht dazugehört

- Konten mit Passwort: „Change password“ bleibt, wie es ist, mit dem alten Passwort.
- Ein Link per E-Mail als Weg zum ersten Passwort. SMTP ist optional und fehlt gerade auf frischen Installationen mit
  WYSCH (verworfen am 13.10.2026).
- Erneute Anmeldung vor anderen Aktionen (Unlink, Provider ändern, Benutzerverwaltung). Das wäre ein eigenes Vorhaben.
- Zweiter Faktor in ReadyStackGo selbst. Den verlangt, wenn überhaupt, der Identity Provider.
- Der Notzugang `rsgo admin set-password` ändert sich nicht.

## Vorgaben

- **Entscheidung:** Muster „sudo mode“ mit erneuter Anmeldung beim Identity Provider (Marcus, 13.10.2026, Issue
  #504). Vorbilder: GitHub „sudo mode“, Keycloak mit `max_age` bzw. `prompt=login`.
- **Anfrage an den Provider:** Autorisierung wie bei der normalen Anmeldung (PAR, wo die Vorlage es verlangt, PKCE,
  `state`, `nonce`), dazu `max_age=0`. WYSCH ID wertet `max_age` aus und zeigt dann die Anmeldeseite, auch wenn der
  Benutzer dort noch angemeldet ist („Angemeldet bleiben“ zählt nicht als Anmeldung), und schreibt `auth_time` ins
  ID-Token (WYSCH `AuthorizationEndpoints.cs`, `docs/CLIENT-PAIRING.md`). `prompt=login` darf zusätzlich gesendet
  werden, verlassen wird sich nur auf `max_age` und `auth_time`.
- **Was als frische Anmeldung gilt:** Alle Bedingungen zugleich:
  - Das ID-Token ist gültig (Signatur, Issuer, Audience, `nonce`), wie bei der normalen Anmeldung.
  - `auth_time` ist vorhanden und nicht älter als der Beginn dieser erneuten Anmeldung, mit derselben Toleranz für
    Uhrabweichung wie die übrige Token-Prüfung. Ohne `auth_time` wird abgelehnt. OIDC Core verlangt `auth_time`, wenn
    `max_age` angefragt war.
  - (Provider, `sub`) ist eine Verknüpfung des angemeldeten Kontos. Eine Anmeldung mit einem anderen Konto beim Provider
    zählt nicht und ändert keine Verknüpfung.
  - Die erneute Anmeldung wurde von genau dieser Sitzung des Kontos gestartet (einmaliger `state`, gebunden an den
    Benutzer).
- **Zeitfenster:** Nach der Rückkehr darf das Konto das Passwort für ein kurzes Zeitfenster setzen. Die Länge legt der
  Plan fest (siehe „Offene Fragen“). Mit dem erfolgreichen Setzen ist die Bestätigung verbraucht. Ein abgelaufenes
  Fenster verlangt eine neue erneute Anmeldung.
- **Fehlercodes:** Ohne gültige Bestätigung antwortet `set-password` mit 403 und einem eigenen Code (Vorschlag
  `reauthentication_required`). Hat das Konto inzwischen ein Passwort, bleibt es bei 409 wie heute.
- **Scheitern beim Provider:** Abbruch (`access_denied`), Provider nicht erreichbar, fremdes Konto, fehlende oder zu
  alte `auth_time` führen zurück ins Profil, mit einer verständlichen Meldung und ohne gesetztes Passwort.
- **Protokoll:** Erneute Anmeldung (bestanden, gescheitert mit Grund) und das Setzen des Passworts werden protokolliert,
  ohne Token oder Passwort.
- **Texte der Oberfläche englisch** (bis zur Lokalisierung vor 1.0, `CLAUDE.md`).
- **Tests** nach `CLAUDE.md`, besonders die Fehlerfälle: ohne erneute Anmeldung, abgelaufenes Fenster, zweites Setzen
  mit derselben Bestätigung, `auth_time` fehlt oder ist zu alt, fremdes `sub`, Bestätigung eines anderen Kontos,
  Provider ausgeschaltet, entfernt oder nicht erreichbar, Abbruch beim Provider, Konto hat schon ein Passwort.

## Oberfläche und Bilder

Betroffen ist die Karte „Set a local password“ im Profil (heute `SetLocalPasswordCard.tsx`, Entwurf von #498: Rahmen
`177:4834` hell und `177:5913` dunkel in der Figma-Datei von ReadyStackGo, Bilder unter
`docs/specs/identity-provider-vorlagen/entwurf/`).

Verbindlich: Entwurf in `docs/specs/lokales-passwort-erneute-anmeldung/entwurf/` (freigegeben mit dem Merge von PR
#507). Entworfen sind, hell und dunkel:

- Karte vor der Bestätigung: Erklärung und Knopf „Confirm with <Provider>“; bei mehreren Providern die Auswahl.
- Karte nach der Rückkehr: das Formular wie heute, mit dem Hinweis, wie lange es noch gilt.
- Fenster abgelaufen: erneut bestätigen.
- Fehler: abgebrochen, Provider nicht erreichbar, anderes Konto beim Provider, Bestätigung nicht frisch genug.
- Kein nutzbarer Provider: Hinweis auf `rsgo admin set-password`.

Richtung: die bestehende Karte und die Alerts aus #498.

## Abnahme

- Ein Konto ohne Passwort kann im Profil erst nach einer frischen Anmeldung beim verknüpften Provider ein Passwort
  setzen; danach meldet es sich mit Benutzername und Passwort an.
- `POST /api/user/set-password` ohne frische Bestätigung antwortet 403 mit dem eigenen Code; ein Integrationstest dafür
  ist auf dem heutigen `main` rot.
- Fremdes `sub`, fehlende oder zu alte `auth_time`, abgelaufenes Fenster und zweites Setzen werden abgelehnt
  (Integrationstests).
- Der SSO-Browsertest (`scripts/sso-e2e.sh`) spielt den Ablauf mit dem Test-Identity-Provider durch: bestätigen,
  Passwort setzen, abmelden, mit Passwort anmelden; dazu ein Abbruch beim Provider.
- Website und Architektur-Dokument beschreiben den Ablauf.

## Material

- Vorhaben #498, Spezifikation `docs/specs/identity-provider-vorlagen.md`, Plan `docs/plans/identity-provider-vorlagen.md`
  (E22 Profil, E23 Notzugang), PR #501.
- Issue #504, Abschnitt „Entschieden: lokales Passwort nur nach frischer Anmeldung“, und die achte Prüfung von PR #501
  (Befund „sollte“ 10).
- Heutiger Code: `src/ReadyStackGo.Api/Endpoints/User/SetPasswordEndpoint.cs`,
  `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Profile/SetLocalPasswordCard.tsx`, die Test-Anmeldung im
  Einrichtungslauf (`SsoSetupService`) als Vorbild für einen Anmeldeablauf außerhalb der normalen Anmeldung.
- WYSCH: `docs/CLIENT-PAIRING.md` (ID-Token mit `auth_time`), `src/Wysch.Identity/Authorization/AuthorizationEndpoints.cs`
  (`max_age`).

## Offene Fragen

- **Länge des Zeitfensters.** Vorschlag: 5 Minuten.
- **Wo die Bestätigung lebt.** Serverseitig je Benutzer im Speicher (wie die Einrichtungssitzungen), oder als Claim in
  einem neu ausgestellten Token. Vorschlag: serverseitig im Speicher, ein Neustart verlangt dann eine neue Bestätigung.
