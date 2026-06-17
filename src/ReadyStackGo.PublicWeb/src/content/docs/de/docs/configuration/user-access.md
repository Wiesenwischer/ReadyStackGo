---
title: Benutzerzugang, E-Mail & Single Sign-On
description: Benutzer per E-Mail einladen, E-Mail-Adressen verifizieren, per E-Mail oder Benutzername anmelden und externe OIDC-Provider (Single Sign-On) anbinden.
---

ReadyStackGo verwaltet Benutzerzugänge über **Admin-Einladungen**: Es gibt keine
öffentliche Selbstregistrierung — ein Administrator lädt eine E-Mail-Adresse ein, und die
eingeladene Person bestätigt ihren Zugang über einen Link und setzt ihr Passwort. Zusätzlich
unterstützt ReadyStackGo die **Anmeldung per E-Mail oder Benutzername** sowie **Single
Sign-On (SSO) über generische OIDC-Provider** (OpenID Connect).

## Übersicht

| Funktion | Beschreibung |
|----------|--------------|
| Admin-Einladung | Administrator lädt eine E-Mail-Adresse mit einer Rolle ein |
| E-Mail-Verifikation | Der Einladungslink ist der Besitznachweis; eigene Adresse später bestätigbar |
| Login per E-Mail oder Username | Beide Kennungen funktionieren, abwärtskompatibel |
| Single Sign-On (OIDC) | Anmeldung über externe Identity-Provider (z. B. IdentityAccess, Keycloak) |

:::note[Voraussetzung]
Einladungen und Verifikations-Mails benötigen einen konfigurierten **SMTP-Server**.
Konfigurieren Sie daher zuerst die E-Mail-Einstellungen.
:::

---

## Schritt 1: E-Mail (SMTP) konfigurieren

Öffnen Sie **Settings → Email (SMTP)**, aktivieren Sie den Versand und tragen Sie Host, Port,
Zugangsdaten und die Absenderadresse ein. Mit **Send test** verschicken Sie eine Testnachricht,
um die Konfiguration zu prüfen.

![SMTP-Einstellungen](/images/docs/auth-03-smtp-settings.png)

:::tip[Passwort bleibt erhalten]
Beim erneuten Speichern können Sie das Passwortfeld leer lassen — das gespeicherte Passwort
bleibt dann unverändert erhalten. Es wird verschlüsselt abgelegt und nie wieder angezeigt.
:::

---

## Schritt 2: Einen Benutzer einladen

Unter **Settings → User Invitations** laden Sie eine Person per E-Mail ein. Wählen Sie die
**Rolle** (Viewer, Operator, Organization Owner oder System Administrator). Für
organisationsgebundene Rollen geben Sie zusätzlich die **Organization ID** an; System
Administrators sind nicht an eine Organisation gebunden.

![Benutzer einladen](/images/docs/auth-05-invitations.png)

Nach dem Versand erscheint die Einladung in der Liste mit Status **Pending** und einem
Ablaufdatum. Offene Einladungen können jederzeit **widerrufen** werden.

---

## Schritt 3: Einladung annehmen

Die eingeladene Person öffnet den Link aus der E-Mail (`/accept-invite?token=…`), sieht ihre
Adresse und vergibt ein Passwort. Mit dem Annehmen gilt die E-Mail-Adresse als **verifiziert**
(der Link ist der Besitznachweis), das Konto wird aktiviert und die zugewiesene Rolle gesetzt.

![Einladung annehmen](/images/docs/auth-06-accept-invite.png)

---

## Anmeldung per E-Mail oder Benutzername

Auf der Anmeldeseite kann sowohl die **E-Mail-Adresse** als auch der **Benutzername**
verwendet werden. Sind OIDC-Provider konfiguriert, erscheinen zusätzlich
„Sign in with …"-Schaltflächen.

![Anmeldeseite](/images/docs/auth-01-login.png)

### E-Mail-Adresse bestätigen

Ist die eigene E-Mail-Adresse noch nicht verifiziert und SMTP konfiguriert, erscheint im
oberen Bereich ein Hinweisbanner. Über **Send verification email** wird ein Bestätigungslink
verschickt.

![Banner „E-Mail nicht verifiziert"](/images/docs/auth-07-verify-banner.png)

:::note[Erster Administrator]
Die beim Setup angelegte Administrator-Adresse wird bewusst **nicht** automatisch als
verifiziert markiert. Der Administrator kann sich dennoch per Passwort anmelden und seine
Adresse später bestätigen, sobald SMTP eingerichtet ist.
:::

---

## Single Sign-On (OIDC)

Unter **Settings → Single Sign-On (OIDC)** konfigurieren Sie einen oder mehrere generische
OpenID-Connect-Provider. Pro Provider werden Name, Anzeigename, Authority (Issuer-URL),
Client-ID, Client-Secret und Scopes hinterlegt.

![OIDC-Einstellungen](/images/docs/auth-04-oidc-settings.png)

Aktivierte Provider erscheinen als Schaltfläche auf der Anmeldeseite. Nach erfolgreicher
Anmeldung beim Provider stellt ReadyStackGo ein eigenes Session-Token aus.

:::caution[Nur eingeladene oder bekannte Identitäten]
Ein OIDC-Login funktioniert ausschließlich, wenn bereits ein Benutzer mit der zurückgelieferten
E-Mail-Adresse existiert **oder** eine offene Einladung für diese Adresse vorliegt. Unbekannte
Identitäten werden abgewiesen (kein automatisches Anlegen).
:::

---

## Einstellungsübersicht

Alle genannten Bereiche finden Sie gebündelt unter **Settings**.

![Einstellungsübersicht](/images/docs/auth-02-settings.png)

---

## Rollen

| Rolle | Geltungsbereich | Zweck |
|-------|-----------------|-------|
| Viewer | Organisation | Nur-Lese-Zugriff |
| Operator | Organisation | Deployments verwalten |
| Organization Owner | Organisation | Vollzugriff auf die Organisation |
| System Administrator | Global | Voller Systemzugriff |
